using QuickShell.Models;
using QuickShell.Services;

namespace QuickShell.Core.Tests;

public sealed class GitRepoDiscoveryTests : IDisposable
{
    private readonly string _root;

    public GitRepoDiscoveryTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "quickshell-git-discovery-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    [Fact]
    public void Discover_FindsGitRepositoriesUnderProvidedRoot()
    {
        var repoPath = Path.Combine(_root, "sample-repo");
        Directory.CreateDirectory(repoPath);
        Directory.CreateDirectory(Path.Combine(repoPath, ".git"));

        var discovered = GitRepoDiscovery.Discover([_root]);

        Assert.Contains(discovered, candidate =>
            string.Equals(candidate.Directory, repoPath, StringComparison.OrdinalIgnoreCase)
            && candidate.Name == "sample-repo");
    }

    [Fact]
    public void Discover_ReadsHttpsOriginRemote()
    {
        var repoPath = Path.Combine(_root, "with-remote");
        var gitPath = Path.Combine(repoPath, ".git");
        Directory.CreateDirectory(gitPath);
        File.WriteAllText(
            Path.Combine(gitPath, "config"),
            """
            [remote "origin"]
                url = https://github.com/example/sample.git
            """);

        var discovered = GitRepoDiscovery.Discover([_root]).Single();

        Assert.Equal("https://github.com/example/sample", discovered.RemoteUrl);
    }

    [Fact]
    public void Discover_SkipsNestedSearchInsideGitRepository()
    {
        var repoPath = Path.Combine(_root, "outer");
        Directory.CreateDirectory(Path.Combine(repoPath, ".git"));
        Directory.CreateDirectory(Path.Combine(repoPath, "nested", ".git"));

        var discovered = GitRepoDiscovery.Discover([_root]);

        Assert.Single(discovered);
        Assert.Equal("outer", discovered[0].Name);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch
        {
            // Best effort cleanup for temp test data.
        }
    }
}

public sealed class ShortcutRecentsTests
{
    [Fact]
    public void GetRecentWorkspaces_OrdersByLastUsedAndSkipsPinned()
    {
        var shortcuts = new List<TerminalShortcut>
        {
            new()
            {
                Id = "1",
                Name = "Old",
            },
            new()
            {
                Id = "2",
                Name = "Recent",
                LastUsedUtc = DateTime.UtcNow.AddHours(-1),
            },
            new()
            {
                Id = "3",
                Name = "Pinned recent",
                IsPinned = true,
                LastUsedUtc = DateTime.UtcNow,
            },
            new()
            {
                Id = "4",
                Name = "Never used",
            },
        };

        var recents = ShortcutRecents.GetRecentWorkspaces(shortcuts);

        Assert.Single(recents);
        Assert.Equal("Recent", recents[0].Name);
    }
}

public sealed class WorkspaceLinkValidationTests
{
    [Fact]
    public void TryValidateOptionalLinkUrl_AcceptsHttpAndHttps()
    {
        Assert.True(ShortcutValidation.TryValidateOptionalLinkUrl("http://localhost:3000", out _, out var normalized));
        Assert.Equal("http://localhost:3000/", normalized);

        Assert.True(ShortcutValidation.TryValidateOptionalLinkUrl("https://github.com/example/repo", out _, out normalized));
        Assert.Equal("https://github.com/example/repo", normalized);
    }

    [Fact]
    public void TryValidateOptionalLinkUrl_RejectsNonHttpSchemes()
    {
        Assert.False(ShortcutValidation.TryValidateOptionalLinkUrl("file:///C:/temp", out var error, out _));
        Assert.Contains("http", error, StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class DevServerUrlDetectionTests : IDisposable
{
    private readonly string _root;

    public DevServerUrlDetectionTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "quickshell-dev-server-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    [Fact]
    public void TryDetectDevServerUrl_ReadsExplicitPortFromDevScript()
    {
        WritePackageJson("""
        {
          "scripts": {
            "dev": "vite --port 4321"
          }
        }
        """);

        var url = DevServerUrlDetection.TryDetectDevServerUrl(_root);

        Assert.Equal("http://localhost:4321", url);
    }

    [Fact]
    public void TryDetectDevServerUrl_UsesViteDefaultWhenNoPortSpecified()
    {
        WritePackageJson("""
        {
          "devDependencies": {
            "vite": "^6.0.0"
          },
          "scripts": {
            "dev": "vite"
          }
        }
        """);

        var url = DevServerUrlDetection.TryDetectDevServerUrl(_root);

        Assert.Equal("http://localhost:5173", url);
    }

    [Fact]
    public void TryDetectDevServerUrl_ReturnsNullWhenNoPackageJson()
    {
        Assert.Null(DevServerUrlDetection.TryDetectDevServerUrl(_root));
    }

    private void WritePackageJson(string contents) =>
        File.WriteAllText(Path.Combine(_root, "package.json"), contents);

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch
        {
        }
    }
}

public sealed class GitRepoIndexTests : IDisposable
{
    private readonly string _root;

    public GitRepoIndexTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "quickshell-git-index-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        GitRepoIndex.Invalidate();
    }

    [Fact]
    public void Search_FiltersByNameAndSkipsSavedDirectories()
    {
        var repoPath = Path.Combine(_root, "alpha-app");
        Directory.CreateDirectory(Path.Combine(repoPath, ".git"));

        GitRepoIndex.Invalidate();
        var saved = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { repoPath };

        var matches = GitRepoIndex.Search("alpha", [_root], saved);

        Assert.Empty(matches);

        matches = GitRepoIndex.Search("alpha", [_root], savedDirectories: null);

        Assert.Single(matches);
        Assert.Equal("alpha-app", matches[0].Name);
    }

    public void Dispose()
    {
        GitRepoIndex.Invalidate();
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch
        {
        }
    }
}

public sealed class CompanionAppTests : IDisposable
{
    private readonly string _root;

