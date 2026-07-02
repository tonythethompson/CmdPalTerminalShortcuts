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
        if (TryGetProfileIcon(launch, out var profileIcon))
        {
            return profileIcon;
        }

        return GetFallbackGlyph(launch);
    }

    private static bool TryGetProfileIcon(WorkspaceEntry launch, out string icon)
    {
        icon = string.Empty;

        var profile = TerminalProfileResolver.ResolveForLaunch(launch);
        if (profile is null)
        {
            return false;
        }

        var resolved = TerminalProfileIconResolver.ResolveEffectiveIcon(profile);
        if (string.IsNullOrWhiteSpace(resolved))
        {
            return false;
        }

        icon = resolved;
        return true;
    }

    private static string GetFallbackGlyph(WorkspaceEntry launch)
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
