using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using QuickShell.Models;
using QuickShell.Services;
using System.Text.Json.Nodes;

namespace QuickShell.Pages;

internal sealed partial class WorkspaceDetailsFormPage : ContentPage
{
    public WorkspaceDetailsFormPage(Workspace workspace, Action onChanged)
    {
        Id = $"com.quickshell.workspace.details.{Guid.NewGuid():N}";
        Icon = new IconInfo("\uE70F");
        Title = "Workspace details";
        Name = "Edit";
        _workspace = workspace;
        _onChanged = onChanged;
    }

    private readonly Workspace _workspace;
    private readonly Action _onChanged;

    public override IContent[] GetContent() => [_form ??= new WorkspaceDetailsForm(_workspace, _onChanged, () => _form = null)];

    private WorkspaceDetailsForm? _form;
}

internal sealed partial class WorkspaceDetailsForm : FormContent
{
    private readonly Workspace _workspace;
    private readonly Action _onChanged;
    private readonly Action? _releaseForm;
    private FormDraft _draft = new();

    public WorkspaceDetailsForm(Workspace workspace, Action onChanged, Action? releaseForm = null)
    {
        _workspace = workspace;
        _onChanged = onChanged;
        _releaseForm = releaseForm;
        TemplateJson = BuildTemplateJson();
        ApplyDraft();
    }

    public override CommandResult SubmitForm(string inputs, string data)
    {
        CaptureInputs(inputs);

        if (IsBrowseAction(inputs, data))
        {
            return HandleBrowse(inputs);
        }

        if (IsPasteAction(inputs, data))
        {
            return HandlePaste(inputs);
        }

        if (IsChooseShortcutAction(inputs, data))
        {
            return HandleChooseShortcut();
        }

        if (IsCancelAction(inputs, data))
        {
            _releaseForm?.Invoke();
            return QuickShellNavigation.GoBack();
        }

        return HandleSave(inputs);
    }

    public override CommandResult SubmitForm(string payload)
    {
        CaptureInputs(payload);

        if (IsBrowseAction(payload, null))
        {
            return HandleBrowse(payload);
        }

        if (IsPasteAction(payload, null))
        {
            return HandlePaste(payload);
        }

        if (IsChooseShortcutAction(payload, null))
        {
            return HandleChooseShortcut();
        }

        if (IsCancelAction(payload, null))
        {
            _releaseForm?.Invoke();
            return QuickShellNavigation.GoBack();
        }

        return HandleSave(payload);
    }

    private CommandResult HandleBrowse(string inputs)
    {
        var initialDirectory = GetField(inputs, "Directory") ?? _draft.Directory;
        MergeDraftFromInputs(inputs, excludeDirectory: true);

        var selected = FolderPickerService.PickFolder(
            string.IsNullOrWhiteSpace(initialDirectory) ? null : initialDirectory);
        if (selected is null)
        {
            return CommandResult.KeepOpen();
        }

        return ApplyDirectorySelection(selected);
    }

    private CommandResult HandlePaste(string inputs)
    {
        MergeDraftFromInputs(inputs, excludeDirectory: true);

        if (!TryReadClipboardFolderPath(out var pasted, out var error))
        {
            return QuickShellNavigation.StayOpen(error);
        }

        return ApplyDirectorySelection(pasted);
    }

    private CommandResult HandleChooseShortcut()
    {
        WorkspaceNavigationState.SetPicker(_onChanged, forCreate: false, changeDirectory: true);
        WorkspaceNavigationState.SetEditor(_workspace, _workspace.Name, _onChanged);
        return CommandResult.GoToPage(new GoToPageArgs
        {
            PageId = ProjectShortcutPickerPage.PageId,
        });
    }

    private CommandResult ApplyDirectorySelection(string directory)
    {
        if (!WorkspacePath.TryNormalizeLexical(directory, out var normalized, out var error))
        {
            return QuickShellNavigation.StayOpen(error);
        }

        _draft.Directory = normalized;
        ApplyDraft();
        return QuickShellNavigation.StayOpen();
    }

    private CommandResult HandleSave(string inputs)
    {
        MergeDraftFromInputs(inputs);

        if (!string.IsNullOrWhiteSpace(_draft.Directory)
            && !WorkspacePath.TryNormalizeLexical(_draft.Directory, out var normalized, out var directoryError))
        {
            return QuickShellNavigation.StayOpen(directoryError);
        }

        _workspace.Name = _draft.Name.Trim();
        _workspace.Abbreviation = string.IsNullOrWhiteSpace(_draft.Abbreviation) ? null : _draft.Abbreviation.Trim();
        _workspace.Directory = string.IsNullOrWhiteSpace(_draft.Directory)
            ? string.Empty
            : WorkspacePath.TryNormalizeLexical(_draft.Directory, out var savedDirectory, out _)
                ? savedDirectory
                : _draft.Directory.Trim();

        _onChanged();
        _releaseForm?.Invoke();
        return QuickShellNavigation.GoBack("Workspace details updated.");
    }

