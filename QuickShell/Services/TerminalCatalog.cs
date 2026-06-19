using System.Diagnostics;
using System.Text.Json;
using Microsoft.CommandPalette.Extensions.Toolkit;
using QuickShell.Models;

namespace QuickShell.Services;

internal enum LaunchTargetKind
{
    Default,
    WindowsTerminal,
    PowerShell,
    Pwsh,
    Cmd,
    Wsl,
}

internal sealed class LaunchTarget
{
    public required string Id { get; init; }

    public required string DisplayName { get; init; }

    public required LaunchTargetKind Kind { get; init; }

    public string? ProfileOrDistro { get; init; }

    public string? WtCommandLine { get; init; }
}

internal static class TerminalCatalog
{
    private static readonly object Sync = new();
    private static IReadOnlyList<LaunchTarget>? _cached;
    private static Dictionary<string, LaunchTarget>? _byId;
    private static ExecutableAvailability? _executables;
    private static string? _cachedFormChoicesJson;
    private static bool _cachedFormChoicesIncludeDefault;

    public static IReadOnlyList<LaunchTarget> GetLaunchTargets(bool includeDefaultChoice = false)
    {
        EnsureCached();

        if (!includeDefaultChoice)
        {
            return _cached!;
        }

        return
        [
            new LaunchTarget
            {
                Id = "default",
                DisplayName = "Default (from settings)",
                Kind = LaunchTargetKind.Default,
            },
            .. _cached!,
        ];
    }

    public static void InvalidateCache()
    {
        lock (Sync)
        {
            _cached = null;
            _byId = null;
            _executables = null;
            _cachedFormChoicesJson = null;
        }

        WtProfilesService.InvalidateCache();
    }

    public static List<ChoiceSetSetting.Choice> GetSettingsChoices()
    {
        return GetLaunchTargets()
            .Select(t => new ChoiceSetSetting.Choice(t.DisplayName, t.Id))
            .ToList();
    }

    public static string GetDisplayName(TerminalShortcut shortcut)
    {
        var id = EncodeLaunchTargetId(shortcut);
        if (id.Equals("default", StringComparison.OrdinalIgnoreCase))
        {
            return "Default";
        }

        EnsureCached();
        if (_byId!.TryGetValue(id, out var target))
        {
            return target.DisplayName;
        }

        return FormatFallback(shortcut);
    }

    public static string EncodeLaunchTargetId(TerminalShortcut shortcut)
    {
        var terminal = (shortcut.Terminal ?? "default").Trim().ToLowerInvariant();
        return terminal switch
        {
            "default" => "default",
            "wt" => string.IsNullOrWhiteSpace(shortcut.WtProfile) ? "wt" : $"wt:{shortcut.WtProfile}",
            "wsl" => string.IsNullOrWhiteSpace(shortcut.WtProfile) ? "wsl" : $"wsl:{shortcut.WtProfile}",
            "powershell" => "powershell",
            "pwsh" or "powershell7" => "pwsh",
            "cmd" => "cmd",
            _ => "default",
        };
    }

    public static void ApplyLaunchTargetId(TerminalShortcut shortcut, string? launchTargetId)
    {
        var id = (launchTargetId ?? "default").Trim();
        if (id.Equals("default", StringComparison.OrdinalIgnoreCase))
        {
            shortcut.Terminal = "default";
            shortcut.WtProfile = null;
            return;
        }

        if (id.Equals("wt", StringComparison.OrdinalIgnoreCase))
        {
            shortcut.Terminal = "wt";
            shortcut.WtProfile = null;
            return;
        }

        if (id.StartsWith("wt:", StringComparison.OrdinalIgnoreCase))
        {
            shortcut.Terminal = "wt";
            shortcut.WtProfile = id[3..];
            return;
        }

        if (id.StartsWith("wsl:", StringComparison.OrdinalIgnoreCase))
        {
            shortcut.Terminal = "wsl";
            shortcut.WtProfile = id[4..];
            return;
        }

        shortcut.Terminal = id.ToLowerInvariant() switch
        {
            "powershell7" => "pwsh",
            _ => id.ToLowerInvariant(),
        };
        shortcut.WtProfile = null;
    }

