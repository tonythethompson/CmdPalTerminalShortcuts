using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using QuickShell.Commands;
using QuickShell.Models;
using QuickShell.Pages;
using Windows.System;

namespace QuickShell.Services;

internal static class ShortcutContextCommands
{
    public static CommandContextItem[] Build(
        TerminalShortcut shortcut,
        Action onChanged,
        QuickShellSettingsManager settings,
        bool includeEdit = true)
    {
        var items = new List<CommandContextItem>();

        if (includeEdit)
        {
            var editPage = new ShortcutFormPage(shortcut, onChanged);
            items.Add(WithShortcut(
                editPage,
                ctrl: true,
                alt: false,
                shift: false,
                VirtualKey.E,
                title: editPage.Title));
        }

        AddElevationContextCommand(items, shortcut, settings);

        var pinCommand = new TogglePinShortcutCommand(shortcut.Name, onChanged, shortcut.IsPinned);
        items.Add(WithShortcut(
            pinCommand,
            ctrl: true,
            alt: false,
            shift: false,
            VirtualKey.P,
            title: pinCommand.Name));

        var duplicateCommand = new DuplicateShortcutCommand(shortcut.Name, onChanged);
        items.Add(WithShortcut(
            duplicateCommand,
            ctrl: true,
            alt: false,
            shift: true,
            VirtualKey.D,
            title: duplicateCommand.Name));

        var undoCommand = new UndoShortcutCommand(onChanged);
        items.Add(WithShortcut(
            undoCommand,
            ctrl: true,
            alt: false,
            shift: false,
            VirtualKey.Z,
            title: undoCommand.Name));

        var redoCommand = new RedoShortcutCommand(onChanged);
        items.Add(WithShortcut(
            redoCommand,
            ctrl: true,
            alt: false,
            shift: false,
            VirtualKey.Y,
            title: redoCommand.Name));

        if (shortcut.IsPinned)
        {
            var moveUpCommand = new MovePinnedShortcutCommand(shortcut.Name, -1, onChanged);
            items.Add(WithShortcut(
                moveUpCommand,
                ctrl: true,
                alt: true,
                shift: false,
                VirtualKey.Up,
                title: moveUpCommand.Name));

            var moveDownCommand = new MovePinnedShortcutCommand(shortcut.Name, +1, onChanged);
            items.Add(WithShortcut(
                moveDownCommand,
                ctrl: true,
                alt: true,
                shift: false,
                VirtualKey.Down,
                title: moveDownCommand.Name));
        }

        var deleteCommand = new DeleteShortcutCommand(shortcut.Name, onChanged);
        items.Add(WithShortcut(
            deleteCommand,
            ctrl: true,
            alt: false,
            shift: false,
            VirtualKey.Delete,
            title: deleteCommand.Name,
            isCritical: true));

        return items.ToArray();
    }

    public static CommandContextItem[] BuildForHomePin(
        TerminalShortcut shortcut,
        Action onChanged,
        QuickShellSettingsManager settings)
    {
        var items = new List<CommandContextItem>();

        var editPage = new ShortcutFormPage(shortcut, onChanged);
        items.Add(new CommandContextItem(editPage)
        {
            Title = editPage.Title,
            RequestedShortcut = KeyChordHelpers.FromModifiers(
                ctrl: true,
                alt: false,
                shift: false,
                win: false,
                vkey: VirtualKey.E),
        });

        if (!shortcut.RunAsAdmin)
        {
            var adminCommand = new OpenTerminalShortcutCommand(shortcut, settings, runAsAdmin: true);
            items.Add(CreateOpenAsAdminContextItem(adminCommand));
        }
        else
        {
            var standardCommand = new OpenTerminalShortcutCommand(shortcut, settings, runAsStandard: true);
            items.Add(CreateOpenWithoutAdminContextItem(standardCommand));
        }

        return items.ToArray();
    }

    public static void AddElevationContextCommand(
        IList<CommandContextItem> items,
        TerminalShortcut shortcut,
        QuickShellSettingsManager settings,
        bool insertAtStart = true)
    {
        CommandContextItem contextItem;
        if (shortcut.RunAsAdmin)
        {
            var standardCommand = new OpenTerminalShortcutCommand(shortcut, settings, runAsStandard: true);
            contextItem = CreateOpenWithoutAdminContextItem(standardCommand);
        }
        else
        {
            var adminCommand = new OpenTerminalShortcutCommand(shortcut, settings, runAsAdmin: true);
            contextItem = CreateOpenAsAdminContextItem(adminCommand);
        }

        if (insertAtStart)
        {
            items.Insert(0, contextItem);
        }
        else
        {
            items.Add(contextItem);
        }
    }

    public static CommandContextItem CreateOpenAsAdminContextItem(OpenTerminalShortcutCommand command) =>
        new(command)
        {
            Title = "Open as administrator",
            RequestedShortcut = KeyChordHelpers.FromModifiers(
                ctrl: true,
                alt: false,
                shift: false,
                win: false,
                vkey: VirtualKey.Enter),
        };

    public static CommandContextItem CreateOpenWithoutAdminContextItem(OpenTerminalShortcutCommand command) =>
        new(command)
        {
            Title = "Open without administrator",
            RequestedShortcut = KeyChordHelpers.FromModifiers(
                ctrl: true,
                alt: false,
                shift: true,
                win: false,
                vkey: VirtualKey.Enter),
        };

    private static CommandContextItem WithShortcut(
        ICommand command,
        bool ctrl,
        bool alt,
        bool shift,
        VirtualKey key,
        string title,
        bool isCritical = false) =>
        new(command)
        {
            Title = title,
            IsCritical = isCritical,
            RequestedShortcut = KeyChordHelpers.FromModifiers(ctrl, alt, shift, win: false, vkey: key),
        };
}
