using System.Text;
using QuickShell.Models;
using QuickShell.Services;

namespace QuickShell.Core.Tests;

public sealed class WorkspaceImportTests
{
    [Fact]
    public void ImportReplace_EmptyFile_FailsWithoutPersisting()
    {
        using var directory = new TempDataDirectory();
        var shortcuts = new FakeShortcutRepository([]);
        using var repository = CreateRepository(shortcuts, directory.Path);

        var importPath = Path.Combine(directory.Path, "empty.json");
        File.WriteAllText(importPath, "[]");

        var result = repository.ImportReplace(importPath);

        Assert.False(result.Success);
        Assert.Contains("No workspaces", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ImportMerge_DuplicateNamesInBatch_RenamesLaterCandidates()
    {
        using var directory = new TempDataDirectory();
        var shortcuts = new FakeShortcutRepository([]);
        using var repository = CreateRepository(shortcuts, directory.Path);

        var importPath = Path.Combine(directory.Path, "duplicates.json");
        File.WriteAllText(importPath, BuildImportJson(
            ("Shared Name", @"C:\Projects\Alpha"),
            ("Shared Name", @"C:\Projects\Beta")));

        var result = repository.ImportMerge(importPath);

        Assert.True(result.Success);
        Assert.Equal(2, result.Imported);
        Assert.Equal(1, result.Renamed);

        var names = repository.GetWorkspaces().Select(workspace => workspace.Name).OrderBy(name => name).ToList();
        Assert.Equal(2, names.Count);
        Assert.Equal(["Shared Name", "Shared Name (2)"], names);
    }

    [Fact]
    public void ImportReplace_ExceedsMaxWorkspaceCount_FailsWithoutPersisting()
    {
        using var directory = new TempDataDirectory();
        var shortcuts = new FakeShortcutRepository([]);
        using var repository = CreateRepository(shortcuts, directory.Path);

        var workspaces = Enumerable.Range(1, WorkspaceValidation.MaxWorkspaceCount + 1)
            .Select(index => ($"Workspace {index}", $@"C:\Projects\{index}"))
            .ToArray();

        var importPath = Path.Combine(directory.Path, "oversized.json");
        File.WriteAllText(importPath, BuildImportJson(workspaces));

        Assert.Empty(repository.GetWorkspaces());
        var result = repository.ImportReplace(importPath);

        Assert.False(result.Success);
        Assert.Contains("200", result.Message, StringComparison.Ordinal);
        Assert.Empty(repository.GetWorkspaces());
    }

    [Fact]
    public void ImportReplace_DuplicateNameLaterInvalid_KeepsEarlierValidWorkspace()
    {
        using var directory = new TempDataDirectory();
        var shortcuts = new FakeShortcutRepository([]);
        using var repository = CreateRepository(shortcuts, directory.Path);

        var importPath = Path.Combine(directory.Path, "duplicate-invalid.json");
        File.WriteAllText(importPath, BuildImportJson(
            ("Alpha", @"C:\Projects\Alpha"),
            ("Alpha", "relative\\path")));

        var result = repository.ImportReplace(importPath);

        Assert.True(result.Success);
        Assert.Equal(1, result.Imported);
        Assert.Equal(1, result.Skipped);

        var workspaces = repository.GetWorkspaces();
        Assert.Single(workspaces);
        Assert.Equal("Alpha", workspaces[0].Name);
        Assert.Equal(@"C:\Projects\Alpha", workspaces[0].Directory);
    }

    private static WorkspaceRepository CreateRepository(FakeShortcutRepository shortcuts, string configDirectory) =>
        new(shortcuts, configDirectory);

    private static string BuildImportJson(params (string Name, string Directory)[] workspaces)
    {
        var builder = new StringBuilder();
        builder.Append('[');
        for (var index = 0; index < workspaces.Length; index++)
        {
            if (index > 0)
            {
                builder.Append(',');
            }

            var (name, directory) = workspaces[index];
            var id = Guid.NewGuid().ToString("N");
            var entryId = Guid.NewGuid().ToString("N");
            builder.Append($$"""
            {
              "Id": "{{id}}",
              "Name": "{{name}}",
              "Directory": "{{directory.Replace("\\", "\\\\")}}",
              "Entries": [
                {
                  "Id": "{{entryId}}",
                  "Label": "Shell",
                  "Command": "cmd",
                  "IsEnabled": true,
                  "Order": 0
                }
              ]
            }
            """);
        }

        builder.Append(']');
        return builder.ToString();
    }

    private sealed class TempDataDirectory : IDisposable
    {
        public TempDataDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "quickshell-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Path))
                {
                    Directory.Delete(Path, recursive: true);
                }
            }
            catch
            {
            }
        }
    }
}
