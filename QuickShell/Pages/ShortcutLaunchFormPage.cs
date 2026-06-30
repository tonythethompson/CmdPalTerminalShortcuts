using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using QuickShell.Models;
using QuickShell.Services;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace QuickShell.Pages;

internal sealed partial class ShortcutLaunchFormPage : ContentPage
{
    public const string PageId = "com.quickshell.shortcut.launch-form";

    private readonly TerminalShortcut _shortcut;
    private readonly WorkspaceEntry _launch;
    private readonly Action<TerminalShortcut> _onChanged;
    private readonly bool _isNewLaunch;

    public ShortcutLaunchFormPage()
    {
        if (!ShortcutEditorNavigationState.TryTakeLaunchForm(
                out var shortcut,
                out var launch,
                out var onChanged,
                out var isNew))
        {
            shortcut = ShortcutEditorState.CreateNew();
            launch = shortcut.Launches[0];
            onChanged = static _ => { };
            isNew = false;
        }

        _shortcut = shortcut;
        _launch = launch;
        _onChanged = onChanged;
        _isNewLaunch = isNew;
        Id = PageId;
        Icon = new IconInfo(TerminalLaunchGlyphs.GetForLaunch(launch));
        Title = isNew ? "Add terminal" : $"Edit {launch.Label}";
        Name = isNew ? "Add" : "Edit";
    }

    public ShortcutLaunchFormPage(
        TerminalShortcut shortcut,
        WorkspaceEntry launch,
        Action<TerminalShortcut> onChanged,
        bool isNew = false)
    {
        _shortcut = shortcut;
        _launch = launch;
        _onChanged = onChanged;
        _isNewLaunch = isNew;
        Id = PageId;
        Icon = new IconInfo(TerminalLaunchGlyphs.GetForLaunch(launch));
        Title = isNew ? "Add terminal" : $"Edit {launch.Label}";
        Name = isNew ? "Add" : "Edit";
    }

    public override IContent[] GetContent() =>
        [_form ??= new ShortcutLaunchForm(_shortcut, _launch, _onChanged, _isNewLaunch, () => _form = null)];

    private ShortcutLaunchForm? _form;
}

internal sealed partial class ShortcutLaunchForm : FormContent
{
    private readonly TerminalShortcut _shortcut;
    private readonly WorkspaceEntry _launch;
    private readonly Action<TerminalShortcut> _onChanged;
    private readonly bool _isNewLaunch;
    private readonly Action? _releaseForm;
    private FormDraft _draft = new();

    public ShortcutLaunchForm(
        TerminalShortcut shortcut,
        WorkspaceEntry launch,
        Action<TerminalShortcut> onChanged,
        bool isNewLaunch = false,
        Action? releaseForm = null)
    {
        _shortcut = shortcut;
        _launch = launch;
        _onChanged = onChanged;
        _isNewLaunch = isNewLaunch;
        _releaseForm = releaseForm;
        TemplateJson = BuildTemplateJson(FormTerminalChoicesJson());
        ApplyDraft();
    }

    public override CommandResult SubmitForm(string inputs, string data)
    {
        CaptureInputs(inputs);

        if (IsRefreshTerminalsAction(inputs, data))
        {
            return HandleRefreshTerminals();
        }

        if (IsCancelAction(inputs, data))
        {
            _releaseForm?.Invoke();
            return QuickShellNavigation.GoBack();
        }

        return HandleSave();
    }

    public override CommandResult SubmitForm(string payload)
    {
        CaptureInputs(payload);

        if (IsRefreshTerminalsAction(payload, null))
        {
            return HandleRefreshTerminals();
        }

        if (IsCancelAction(payload, null))
        {
            _releaseForm?.Invoke();
            return QuickShellNavigation.GoBack();
        }

        return HandleSave();
    }

    private CommandResult HandleRefreshTerminals()
    {
        TerminalCatalog.InvalidateCache();
        TemplateJson = BuildTemplateJson(FormTerminalChoicesJson());
        PublishDraftJson();
        return QuickShellNavigation.StayOpen("Terminal list refreshed.");
    }

