using System.Text;
using System.Text.Json;
using QuickShell.Models;
using QuickShell.Services;

namespace QuickShell.Core.Tests;

public sealed class WorkspaceMigrationTests
{
    private static readonly string WorkspaceId = "a1b2c3d4e5f6478990a1b2c3d4e5f678";
    private static readonly string EntryId = "b2c3d4e5f6478990a1b2c3d4e5f67890";
    private static readonly string ShortcutId = "c1d2e3f4a5b6478990a1b2c3d4e5f601";

    [Fact]
    public void MigrationOutput_OmitsProjectShortcutId_AndWritesResolvedDirectory()
    {
        var shortcuts = new FakeShortcutRepository(
        [
            new TerminalShortcut
            {
                Id = ShortcutId,
                Name = "Trackdub",
                Directory = @"C:\Projects\Trackdub",
            },
        ]);

        var records = new List<WorkspaceDiskRecord>
        {
            new()
            {
                Id = WorkspaceId,
                Name = "Trackdub — Agents",
                ProjectShortcutId = ShortcutId,
                Entries =
                [
                    new WorkspaceEntry
                    {
                        Id = EntryId,
                        Label = "Claude",
                        Command = "claude",
                        IsEnabled = true,
                        Order = 0,
                    },
                ],
            },
        };

        var workspaces = WorkspaceRepository.LoadFromDiskRecordsForTests(records, shortcuts, out var requiresPersistence);
        Assert.True(requiresPersistence);
        Assert.Single(workspaces);
        Assert.Equal(@"C:\Projects\Trackdub", workspaces[0].Directory);

        var jsonBytes = WorkspaceRepository.SerializeWriteRecordsForTests(workspaces);
        var json = Encoding.UTF8.GetString(jsonBytes);

        Assert.DoesNotContain("ProjectShortcutId", json, StringComparison.Ordinal);

        using var document = JsonDocument.Parse(json);
        var workspace = document.RootElement[0];
        Assert.False(workspace.TryGetProperty("ProjectShortcutId", out _));
        Assert.Equal(@"C:\Projects\Trackdub", workspace.GetProperty("Directory").GetString());
    }
}
