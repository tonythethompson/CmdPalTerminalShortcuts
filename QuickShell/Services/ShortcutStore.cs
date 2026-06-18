using System.Text.Json;
using QuickShell.Models;

namespace QuickShell.Services;

internal static class ShortcutStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true,
    };

    private static readonly object Sync = new();
    private static IReadOnlyList<TerminalShortcut> _shortcuts = [];
    private static DateTime _lastWriteTimeUtc = DateTime.MinValue;

    public static string ConfigDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "QuickShell");

    public static string ConfigPath => Path.Combine(ConfigDirectory, "shortcuts.json");

    public static IReadOnlyList<TerminalShortcut> GetShortcuts()
    {
        lock (Sync)
        {
            EnsureLoaded();
            return _shortcuts;
        }
    }

    public static void Reload()
    {
        lock (Sync)
        {
            _lastWriteTimeUtc = DateTime.MinValue;
            EnsureLoaded(force: true);
        }
    }

    public static IEnumerable<TerminalShortcut> Search(string query)
    {
        var shortcuts = GetShortcuts();
        if (string.IsNullOrWhiteSpace(query))
        {
            return shortcuts;
        }

        return shortcuts.Where(shortcut => Matches(shortcut, query.Trim()));
    }

    private static void EnsureLoaded(bool force = false)
    {
        EnsureConfigExists();

        var writeTime = File.GetLastWriteTimeUtc(ConfigPath);
        if (!force && writeTime == _lastWriteTimeUtc)
        {
            return;
        }

        try
        {
            var json = File.ReadAllText(ConfigPath);
            _shortcuts = JsonSerializer.Deserialize<List<TerminalShortcut>>(json, JsonOptions)?
                .Where(IsValidShortcut)
                .ToArray() ?? [];
        }
        catch
        {
            _shortcuts = [];
        }

        _lastWriteTimeUtc = writeTime;
    }

    private static void EnsureConfigExists()
    {
        Directory.CreateDirectory(ConfigDirectory);

        if (File.Exists(ConfigPath))
        {
            return;
        }

        var legacyPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TerminalShortcutsCmdPal",
            "shortcuts.json");
        if (File.Exists(legacyPath))
        {
            File.Copy(legacyPath, ConfigPath);
            return;
        }

        File.WriteAllText(ConfigPath, "[]");
    }

    private static bool IsValidShortcut(TerminalShortcut shortcut) =>
        !string.IsNullOrWhiteSpace(shortcut.Name) && !string.IsNullOrWhiteSpace(shortcut.Directory);

    private static bool Matches(TerminalShortcut shortcut, string query)
    {
        if (shortcut.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(shortcut.Abbreviation) &&
            shortcut.Abbreviation.Contains(query, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (shortcut.Directory.Contains(query, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(shortcut.Command) &&
               shortcut.Command.Contains(query, StringComparison.OrdinalIgnoreCase);
    }
}