    public CompanionAppTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "quickshell-companion-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    [Fact]
    public void TrySuggestFromDirectory_PrefersVsCodeWhenDotVscodeExists()
    {
        Directory.CreateDirectory(Path.Combine(_root, ".vscode"));

        var suggestion = CompanionAppDetection.TrySuggestFromDirectory(_root);

        if (CompanionAppCatalog.TryResolveExecutable(CompanionAppCatalog.PresetVsCode) is null)
        {
            Assert.Null(suggestion);
            return;
        }

        Assert.NotNull(suggestion);
        Assert.Equal(CompanionAppCatalog.PresetVsCode, suggestion!.PresetId);
        Assert.Equal(".", suggestion.Arguments);
        Assert.True(suggestion.EnableOnLaunch);
    }

    [Fact]
    public void ExpandArguments_ReplacesFolderTokenAndDot()
    {
        var directory = @"C:\Projects\sample app";

        Assert.Equal("\"C:\\Projects\\sample app\"", CompanionAppLauncher.ExpandArguments(".", directory));
        Assert.Equal(
            "\"C:\\Projects\\sample app\" --new-window",
            CompanionAppLauncher.ExpandArguments("{folder} --new-window", directory));
        Assert.Equal("C:\\Projects\\sample", CompanionAppLauncher.ExpandArguments(".", @"C:\Projects\sample"));
    }

    [Fact]
    public void TryValidateCompanionApp_RequiresPathWhenLaunchEnabled()
    {
        var shortcut = new TerminalShortcut
        {
            Name = "Sample",
            Directory = _root,
            OpenCompanionAppOnLaunch = true,
        };

        Assert.False(ShortcutValidation.TryValidateCompanionApp(shortcut, out var error));
        Assert.Contains("required", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void InferPresetFromPath_RecognizesKnownEditors()
    {
        Assert.Equal(
            CompanionAppCatalog.PresetVsCode,
            CompanionAppCatalog.InferPresetFromPath(@"C:\Apps\Microsoft VS Code\Code.exe"));
        Assert.Equal(
            CompanionAppCatalog.PresetCustom,
            CompanionAppCatalog.InferPresetFromPath(@"C:\Apps\MyEditor.exe"));
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch
        {
        }
    }
}

public sealed class ShortcutHealthTests : IDisposable
{
    private readonly string _root;

    public ShortcutHealthTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "quickshell-health-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    [Fact]
    public void NeedsRepair_ReturnsTrueWhenDirectoryMissingOnDisk()
    {
        var shortcut = new TerminalShortcut
        {
            Name = "Gone",
            Directory = Path.Combine(_root, "missing-folder"),
            Launches = [new WorkspaceEntry { Label = "Main", IsEnabled = true }],
        };

        Assert.True(ShortcutHealth.NeedsRepair(shortcut));
        Assert.Equal(ShortcutGlyphs.IncidentTriangle, ShortcutHealth.GetListGlyph(shortcut));
        Assert.Contains("Folder not found", ShortcutHealth.BuildListSubtitle(shortcut), StringComparison.Ordinal);
    }

    [Fact]
    public void NeedsRepair_ReturnsFalseForHealthyShortcut()
    {
        var shortcut = new TerminalShortcut
        {
            Name = "Healthy",
            Directory = _root,
            Launches = [new WorkspaceEntry { Label = "Main", IsEnabled = true }],
        };

        Assert.False(ShortcutHealth.NeedsRepair(shortcut));
        Assert.Equal(ShortcutGlyphs.NewWindow, ShortcutHealth.GetListGlyph(shortcut));
    }

    [Fact]
    public void GetListGlyph_UsesAdminIconWhenHealthyAndElevated()
    {
        var shortcut = new TerminalShortcut
        {
            Name = "Admin",
            Directory = _root,
            RunAsAdmin = true,
            Launches = [new WorkspaceEntry { Label = "Main", IsEnabled = true }],
        };

        Assert.False(ShortcutHealth.NeedsRepair(shortcut));
        Assert.Equal(ShortcutGlyphs.AdminLaunch, ShortcutHealth.GetListGlyph(shortcut));
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch
        {
        }
    }
}

public sealed class TerminalLaunchGlyphsTests
{
    [Fact]
    public void GetForLaunch_UsesPowerShellGlyphForPwsh()
    {
        var launch = new WorkspaceEntry { Terminal = "pwsh", IsEnabled = true };

        Assert.Equal(ShortcutGlyphs.PowerShell, TerminalLaunchGlyphs.GetForLaunch(launch));
    }

    [Fact]
    public void GetForLaunch_UsesLinuxGlyphForUbuntuProfile()
    {
        var launch = new WorkspaceEntry { Terminal = "wt", WtProfile = "Ubuntu", IsEnabled = true };

        Assert.Equal(ShortcutGlyphs.Linux, TerminalLaunchGlyphs.GetForLaunch(launch));
    }

    [Fact]
    public void GetForLaunch_UsesNewWindowForDefaultTerminal()
    {
        var launch = new WorkspaceEntry { Terminal = "default", IsEnabled = true };

        Assert.Equal(ShortcutGlyphs.NewWindow, TerminalLaunchGlyphs.GetForLaunch(launch));
    }
}
