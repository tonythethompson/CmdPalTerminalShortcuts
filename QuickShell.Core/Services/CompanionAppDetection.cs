namespace QuickShell.Services;

internal sealed class CompanionAppSuggestion
{
    public required string PresetId { get; init; }

    public string? ExecutablePath { get; init; }

    public string Arguments { get; init; } = string.Empty;

    public bool EnableOnLaunch { get; init; }
}

internal static class CompanionAppDetection
{
    public static CompanionAppSuggestion? TrySuggestFromDirectory(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return null;
        }

        if (Directory.Exists(Path.Combine(directory, ".vscode")))
        {
            return BuildSuggestion(CompanionAppCatalog.PresetVsCode);
        }

        if (Directory.Exists(Path.Combine(directory, ".cursor")))
        {
            return BuildSuggestion(CompanionAppCatalog.PresetCursor);
        }

        return null;
    }

    private static CompanionAppSuggestion? BuildSuggestion(string presetId)
    {
        var executablePath = CompanionAppCatalog.TryResolveExecutable(presetId);
        if (executablePath is null)
        {
            return null;
        }

        return new CompanionAppSuggestion
        {
            PresetId = presetId,
            ExecutablePath = executablePath,
            Arguments = CompanionAppCatalog.GetDefaultArguments(presetId),
            EnableOnLaunch = true,
        };
    }
}
