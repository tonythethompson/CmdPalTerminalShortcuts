using QuickShell.Models;

namespace QuickShell.Services;

internal static class ShortcutEditorState
{
    public static TerminalShortcut CreateNew() =>
        new()
        {
            Launches =
            [
                CreateLaunch("Main", null, 0),
            ],
        };

    public static TerminalShortcut CloneShortcut(TerminalShortcut shortcut)
    {
        ShortcutLaunchNormalization.EnsureLaunchesFromLegacy(shortcut);
        return new TerminalShortcut
        {
            Id = shortcut.Id,
            Name = shortcut.Name,
            Abbreviation = shortcut.Abbreviation,
            Directory = shortcut.Directory,
            Command = shortcut.Command,
            Terminal = shortcut.Terminal,
            WtProfile = shortcut.WtProfile,
            RunAsAdmin = shortcut.RunAsAdmin,
            IsPinned = shortcut.IsPinned,
            PinOrder = shortcut.PinOrder,
            LastUsedUtc = shortcut.LastUsedUtc,
            Launches = shortcut.Launches.Select(WorkspaceMapper.CloneEntry).ToList(),
        };
    }

    public static WorkspaceEntry CreateLaunch(string label, string? command, int order) => new()
    {
        Id = Guid.NewGuid().ToString("N"),
        Label = label,
        Command = command,
        Terminal = "default",
        IsEnabled = true,
        Order = order,
    };

    public static void MoveLaunch(TerminalShortcut shortcut, string launchId, int direction)
    {
        ShortcutLaunchNormalization.EnsureLaunchesFromLegacy(shortcut);
        var ordered = shortcut.Launches.OrderBy(entry => entry.Order).ToList();
        var index = ordered.FindIndex(entry => entry.Id.Equals(launchId, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
        {
            return;
        }

        var target = index + direction;
        if (target < 0 || target >= ordered.Count)
        {
            return;
        }

        (ordered[index], ordered[target]) = (ordered[target], ordered[index]);
        for (var i = 0; i < ordered.Count; i++)
        {
            ordered[i].Order = i;
        }

        shortcut.Launches = ordered;
    }
}