    private CommandResult HandleSave()
    {
        var candidate = BuildCandidateLaunchFromDraft();
        if (!WorkspaceValidation.TryValidateEntry(candidate, out var error))
        {
            return QuickShellNavigation.StayOpen(error);
        }

        ShortcutLaunchNormalization.EnsureLaunchesFromLegacy(_shortcut);
        var duplicateLabel = _shortcut.Launches.Any(entry =>
            !entry.Id.Equals(candidate.Id, StringComparison.OrdinalIgnoreCase)
            && entry.Label.Equals(candidate.Label, StringComparison.OrdinalIgnoreCase));
        if (duplicateLabel)
        {
            return QuickShellNavigation.StayOpen($"Duplicate terminal label '{candidate.Label}'.");
        }

        ApplyCandidateToLaunch(candidate);

        if (_isNewLaunch
            && !_shortcut.Launches.Any(entry => entry.Id.Equals(_launch.Id, StringComparison.OrdinalIgnoreCase)))
        {
            _shortcut.Launches.Add(_launch);
        }

        ShortcutLaunchNormalization.NormalizeLaunchOrders(_shortcut);
        _onChanged(_shortcut);
        _releaseForm?.Invoke();
        return QuickShellNavigation.GoBack(_isNewLaunch ? $"Added '{_launch.Label}'." : $"Updated '{_launch.Label}'.");
    }

    private WorkspaceEntry BuildCandidateLaunchFromDraft()
    {
        var launchShortcut = new TerminalShortcut();
        TerminalCatalog.ApplyLaunchTargetId(launchShortcut, _draft.LaunchTarget);
        return new WorkspaceEntry
        {
            Id = _launch.Id,
            Label = _draft.Label.Trim(),
            Command = string.IsNullOrWhiteSpace(_draft.Command) ? null : _draft.Command.Trim(),
            Terminal = launchShortcut.Terminal,
            WtProfile = launchShortcut.WtProfile,
            RunAsAdmin = _draft.RunAsAdmin,
            IsEnabled = _draft.IsEnabled,
            Order = _launch.Order,
        };
    }

    private void ApplyCandidateToLaunch(WorkspaceEntry candidate)
    {
        _launch.Label = candidate.Label;
        _launch.Command = candidate.Command;
        _launch.Terminal = candidate.Terminal;
        _launch.WtProfile = candidate.WtProfile;
        _launch.RunAsAdmin = candidate.RunAsAdmin;
        _launch.IsEnabled = candidate.IsEnabled;
    }

    private void CaptureInputs(string payload)
    {
        _draft.Label = GetField(payload, "Label") ?? _draft.Label;
        _draft.Command = GetField(payload, "Command") ?? _draft.Command;
        _draft.LaunchTarget = GetField(payload, "LaunchTarget") ?? _draft.LaunchTarget;
        _draft.RunAsAdmin = ParseToggleBool(GetField(payload, "RunAsAdmin"), _draft.RunAsAdmin);
        _draft.IsEnabled = ParseToggleBool(GetField(payload, "IsEnabled"), _draft.IsEnabled);
    }

    private void ApplyDraft()
    {
        var launchTarget = TerminalCatalog.EncodeLaunchTargetId(new TerminalShortcut
        {
            Terminal = _launch.Terminal,
            WtProfile = _launch.WtProfile,
        });

        _draft = new FormDraft
        {
            Label = _launch.Label,
            Command = _launch.Command ?? string.Empty,
            LaunchTarget = launchTarget,
            RunAsAdmin = _launch.RunAsAdmin,
            IsEnabled = _launch.IsEnabled,
        };

        PublishDraftJson();
    }

    private void PublishDraftJson()
    {
        DataJson = $$"""
        {
          "Label": "{{EscapeJsonValue(_draft.Label)}}",
          "Command": "{{EscapeJsonValue(_draft.Command)}}",
          "LaunchTarget": "{{EscapeJsonValue(_draft.LaunchTarget)}}",
          "RunAsAdmin": "{{(_draft.RunAsAdmin ? "true" : "false")}}",
          "IsEnabled": "{{(_draft.IsEnabled ? "true" : "false")}}"
        }
        """;
    }

