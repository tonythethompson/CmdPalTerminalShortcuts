using QuickShell.Models;

namespace QuickShell.Services;

internal static class TerminalLaunchGlyphs
{
    public static string GetForShortcut(TerminalShortcut shortcut)
    {
        ShortcutLaunchNormalization.EnsureLaunchesFromLegacy(shortcut);
        var launches = ShortcutLaunchNormalization.GetEnabledLaunches(shortcut);
        return launches.Count == 0 ? ShortcutGlyphs.NewWindow : GetForLaunch(launches[0]);
    }

    public static string GetForLaunch(WorkspaceEntry launch)
    {
        var terminal = (launch.Terminal ?? "default").Trim().ToLowerInvariant();
        if (IsLinuxTarget(terminal, launch.WtProfile))
        {
            return ShortcutGlyphs.Linux;
        }

        return terminal switch
        {
            "pwsh" or "powershell7" => ShortcutGlyphs.PowerShell,
            "powershell" => ShortcutGlyphs.PowerShell,
            "cmd" => ShortcutGlyphs.CommandPrompt,
            "wt" or "it" or "windows-terminal" or "intelligent-terminal" => ShortcutGlyphs.Terminal,
            "wsl" => ShortcutGlyphs.Linux,
            _ => ShortcutGlyphs.NewWindow,
        };
    }

    private static bool IsLinuxTarget(string terminal, string? profile)
    {
        if (terminal.Equals("wsl", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(profile))
        {
            return false;
        }

        return profile.Contains("ubuntu", StringComparison.OrdinalIgnoreCase)
            || profile.Contains("debian", StringComparison.OrdinalIgnoreCase)
            || profile.Contains("fedora", StringComparison.OrdinalIgnoreCase)
            || profile.Contains("linux", StringComparison.OrdinalIgnoreCase)
            || profile.Contains("wsl", StringComparison.OrdinalIgnoreCase);
    }
}
