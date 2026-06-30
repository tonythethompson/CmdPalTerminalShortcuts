using QuickShell.Models;

namespace QuickShell.Services;

internal static class WorkspaceSeedFactory
{
    public static TerminalShortcut FromGitRepo(GitRepoCandidate candidate) =>
        ApplyDirectoryHints(new TerminalShortcut
        {
            Name = candidate.Name,
            Directory = candidate.Directory,
            RepoUrl = candidate.RemoteUrl,
        });

    public static TerminalShortcut ApplyDirectoryHints(TerminalShortcut seed)
    {
        if (string.IsNullOrWhiteSpace(seed.Directory))
        {
            return seed;
        }

        if (string.IsNullOrWhiteSpace(seed.RepoUrl))
        {
            seed.RepoUrl = GitRepoDiscovery.TryGetRemoteUrl(seed.Directory);
        }

        if (string.IsNullOrWhiteSpace(seed.DevServerUrl))
        {
            seed.DevServerUrl = DevServerUrlDetection.TryDetectDevServerUrl(seed.Directory);
        }

        if (string.IsNullOrWhiteSpace(seed.CompanionAppPath))
        {
            var suggestion = CompanionAppDetection.TrySuggestFromDirectory(seed.Directory);
            if (suggestion is not null)
            {
                seed.CompanionAppPath = suggestion.ExecutablePath;
                seed.CompanionAppArguments = suggestion.Arguments;
                seed.OpenCompanionAppOnLaunch = suggestion.EnableOnLaunch;
            }
        }

        return seed;
    }
}