    private void CaptureInputs(string payload)
    {
        _draft.Name = GetField(payload, "Name") ?? _draft.Name;
        _draft.Abbreviation = GetField(payload, "Abbreviation") ?? _draft.Abbreviation;
        _draft.Directory = GetField(payload, "Directory") ?? _draft.Directory;
    }

    private void MergeDraftFromInputs(string payload, bool excludeDirectory = false)
    {
        _draft.Name = GetField(payload, "Name") ?? _draft.Name;
        _draft.Abbreviation = GetField(payload, "Abbreviation") ?? _draft.Abbreviation;
        if (!excludeDirectory)
        {
            _draft.Directory = GetField(payload, "Directory") ?? _draft.Directory;
        }
    }

    private void ApplyDraft()
    {
        _draft.Name = _workspace.Name;
        _draft.Abbreviation = _workspace.Abbreviation ?? string.Empty;
        _draft.Directory = _workspace.Directory;

        DataJson = $$"""
        {
          "Name": "{{Escape(_draft.Name)}}",
          "Abbreviation": "{{Escape(_draft.Abbreviation)}}",
          "Directory": "{{Escape(_draft.Directory)}}"
        }
        """;
    }

    private static string BuildTemplateJson() => $$"""
    {
      "$schema": "http://adaptivecards.io/schemas/adaptive-card.json",
      "type": "AdaptiveCard",
      "version": "1.6",
      "body": [
        {{SettingsCardJson.FieldGroup("Workspace name", "", """
        {
          "type": "Input.Text",
          "id": "Name",
          "isRequired": true,
          "value": "${Name}"
        }
        """)}},
        {{SettingsCardJson.FieldGroup("Home keyword (optional)", "Type this at Command Palette home to launch this workspace.", """
        {
          "type": "Input.Text",
          "id": "Abbreviation",
          "placeholder": "e.g. td-agents",
          "value": "${Abbreviation}"
        }
        """)}},
        {
          "type": "Container",
          "spacing": "Medium",
          "items": [
            {{SettingsCardJson.FieldLabel("Project folder")}},
            {{SettingsCardJson.FieldHelp("Absolute folder path used when launching this workspace. Browse, paste, or copy from a saved shortcut.")}},
            {
              "type": "Input.Text",
              "id": "Directory",
              "placeholder": "e.g. C:\\Projects\\MyApp",
              "value": "${Directory}"
            },
            {
              "type": "ActionSet",
              "spacing": "Small",
              "actions": [
                {
                  "type": "Action.Submit",
                  "title": "Browse folder",
                  "data": { "action": "browse" },
                  "associatedInputs": "none"
                },
                {
                  "type": "Action.Submit",
                  "title": "Paste path",
                  "data": { "action": "paste" },
                  "associatedInputs": "none"
                },
                {
                  "type": "Action.Submit",
                  "title": "Choose from shortcut",
                  "data": { "action": "chooseShortcut" },
                  "associatedInputs": "none"
                }
              ]
            }
          ]
        }
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

    private static bool IsBrowseAction(string inputs, string? data) =>
        TryGetAction(data) == "browse" || TryGetActionFromInputs(inputs) == "browse";

    private static bool IsPasteAction(string inputs, string? data) =>
        TryGetAction(data) == "paste" || TryGetActionFromInputs(inputs) == "paste";

    private static bool IsChooseShortcutAction(string inputs, string? data) =>
        TryGetAction(data) == "chooseShortcut" || TryGetActionFromInputs(inputs) == "chooseShortcut";

    private static bool IsCancelAction(string inputs, string? data) =>
        data?.Contains("cancel", StringComparison.Ordinal) == true
        || TryGetActionFromInputs(inputs) == "cancel";

    private static string? TryGetActionFromInputs(string inputs) =>
        JsonNode.Parse(inputs)?.AsObject()?["action"]?.ToString();

    private static string? TryGetAction(string? data)
    {
        if (string.IsNullOrWhiteSpace(data))
        {
            return null;
        }

        return JsonNode.Parse(data)?.AsObject()?["action"]?.ToString();
    }

    private static bool TryReadClipboardFolderPath(out string path, out string error)
    {
        path = string.Empty;
        error = string.Empty;

        var raw = StaClipboard.TryReadText()?.Trim();
        if (string.IsNullOrWhiteSpace(raw))
        {
            error = "Clipboard does not contain text to paste.";
            return false;
        }

        raw = UnwrapQuotedPath(raw);

        if (!WorkspacePath.TryNormalizeLexical(raw, out var normalized, out var validationError))
        {
            error = validationError;
            return false;
        }

        path = normalized;
        return true;
    }

    private static string UnwrapQuotedPath(string value)
    {
        if (value.Length >= 2
            && ((value.StartsWith('"') && value.EndsWith('"'))
                || (value.StartsWith('\'') && value.EndsWith('\''))))
        {
            return value[1..^1].Trim();
        }

        return value;
    }

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

    private sealed class FormDraft
    {
        public string Name { get; set; } = string.Empty;

        public string Abbreviation { get; set; } = string.Empty;

        public string Directory { get; set; } = string.Empty;
    }
}
