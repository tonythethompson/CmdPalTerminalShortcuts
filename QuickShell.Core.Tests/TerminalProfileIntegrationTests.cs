using QuickShell.Models;
using QuickShell.Services;

namespace QuickShell.Core.Tests;

public sealed class TerminalProfileIntegrationTests
{
    [Fact]
    public void GetForLaunch_ResolvesDefaultTerminalToConfiguredProfileIcon()
    {
        WtProfilesService.InvalidateCache();
        WindowsTerminalInstallDiscovery.InvalidateCache();

        var launch = new WorkspaceEntry { Terminal = "default", IsEnabled = true };
        var icon = TerminalLaunchGlyphs.GetForLaunch(launch);

        Assert.False(string.IsNullOrWhiteSpace(icon));
        Assert.True(
            icon.Contains('\\') || icon.Contains('/') || icon.Length > 2,
            $"Expected a profile image path or emoji, got '{icon}'");
    }
}
