using QuickShell.Models;
using QuickShell.Services;

namespace QuickShell.Core.Tests;

public sealed class ShortcutDisplayTests
{
    [Fact]
    public void GetLaunchContextMenuTitle_UsesCommandText()
    {
        var entry = new WorkspaceEntry
        {
            Id = "a",
            Label = "Trackdub",
            Command = "claude",
            Terminal = "default",
            IsEnabled = true,
            Order = 0,
        };

        Assert.Equal("claude", ShortcutDisplay.GetLaunchContextMenuTitle(entry));
    }

    [Fact]
    public void GetLaunchContextMenuTitle_CollapsesMultilineCommandToSingleLine()
    {
        var entry = new WorkspaceEntry
        {
            Id = "a",
            Label = "Command 2",
            Command = "npm run\r\ndev",
            Terminal = "default",
            IsEnabled = true,
            Order = 1,
        };

        Assert.Equal("npm run dev", ShortcutDisplay.GetLaunchContextMenuTitle(entry));
    }

    [Fact]
    public void GetLaunchContextMenuTitle_FallsBackToLabelWhenCommandBlank()
    {
        var entry = new WorkspaceEntry
        {
            Id = "a",
            Label = "Frontend",
            Command = string.Empty,
            Terminal = "default",
            IsEnabled = true,
            Order = 0,
        };

        Assert.Equal("Frontend", ShortcutDisplay.GetLaunchContextMenuTitle(entry));
    }

    [Fact]
    public void GetLaunchContextMenuTitle_UsesOpenFolderWhenCommandAndLabelBlank()
    {
        var entry = new WorkspaceEntry
        {
            Id = "a",
            Label = string.Empty,
            Command = string.Empty,
            Terminal = "default",
            IsEnabled = true,
            Order = 0,
        };

        Assert.Equal("Open folder", ShortcutDisplay.GetLaunchContextMenuTitle(entry));
    }
}
