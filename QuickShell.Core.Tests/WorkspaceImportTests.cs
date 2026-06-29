using QuickShell.Services;

namespace QuickShell.Core.Tests;

public sealed class WorkspaceImportTests
{
    [Fact]
    public void ImportReplace_EmptyFile_FailsWithoutPersisting()
    {
        using var directory = new TempDataDirectory();
        var shortcuts = new FakeShortcutRepository([]);
        using var repository = new WorkspaceRepository(shortcuts);

        var importPath = Path.Combine(directory.Path, "empty.json");
        File.WriteAllText(importPath, "[]");

        var result = repository.ImportReplace(importPath);

        Assert.False(result.Success);
        Assert.Contains("No workspaces", result.Message, StringComparison.OrdinalIgnoreCase);
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
