using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using QuickShell.Commands;
using QuickShell.Models;
using QuickShell.Pages;
using Windows.System;

namespace QuickShell.Services;

internal static class WorkspaceContextCommands
{
    public static CommandContextItem[] Build(
        Workspace workspace,
        Action onChanged,
        QuickShellSettingsManager settings,
        CreateShortcutCommand? createShortcutCommand = null)
    {
        var items = new List<CommandContextItem>();

        var enabledEntries = workspace.Entries
            .Where(entry => entry.IsEnabled)
            .OrderBy(entry => entry.Order)
            .ToList();

        if (enabledEntries.Count > 0)
        {
            foreach (var entry in enabledEntries)
            {
                items.Add(new CommandContextItem(new OpenWorkspaceEntryCommand(workspace, entry, settings))
                {
                    Title = $"Open {entry.Label}",
                    Icon = new IconInfo("\uE756"),
                });
            }
        }

        items.Add(new CommandContextItem(new EditWorkspaceCommand(workspace.Name, onChanged))
        {
            Title = "Edit workspace",
            Icon = new IconInfo("\uE70F"),
            RequestedShortcut = KeyChordHelpers.FromModifiers(ctrl: true, alt: false, shift: false, win: false, vkey: VirtualKey.E),
        });

        items.Add(new CommandContextItem(new DuplicateWorkspaceCommand(workspace.Name, onChanged))
        {
            Title = "Duplicate",
            Icon = new IconInfo("\uE8C8"),
            RequestedShortcut = KeyChordHelpers.FromModifiers(ctrl: true, alt: false, shift: true, win: false, vkey: VirtualKey.D),
        });

        items.Add(new CommandContextItem(new ToggleFavoriteWorkspaceCommand(workspace.Name, onChanged, workspace.IsPinned))
        {
            Title = workspace.IsPinned ? "Unfavorite" : "Favorite",
            Icon = new IconInfo(workspace.IsPinned ? ShortcutGlyphs.FavoriteFilled : ShortcutGlyphs.FavoriteOutline),
            RequestedShortcut = KeyChordHelpers.FromModifiers(ctrl: true, alt: false, shift: false, win: false, vkey: VirtualKey.F),
        });

        if (createShortcutCommand is not null)
        {
            items.Add(new CommandContextItem(createShortcutCommand)
            {
                Title = "Create shortcut",
                Icon = new IconInfo("\uE710"),
            });
        }

        items.Add(ShortcutContextCommands.CreateSettingsItem(settings));

        items.Add(new CommandContextItem(new DeleteWorkspaceCommand(workspace.Name, onChanged))
        {
            Title = "Delete",
            Icon = new IconInfo("\uE74D"),
            IsCritical = true,
            RequestedShortcut = KeyChordHelpers.FromModifiers(ctrl: true, alt: false, shift: false, win: false, vkey: VirtualKey.Delete),
        });

        return items.ToArray();
    }

    public static CommandContextItem[] BuildForShortcut(
        TerminalShortcut shortcut,
        Action onChanged,
        QuickShellSettingsManager settings)
    {
        var workspaces = QuickShellRuntimeServices.Workspaces.GetByDirectory(shortcut.Directory);
        if (workspaces.Count == 0)
        {
            return
            [
                new CommandContextItem(new CreateWorkspaceFromShortcutCommand(shortcut.Id, onChanged))
                {
                    Title = "Create workspace from this shortcut",
                    Icon = new IconInfo(WorkspaceListItems.WorkspaceIcon),
                },
            ];
        }

        var items = new List<CommandContextItem>();
        foreach (var workspace in workspaces)
        {
            items.Add(new CommandContextItem(new OpenWorkspaceCommand(workspace, settings))
            {
                Title = $"Open workspace — {workspace.Name}",
                Icon = new IconInfo(WorkspaceListItems.WorkspaceIcon),
            });
        }

        items.Add(new CommandContextItem(new CreateWorkspaceFromShortcutCommand(shortcut.Id, onChanged))
        {
            Title = "Create workspace from this shortcut",
            Icon = new IconInfo(WorkspaceListItems.WorkspaceIcon),
        });

        return items.ToArray();
    }
}
