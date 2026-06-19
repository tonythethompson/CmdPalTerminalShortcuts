using System.Text.Json;

namespace QuickShell.Services;

internal sealed class WtProfileInfo
{
    public required string Name { get; init; }

    public string? Commandline { get; init; }

    public bool IsDefault { get; init; }
}

internal static class WtProfilesService
{
    private static readonly object Sync = new();

    private static readonly string[] CandidateSettingsPaths =
    [
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Packages", "Microsoft.WindowsTerminal_8wekyb3d8bbwe", "LocalState", "settings.json"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Packages", "Microsoft.WindowsTerminalPreview_8wekyb3d8bbwe", "LocalState", "settings.json"),
    ];

    private static WtProfileInfo[] _cached = [];
    private static string? _cachedPath;
    private static DateTime _cachedWriteTimeUtc = DateTime.MinValue;

    public static void InvalidateCache()
    {
        lock (Sync)
        {
            _cached = [];
            _cachedPath = null;
            _cachedWriteTimeUtc = DateTime.MinValue;
        }
    }

    public static IReadOnlyList<WtProfileInfo> GetProfiles()
    {
        lock (Sync)
        {
            foreach (var path in CandidateSettingsPaths)
            {
                if (!File.Exists(path))
                {
                    continue;
                }

                var writeTime = File.GetLastWriteTimeUtc(path);
                if (_cachedPath == path && writeTime == _cachedWriteTimeUtc && _cached.Length > 0)
                {
                    return _cached;
                }

                var profiles = TryReadProfiles(path);
                if (profiles.Length > 0)
                {
                    _cached = profiles;
                    _cachedPath = path;
                    _cachedWriteTimeUtc = writeTime;
                    return _cached;
                }
            }

            return [];
        }
    }

    public static IReadOnlyList<string> GetProfileNames() =>
        GetProfiles().Select(p => p.Name).ToArray();

    private static WtProfileInfo[] TryReadProfiles(string path)
    {
        if (!File.Exists(path))
        {
            return [];
        }

        try
        {
            var json = File.ReadAllText(path);
            using var doc = JsonDocument.Parse(json);

            var defaultGuid = ReadDefaultProfileGuid(doc.RootElement);
            if (!doc.RootElement.TryGetProperty("profiles", out var profilesNode))
            {
                return [];
            }

            var listNode = profilesNode.TryGetProperty("list", out var directList)
                ? directList
                : profilesNode;

            if (listNode.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            var profiles = listNode
                .EnumerateArray()
                .Select(element => ToProfile(element, defaultGuid))
                .Where(p => p is not null)
                .Cast<WtProfileInfo>()
                .GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .OrderByDescending(p => p.IsDefault)
                .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            return profiles;
        }
        catch
        {
            return [];
        }
    }

    private static string? ReadDefaultProfileGuid(JsonElement root)
    {
        if (root.TryGetProperty("defaultProfile", out var topLevel) && topLevel.ValueKind == JsonValueKind.String)
        {
            return topLevel.GetString();
        }

        if (root.TryGetProperty("profiles", out var profilesNode)
            && profilesNode.TryGetProperty("defaultProfile", out var nested)
            && nested.ValueKind == JsonValueKind.String)
        {
            return nested.GetString();
        }

        return null;
    }

    private static WtProfileInfo? ToProfile(JsonElement element, string? defaultGuid)
    {
        if (!element.TryGetProperty("name", out var nameNode))
        {
            return null;
        }

        var name = nameNode.GetString();
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        if (element.TryGetProperty("hidden", out var hiddenNode)
            && hiddenNode.ValueKind == JsonValueKind.True)
        {
            return null;
        }

        var guid = element.TryGetProperty("guid", out var guidNode) ? guidNode.GetString() : null;
        var commandline = element.TryGetProperty("commandline", out var commandNode)
            ? commandNode.GetString()
            : null;

        return new WtProfileInfo
        {
            Name = name.Trim(),
            Commandline = commandline,
            IsDefault = !string.IsNullOrWhiteSpace(defaultGuid)
                && !string.IsNullOrWhiteSpace(guid)
                && defaultGuid.Equals(guid, StringComparison.OrdinalIgnoreCase),
        };
    }
}
