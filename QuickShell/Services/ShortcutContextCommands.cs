using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using QuickShell.Commands;
using QuickShell.Models;
using QuickShell.Pages;

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
            items.Add(new(new ShortcutFormPage(shortcut, onChanged)));
        }

        items.Add(new(new TogglePinShortcutCommand(shortcut.Name, onChanged, shortcut.IsPinned)));
        items.Add(new(new DuplicateShortcutCommand(shortcut.Name, onChanged)));

        if (shortcut.IsPinned)
        {
            items.Add(new(new MovePinnedShortcutCommand(shortcut.Name, -1, onChanged)));
            items.Add(new(new MovePinnedShortcutCommand(shortcut.Name, +1, onChanged)));
        }

        items.Add(new(new DeleteShortcutCommand(shortcut.Name, onChanged)));

        return items.ToArray();
    }
}