    public static LaunchTarget Resolve(string? launchTargetId)
    {
        var id = NormalizeLaunchTargetId(launchTargetId);
        if (id.Equals("default", StringComparison.OrdinalIgnoreCase))
        {
            id = "wt";
        }

        EnsureCached();
        if (_byId!.TryGetValue(id, out var target))
        {
            return target;
        }

        return _byId.TryGetValue("wt", out var fallback)
            ? fallback
            : new LaunchTarget
            {
                Id = "wt",
                DisplayName = "Windows Terminal",
                Kind = LaunchTargetKind.WindowsTerminal,
            };
    }

    public static LaunchTarget ResolveForShortcut(TerminalShortcut shortcut, string defaultLaunchTargetId)
    {
        var id = EncodeLaunchTargetId(shortcut);
        if (id.Equals("default", StringComparison.OrdinalIgnoreCase))
        {
            id = NormalizeLaunchTargetId(defaultLaunchTargetId);
        }

        return Resolve(id);
    }

    public static string NormalizeLaunchTargetId(string? launchTargetId)
    {
        var value = (launchTargetId ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            return "wt";
        }

        if (value.Equals("default", StringComparison.OrdinalIgnoreCase))
        {
            return "default";
        }

        if (value.Equals("windows-terminal", StringComparison.OrdinalIgnoreCase))
        {
            return "wt";
        }

        if (value.Equals("powershell7", StringComparison.OrdinalIgnoreCase))
        {
            return "pwsh";
        }

        if (value.StartsWith("wt:", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("wsl:", StringComparison.OrdinalIgnoreCase)
            || value is "wt" or "powershell" or "pwsh" or "cmd")
        {
            return value;
        }

        return value.ToLowerInvariant() switch
        {
            "powershell" => "powershell",
            "pwsh" => "pwsh",
            "cmd" => "cmd",
            _ => "wt",
        };
    }

    public static string BuildFormChoicesJson(bool includeDefaultChoice)
    {
        lock (Sync)
        {
            if (_cachedFormChoicesJson is not null && _cachedFormChoicesIncludeDefault == includeDefaultChoice)
            {
                return _cachedFormChoicesJson;
            }
        }

        var choices = GetLaunchTargets(includeDefaultChoice)
            .Select(t => $"{{ \"title\": \"{EscapeJson(t.DisplayName)}\", \"value\": \"{EscapeJson(t.Id)}\" }}");

        var json = "[" + string.Join(',', choices) + "]";
        lock (Sync)
        {
            _cachedFormChoicesIncludeDefault = includeDefaultChoice;
            _cachedFormChoicesJson = json;
            return _cachedFormChoicesJson;
        }
    }

    private static void EnsureCached()
    {
        lock (Sync)
        {
            if (_cached is not null)
            {
                return;
            }

            _executables ??= ExecutableAvailability.Discover();
            _cached = DiscoverLaunchTargets(_executables);
            _byId = _cached.ToDictionary(t => t.Id, StringComparer.OrdinalIgnoreCase);
        }
    }

    private static List<LaunchTarget> DiscoverLaunchTargets(ExecutableAvailability executables)
    {
        var targets = new List<LaunchTarget>();

        if (executables.WindowsTerminal)
        {
            var profiles = WtProfilesService.GetProfiles();
            if (profiles.Count > 0)
            {
                targets.Add(new LaunchTarget
                {
                    Id = "wt",
                    DisplayName = "Windows Terminal (default profile)",
                    Kind = LaunchTargetKind.WindowsTerminal,
                });

                foreach (var profile in profiles)
                {
                    targets.Add(new LaunchTarget
                    {
                        Id = $"wt:{profile.Name}",
                        DisplayName = profile.Name,
                        Kind = LaunchTargetKind.WindowsTerminal,
                        ProfileOrDistro = profile.Name,
                        WtCommandLine = profile.Commandline,
                    });
                }
            }
            else
            {
                targets.Add(new LaunchTarget
                {
                    Id = "wt",
                    DisplayName = "Windows Terminal",
                    Kind = LaunchTargetKind.WindowsTerminal,
                });
            }
        }

        if (executables.PowerShell)
        {
            targets.Add(new LaunchTarget
            {
                Id = "powershell",
                DisplayName = "PowerShell",
                Kind = LaunchTargetKind.PowerShell,
            });
        }

        if (executables.Pwsh)
        {
            targets.Add(new LaunchTarget
            {
                Id = "pwsh",
                DisplayName = "PowerShell 7",
                Kind = LaunchTargetKind.Pwsh,
            });
        }

        if (executables.Cmd)
        {
            targets.Add(new LaunchTarget
            {
                Id = "cmd",
                DisplayName = "Command Prompt",
                Kind = LaunchTargetKind.Cmd,
            });
        }

        if (!executables.WindowsTerminal)
        {
            foreach (var distro in executables.WslDistros)
            {
                targets.Add(new LaunchTarget
                {
                    Id = $"wsl:{distro}",
                    DisplayName = $"WSL — {distro}",
                    Kind = LaunchTargetKind.Wsl,
                    ProfileOrDistro = distro,
                });
            }
        }

        if (targets.Count == 0)
        {
            targets.Add(new LaunchTarget
            {
                Id = "cmd",
                DisplayName = "Command Prompt",
                Kind = LaunchTargetKind.Cmd,
            });
        }

        return targets;
    }

    private static string FormatFallback(TerminalShortcut shortcut)
    {
        var terminal = (shortcut.Terminal ?? "default").Trim();
        if (!string.IsNullOrWhiteSpace(shortcut.WtProfile))
        {
            return $"{terminal} — {shortcut.WtProfile}";
        }

        return terminal;
    }

    private static string EscapeJson(string value) =>
        value.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private sealed class ExecutableAvailability
    {
        public bool WindowsTerminal { get; init; }

        public bool PowerShell { get; init; }

        public bool Pwsh { get; init; }

        public bool Cmd { get; init; }

        public string[] WslDistros { get; init; } = [];

        public static ExecutableAvailability Discover()
        {
            var wt = IsOnPath("wt.exe");
            return new ExecutableAvailability
            {
                WindowsTerminal = wt,
                PowerShell = IsOnPath("powershell.exe"),
                Pwsh = IsOnPath("pwsh.exe"),
                Cmd = IsOnPath("cmd.exe"),
                WslDistros = wt ? [] : GetWslDistros(),
            };
        }

        private static bool IsOnPath(string fileName)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "where.exe",
                    Arguments = fileName,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };

                using var process = Process.Start(startInfo);
                if (process is null)
                {
                    return false;
                }

                if (!process.WaitForExit(1500))
                {
                    try
                    {
                        process.Kill(entireProcessTree: true);
                    }
                    catch
                    {
                        // Best effort.
                    }

                    return false;
                }

                return process.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        private static string[] GetWslDistros()
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "wsl.exe",
                    Arguments = "-l -q",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };

                using var process = Process.Start(startInfo);
                if (process is null)
                {
                    return [];
                }

                var output = process.StandardOutput.ReadToEnd();
                if (!process.WaitForExit(3000))
                {
                    try
                    {
                        process.Kill(entireProcessTree: true);
                    }
                    catch
                    {
                        // Best effort.
                    }

                    return [];
                }

                if (process.ExitCode != 0)
                {
                    return [];
                }

                return output
                    .Replace("\0", string.Empty)
                    .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                    .Select(line => line.Trim())
                    .Where(line => line.Length > 0)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(line => line, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }
            catch
            {
                return [];
            }
        }
    }
}