    private static string BuildTemplateJson(string terminalChoices) => $$"""
    {
      "$schema": "http://adaptivecards.io/schemas/adaptive-card.json",
      "type": "AdaptiveCard",
      "version": "1.6",
      "body": [
        {{SettingsCardJson.FieldGroup("Label", "Shown when this workspace has multiple terminals.", """
        {
          "type": "Input.Text",
          "id": "Label",
          "isRequired": true,
          "value": "${Label}"
        }
        """)}},
        {
          "type": "Container",
          "spacing": "Medium",
          "items": [
            {{SettingsCardJson.FieldLabel("Terminal profile")}},
            {
              "type": "Input.ChoiceSet",
              "id": "LaunchTarget",
              "style": "compact",
              "value": "${LaunchTarget}",
              "choices": {{terminalChoices}}
            },
            {
              "type": "ActionSet",
              "spacing": "Small",
              "actions": [
                {
                  "type": "Action.Submit",
                  "title": "Refresh profile list",
                  "data": { "action": "refreshTerminals" },
                  "associatedInputs": "auto"
                }
              ]
            }
          ]
        },
        {{SettingsCardJson.FieldGroup("Command (optional)", "Run after the terminal opens.", """
        {
          "type": "Input.Text",
          "id": "Command",
          "value": "${Command}"
        }
        """)}},
        {{SettingsCardJson.FieldGroup("Administrator", "", """
        {
          "type": "Input.Toggle",
          "id": "RunAsAdmin",
          "title": "Run as administrator",
          "value": "${RunAsAdmin}",
          "valueOn": "true",
          "valueOff": "false"
        }
        """)}},
        {{SettingsCardJson.FieldGroup("Enabled", "", """
        {
          "type": "Input.Toggle",
          "id": "IsEnabled",
          "title": "Include when opening workspace",
          "value": "${IsEnabled}",
          "valueOn": "true",
          "valueOff": "false"
        }
        """)}}
      ],
      "actions": [
        {
          "type": "Action.Submit",
          "title": "Apply",
          "associatedInputs": "auto"
        },
        {
          "type": "Action.Submit",
          "title": "Cancel",
          "data": { "action": "cancel" },
          "associatedInputs": "none"
        }
      ]
    }
    """;

    private static string FormTerminalChoicesJson() =>
        TerminalCatalog.BuildFormChoicesJson(
            includeDefaultChoice: true,
            QuickShellRuntimeServices.Settings?.TerminalApplicationId ?? TerminalHostIds.WindowsTerminal);

    private static bool IsRefreshTerminalsAction(string inputs, string? data) =>
        data?.Contains("refreshTerminals", StringComparison.Ordinal) == true
        || inputs.Contains("\"action\":\"refreshTerminals\"", StringComparison.Ordinal);

    private static bool IsCancelAction(string inputs, string? data) =>
        data?.Contains("cancel", StringComparison.Ordinal) == true
        || inputs.Contains("\"action\":\"cancel\"", StringComparison.Ordinal);

    private static string? GetField(string payload, string fieldName)
    {
        try
        {
            return JsonNode.Parse(payload)?[fieldName]?.GetValue<string>();
        }
        catch
        {
            return null;
        }
    }

    private static string EscapeJsonValue(string? value)
    {
        var encoded = JsonSerializer.Serialize(value ?? string.Empty, QuickShellJsonContext.Default.String);
        return encoded.Length >= 2 ? encoded[1..^1] : string.Empty;
    }

    private static bool ParseToggleBool(string? value, bool fallback) =>
        value switch
        {
            "true" => true,
            "false" => false,
            _ => fallback,
        };

    private sealed class FormDraft
    {
        public string Label { get; set; } = string.Empty;

        public string Command { get; set; } = string.Empty;

        public string LaunchTarget { get; set; } = "default";

        public bool RunAsAdmin { get; set; }

        public bool IsEnabled { get; set; } = true;
    }
}
