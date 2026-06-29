using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using QuickShell.Models;
using QuickShell.Services;
using System.Text.Json.Nodes;

namespace QuickShell.Pages;

internal sealed partial class WorkspaceEntryFormPage : ContentPage
{
    public WorkspaceEntryFormPage(Workspace workspace, WorkspaceEntry entry, Action<Workspace> onChanged)
    {
        _workspace = workspace;
        _entry = entry;
        _onChanged = onChanged;
        Id = $"com.quickshell.workspace.entry-form.{Guid.NewGuid():N}";
        Icon = new IconInfo("\uE756");
        Title = $"Edit {entry.Label}";
        Name = "Edit";
    }

    private readonly Workspace _workspace;
    private readonly WorkspaceEntry _entry;
    private readonly Action<Workspace> _onChanged;

    public override IContent[] GetContent() =>
        [_form ??= new WorkspaceEntryForm(_workspace, _entry, _onChanged, () => _form = null)];

    private WorkspaceEntryForm? _form;
}

internal sealed partial class WorkspaceEntryForm : FormContent
{
    private readonly Workspace _workspace;
    private readonly WorkspaceEntry _entry;
    private readonly Action<Workspace> _onChanged;
    private readonly Action? _releaseForm;
    private FormDraft _draft = new();

    public WorkspaceEntryForm(
        Workspace workspace,
        WorkspaceEntry entry,
        Action<Workspace> onChanged,
        Action? releaseForm = null)
    {
        _workspace = workspace;
        _entry = entry;
        _onChanged = onChanged;
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
        ApplyDraft();
        return QuickShellNavigation.StayOpen("Terminal list refreshed.");
    }

    private CommandResult HandleSave()
    {
        _entry.Label = _draft.Label.Trim();
        _entry.Command = string.IsNullOrWhiteSpace(_draft.Command) ? null : _draft.Command.Trim();
        _entry.RunAsAdmin = _draft.RunAsAdmin;
        _entry.IsEnabled = _draft.IsEnabled;

        var launchShortcut = new TerminalShortcut();
        TerminalCatalog.ApplyLaunchTargetId(launchShortcut, _draft.LaunchTarget);
        _entry.Terminal = launchShortcut.Terminal;
        _entry.WtProfile = launchShortcut.WtProfile;

        if (!WorkspaceValidation.TryValidateEntry(_entry, out var error))
        {
            return QuickShellNavigation.StayOpen(error);
        }

        var duplicateLabel = _workspace.Entries.Any(entry =>
            !entry.Id.Equals(_entry.Id, StringComparison.OrdinalIgnoreCase)
            && entry.Label.Equals(_entry.Label, StringComparison.OrdinalIgnoreCase));
        if (duplicateLabel)
        {
            return QuickShellNavigation.StayOpen($"Duplicate launch label '{_entry.Label}'.");
        }

        _onChanged(_workspace);
        _releaseForm?.Invoke();
        return QuickShellNavigation.GoBack($"Updated '{_entry.Label}'.");
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
            Terminal = _entry.Terminal,
            WtProfile = _entry.WtProfile,
        });

        _draft = new FormDraft
        {
            Label = _entry.Label,
            Command = _entry.Command ?? string.Empty,
            LaunchTarget = launchTarget,
            RunAsAdmin = _entry.RunAsAdmin,
            IsEnabled = _entry.IsEnabled,
        };

        DataJson = $$"""
        {
          "Label": "{{Escape(_draft.Label)}}",
          "Command": "{{Escape(_draft.Command)}}",
          "LaunchTarget": "{{Escape(_draft.LaunchTarget)}}",
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
        {{SettingsCardJson.FieldGroup("Label", "", """
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
            {{SettingsCardJson.FieldLabel("Terminal application")}},
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
        {{SettingsCardJson.FieldGroup("Command (optional)", "", """
        {
          "type": "Input.Text",
          "id": "Command",
          "value": "${Command}"
        }
        """)}},
        {{SettingsCardJson.FieldGroup("Launch elevated", "", """
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
          "title": "Include when launching workspace",
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
            var node = JsonNode.Parse(payload);
            return node?[fieldName]?.GetValue<string>();
        }
        catch
        {
            return null;
        }
    }

    private static string Escape(string? value) => (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");

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
