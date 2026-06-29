using QuickShell.Models;
using QuickShell.Services;

namespace QuickShell.Core.Tests;

public sealed class WorkspaceDiscoveryTests
{
    [Fact]
    public void GetByDirectory_InvalidQuery_ReturnsEmpty()
    {
        var shortcuts = new FakeShortcutRepository([]);
        using var repository = new WorkspaceRepository(shortcuts);

        var matches = repository.GetByDirectory("relative\\path");

        Assert.Empty(matches);
    }

    [Fact]
    public void GetByDirectory_MatchesLexicallyWithoutShortcutRepository()
    {
        var workspaceDirectory = @"C:\Projects\Foo";
        var queryDirectory = @"c:\projects\foo\";

        Assert.True(WorkspacePath.TryNormalizeLexical(queryDirectory, out _, out _));
        Assert.True(WorkspacePath.PathsEqual(workspaceDirectory, queryDirectory));
    }
}
