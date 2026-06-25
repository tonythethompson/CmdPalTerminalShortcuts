using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using QuickShell.Services;
using System.Text.Json.Nodes;

namespace QuickShell.Pages;

internal sealed partial class ImportConflictPage : ContentPage
{
    public const string PageId = "com.quickshell.import-conflict";

    public ImportConflictPage(Action onReload)
    {
        Id = PageId;
        Icon = new IconInfo("\uE7BA");
        Title = "Finish importing";
        Name = "Import";
        _onReload = onReload;
    }

    private readonly Action _onReload;

    public override IContent[] GetContent() => [new ImportConflictForm(_onReload)];
}

internal sealed partial class ImportConflictForm : FormContent
{
    private readonly Action _onReload;

    public ImportConflictForm(Action onReload)
    {
        _onReload = onReload;

        TemplateJson = """
        {
          "$schema": "http://adaptivecards.io/schemas/adaptive-card.json",
          "type": "AdaptiveCard",
          "version": "1.6",
          "body": [
            {
              "type": "TextBlock",
              "text": "Duplicate shortcut names",
              "weight": "Bolder",
              "size": "Large"
            },
            {
              "type": "TextBlock",
              "text": "${Description}",
              "wrap": true,
              "spacing": "Small"
            },
            {
              "type": "TextBlock",
              "text": "${FileName}",
              "isSubtle": true,
              "spacing": "Medium"
            },
            {
              "type": "TextBlock",
              "text": "Choose an option below to complete the import.",
              "wrap": true,
              "weight": "Bolder",
              "spacing": "Large"
            }
          ],
          "actions": [
            {
              "type": "Action.Submit",
              "title": "Merge — rename duplicates",
              "data": { "action": "merge" },
              "associatedInputs": "none"
            },
            {
              "type": "Action.Submit",
              "title": "Replace all shortcuts",
              "data": { "action": "replace" },
              "associatedInputs": "none"
            },
            {
              "type": "Action.Submit",
              "title": "Cancel import",
              "data": { "action": "cancel" },
              "associatedInputs": "none"
            }
          ]
        }
        """;

        ApplyPendingState();
    }

    public override CommandResult SubmitForm(string inputs, string data) =>
        HandleSubmit(TryGetAction(data) ?? TryGetActionFromInputs(inputs));

    public override CommandResult SubmitForm(string payload) =>
        HandleSubmit(TryGetActionFromInputs(payload));

    private CommandResult HandleSubmit(string? action)
    {
        if (action == "cancel")
        {
            ImportConflictState.Clear();
            return QuickShellNavigation.GoBack("Import cancelled.");
        }

        var pending = ImportConflictState.Pending;
        if (pending is null)
        {
            return QuickShellNavigation.GoBack("No import is pending.");
        }

        var result = action switch
        {
            "merge" => ShortcutStore.ImportMerge(pending.Path),
            "replace" => ShortcutStore.ImportReplace(pending.Path),
            _ => null,
        };

        if (result is null)
        {
            return QuickShellNavigation.StayOpen("Unable to read form values.");
        }

        if (!result.Success)
        {
            return QuickShellNavigation.StayOpen(result.Message);
        }

        ImportConflictState.Clear();
        _onReload();
        return QuickShellNavigation.GoBack(result.Message);
    }

    private void ApplyPendingState()
    {
        var pending = ImportConflictState.Pending;
        if (pending is null)
        {
            DataJson = """
            {
              "Description": "No import is waiting for a decision.",
              "FileName": ""
            }
            """;
            return;
        }

        var fileName = Path.GetFileName(pending.Path);
        var conflictLabel = pending.ConflictCount == 1 ? "name" : "names";
        var importLabel = pending.ImportCount == 1 ? "shortcut" : "shortcuts";
        var description =
            $"The file contains {pending.ConflictCount} duplicate {conflictLabel} " +
            $"among {pending.ImportCount} {importLabel}.";

        DataJson = $$"""
        {
          "Description": "{{Escape(description)}}",
          "FileName": "{{Escape(fileName)}}"
        }
        """;
    }

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

    private static string Escape(string value) =>
        value.Replace("\\", "\\\\").Replace("\"", "\\\"");
}
