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
    private static readonly string[] GitClientPresetPriority =
    [
        CompanionAppCatalog.PresetFork,
        CompanionAppCatalog.PresetGitHubDesktop,
    ];

    private static readonly string[] VisualStudioPresetPriority =
    [
        CompanionAppCatalog.PresetVs2026,
        CompanionAppCatalog.PresetVs2022,
    ];

    public static CompanionAppSuggestion? TrySuggestFromDirectory(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return null;
        }

        if (Directory.Exists(Path.Combine(directory, ".cursor")))
        {
            return BuildSuggestion(CompanionAppCatalog.PresetCursor);
        }

        if (Directory.Exists(Path.Combine(directory, ".vscode")))
        {
            return BuildSuggestion(CompanionAppCatalog.PresetVsCode);
        }

        if (Directory.Exists(Path.Combine(directory, ".obsidian")))
        {
            return BuildSuggestion(CompanionAppCatalog.PresetObsidian);
        }

        if (WorkspaceCompanionSignals.HasZedProject(directory))
        {
            return BuildSuggestion(CompanionAppCatalog.PresetZed);
        }

        if (WorkspaceCompanionSignals.HasJetBrainsProject(directory)
            && WorkspaceCompanionSignals.HasDotNetProject(directory))
        {
            return BuildSuggestion(CompanionAppCatalog.PresetRider);
        }

        if (WorkspaceCompanionSignals.HasVisualStudioSolution(directory))
        {
            return BuildFirstSuggestion(VisualStudioPresetPriority);
        }

        if (WorkspaceCompanionSignals.HasJetBrainsProject(directory))
        {
            return BuildSuggestion(CompanionAppCatalog.PresetIntelliJIdea);
        }

        if (WorkspaceCompanionSignals.HasSublimeProject(directory))
        {
            return BuildSuggestion(CompanionAppCatalog.PresetSublime);
        }

        if (WorkspaceCompanionSignals.HasGitRepository(directory))
        {
            return BuildFirstSuggestion(GitClientPresetPriority);
        }

        return null;
    }

    private static CompanionAppSuggestion? BuildFirstSuggestion(IEnumerable<string> presetIds)
    {
        foreach (var presetId in presetIds)
        {
            var suggestion = BuildSuggestion(presetId);
            if (suggestion is not null)
            {
                return suggestion;
            }
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
