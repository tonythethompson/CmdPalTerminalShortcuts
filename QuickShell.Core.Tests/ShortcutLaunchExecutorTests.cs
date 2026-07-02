using QuickShell.Models;
using QuickShell.Services;

namespace QuickShell.Core.Tests;

public sealed class ShortcutLaunchExecutorTests
{
    [Fact]
    public void Launch_ReturnsErrorWhenDirectoryMissing()
    {
        var shortcut = new TerminalShortcut
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = "Missing",
            Directory = @"C:\does-not-exist-quickshell-test",
            Launches =
            [
                new WorkspaceEntry
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Label = "Main",
                    Terminal = "default",
                    IsEnabled = true,
                    Order = 0,
                },
            ],
        };

        var result = ShortcutLaunchExecutor.Launch(shortcut, "wt", "default");

        Assert.False(result.Dismiss);
        Assert.Contains("folder not found", result.StayOpenMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Launch_ReturnsErrorWhenNoEnabledLaunches()
    {
        var shortcut = new TerminalShortcut
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = "Disabled",
            Directory = Environment.CurrentDirectory,
            Launches =
            [
                new WorkspaceEntry
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Label = "Off",
                    Terminal = "default",
                    IsEnabled = false,
                    Order = 0,
                },
            ],
        };

        var result = ShortcutLaunchExecutor.Launch(shortcut, "wt", "default");

        Assert.False(result.Dismiss);
        Assert.Contains("no enabled launch", result.StayOpenMessage, StringComparison.OrdinalIgnoreCase);
    }
}
