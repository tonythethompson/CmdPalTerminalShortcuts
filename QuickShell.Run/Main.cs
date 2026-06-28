using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using ManagedCommon;
using QuickShell.Models;
using QuickShell.Services;
using Wox.Plugin;

namespace QuickShell.Run;

public class Main : IPlugin, IContextMenu
{
    public const string PluginIdValue = "a7c3e891-4b2d-4f6e-9c1a-2d8e5f03b4c6";

    public static string PluginID => PluginIdValue;

    static Main()
    {
        var pluginDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        if (string.IsNullOrEmpty(pluginDir))
        {
            return;
        }

        AssemblyLoadContext.Default.Resolving += (_, assemblyName) =>
        {
            var candidate = Path.Combine(pluginDir, $"{assemblyName.Name}.dll");
            return File.Exists(candidate)
                ? AssemblyLoadContext.Default.LoadFromAssemblyPath(candidate)
                : null;
        };
    }

    private PluginInitContext? _context;
    private string _iconPath = string.Empty;
    private ShortcutRepository? _shortcuts;
    private QuickShellSettingsReader? _settings;

    public string Name => "Quick Shell";

    public string Description => "Open saved folders in any terminal you use";

    public void Init(PluginInitContext context)
    {
        _shortcuts = new ShortcutRepository();
        _settings = new QuickShellSettingsReader();
        _context = context;
        UpdateIconPath(context.API.GetCurrentTheme());
        context.API.ThemeChanged += OnThemeChanged;
        _shortcuts.Reload();
    }

    private ShortcutRepository Shortcuts =>
        _shortcuts ?? throw new InvalidOperationException("Quick Shell plugin is not initialized.");

    private QuickShellSettingsReader Settings =>
        _settings ?? throw new InvalidOperationException("Quick Shell plugin is not initialized.");

    public List<Result> Query(Query query)
    {
        var search = query.Search?.Trim() ?? string.Empty;

        if (ShouldSuppressGlobalQuery(query, search))
        {
            return [];
        }

        var results = string.IsNullOrWhiteSpace(search)
            ? Shortcuts.GetShortcuts()
            : MergeSearchResults(search);

        return results
            .Select(shortcut => CreateResult(shortcut, search))
            .OrderByDescending(result => result.Score)
            .ThenBy(result => result.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public List<ContextMenuResult> LoadContextMenus(Result selectedResult)
    {
        if (selectedResult.ContextData is not string shortcutId)
        {
            return [];
        }

        var shortcut = Shortcuts.GetById(shortcutId);
        if (shortcut is null)
        {
            return [];
        }

        return
        [
            new ContextMenuResult
            {
                Title = "Run as administrator",
                Glyph = "\uEA18",
                FontFamily = "Segoe Fluent Icons,Segoe MDL2 Assets",
                Action = _ =>
                {
                    Launch(shortcut, runAsAdmin: true);
                    return true;
                },
            },
            new ContextMenuResult
            {
                Title = "Open containing folder",
                Glyph = "\uE838",
                FontFamily = "Segoe Fluent Icons,Segoe MDL2 Assets",
                Action = _ => OpenContainingFolder(shortcut.Directory),
            },
            new ContextMenuResult
            {
                Title = "Copy path",
                Glyph = "\uE8C8",
                FontFamily = "Segoe Fluent Icons,Segoe MDL2 Assets",
                Action = _ =>
                {
                    CopyPath(shortcut.Directory);
                    return true;
                },
            },
        ];
    }

    private Result CreateResult(TerminalShortcut shortcut, string search)
    {
        return new Result
        {
            Title = shortcut.Name,
            SubTitle = ShortcutDisplay.BuildSubtitle(shortcut),
            IcoPath = _iconPath,
            Score = ComputeScore(shortcut, search),
            ContextData = shortcut.Id,
            Action = action =>
            {
                var forceAdmin = action.SpecialKeyState.CtrlPressed && action.SpecialKeyState.ShiftPressed;
                Launch(shortcut, runAsAdmin: forceAdmin || shortcut.RunAsAdmin);
                return true;
            },
        };
    }

    private IEnumerable<TerminalShortcut> MergeSearchResults(string search)
    {
        var rootMatches = Shortcuts.SearchForRootPalette(search).ToArray();
        if (rootMatches.Length > 0)
        {
            return rootMatches;
        }

        return Shortcuts.Search(search);
    }

    private static int ComputeScore(TerminalShortcut shortcut, string search)
    {
        var score = shortcut.IsPinned ? 100 : 0;

        if (!string.IsNullOrWhiteSpace(search)
            && !string.IsNullOrWhiteSpace(shortcut.Abbreviation)
            && shortcut.Abbreviation.Equals(search, StringComparison.OrdinalIgnoreCase))
        {
            score += 200;
        }
        else if (!string.IsNullOrWhiteSpace(search)
            && !string.IsNullOrWhiteSpace(shortcut.Abbreviation)
            && shortcut.Abbreviation.StartsWith(search, StringComparison.OrdinalIgnoreCase))
        {
            score += 120;
        }

        if (shortcut.LastUsedUtc is not null)
        {
            var ageHours = Math.Max(0, (DateTime.UtcNow - shortcut.LastUsedUtc.Value).TotalHours);
            score += (int)Math.Max(0, 40 - ageHours);
        }

        return score;
    }

    private void Launch(TerminalShortcut shortcut, bool runAsAdmin = false, bool runAsStandard = false)
    {
        try
        {
            TerminalLauncher.Open(
                shortcut,
                Settings.TerminalApplicationId,
                Settings.DefaultProfileId,
                runAsAdmin,
                runAsStandard);
            Shortcuts.MarkUsed(shortcut.Id);
        }
        catch (Exception ex)
        {
            _context?.API.ShowMsg("Quick Shell", ex.Message, string.Empty);
        }
    }

    private static bool ShouldSuppressGlobalQuery(Query query, string search)
    {
        if (!string.IsNullOrEmpty(query.ActionKeyword))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(search))
        {
            return true;
        }

        return search.Contains("quick shell", StringComparison.OrdinalIgnoreCase);
    }

    private void OnThemeChanged(Theme currentTheme, Theme newTheme) => UpdateIconPath(newTheme);

    private void UpdateIconPath(Theme theme)
    {
        _iconPath = theme is Theme.Light or Theme.HighContrastWhite
            ? "Images\\quickshell.light.png"
            : "Images\\quickshell.dark.png";
    }

    private static bool OpenContainingFolder(string directory)
    {
        if (!ShortcutValidation.TryNormalizeDirectory(directory, out var normalized, out _))
        {
            return false;
        }

        if (!ShortcutValidation.DirectoryExists(normalized))
        {
            return false;
        }

        return System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"\"{normalized}\"",
            UseShellExecute = true,
        }) is not null;
    }

    private static bool CopyPath(string directory)
    {
        if (!ShortcutValidation.TryNormalizeDirectory(directory, out var normalized, out _))
        {
            return false;
        }

        return StaClipboard.TrySetText(normalized);
    }
}
