using QuickShell.Models;
using QuickShell.Services;

namespace QuickShell.Core.Tests;

public sealed class WorkspaceValidationTests
{
    private static readonly string WorkspaceId = "a1b2c3d4e5f6478990a1b2c3d4e5f678";
    private static readonly string EntryId = "b2c3d4e5f6478990a1b2c3d4e5f67890";
    private static readonly string ShortcutId = "c1d2e3f4a5b6478990a1b2c3d4e5f601";

    [Fact]
    public void NormalizeForLoad_LegacyProjectShortcutId_ReachesMapping()
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

        var record = CreateDiskRecord(projectShortcutId: ShortcutId, directory: null);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var result = WorkspaceValidation.NormalizeForLoad(record, shortcuts, seen);

        Assert.NotNull(result.Workspace);
        Assert.True(result.RequiresPersistence);
        Assert.Equal(@"C:\Projects\Trackdub", result.Workspace!.Directory);
    }

    [Fact]
    public void NormalizeForLoad_DirectoryWinsOverLegacyId()
    {
        var shortcuts = new FakeShortcutRepository(
        [
            new TerminalShortcut
            {
                Id = ShortcutId,
                Name = "Trackdub",
                Directory = @"C:\Projects\Other",
            },
        ]);

        var record = CreateDiskRecord(
            projectShortcutId: ShortcutId,
            directory: @"C:\Projects\Winner");

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = WorkspaceValidation.NormalizeForLoad(record, shortcuts, seen);

        Assert.Equal(@"C:\Projects\Winner", result.Workspace!.Directory);
    }

    [Fact]
    public void NormalizeForLoad_DeadLegacyId_IsRepairable()
    {
        var shortcuts = new FakeShortcutRepository([]);
        var record = CreateDiskRecord(projectShortcutId: ShortcutId, directory: null);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var result = WorkspaceValidation.NormalizeForLoad(record, shortcuts, seen);

        Assert.NotNull(result.Workspace);
        Assert.True(result.NeedsFolderRepair);
        Assert.True(result.RequiresPersistence);
        Assert.Equal(string.Empty, result.Workspace!.Directory);
    }

    [Fact]
    public void NormalizeForLoad_DuplicateWorkspaceId_IsSkipped()
    {
        var shortcuts = new FakeShortcutRepository([]);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { WorkspaceId };
        var record = CreateDiskRecord(directory: @"C:\Projects\Foo");

        var result = WorkspaceValidation.NormalizeForLoad(record, shortcuts, seen);

        Assert.Null(result.Workspace);
        Assert.Contains("duplicate", result.Warning ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryValidateForSave_RejectsRepairableEmptyDirectory()
    {
        var shortcuts = new FakeShortcutRepository([]);
        var workspaces = new InMemoryWorkspaceRepository();
        var workspace = CreateRuntimeWorkspace(directory: string.Empty);

        Assert.False(WorkspaceValidation.TryValidateForSave(workspace, shortcuts, workspaces, null, out var error));
        Assert.Contains("required", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryValidateForImport_AcceptsRepairableEmptyDirectory()
    {
        var shortcuts = new FakeShortcutRepository([]);
        var workspace = CreateRuntimeWorkspace(directory: string.Empty);

        Assert.True(WorkspaceValidation.TryValidateForImport(workspace, shortcuts, [], out var error));
        Assert.Equal(string.Empty, error);
    }

    [Fact]
    public void TryValidateForSave_RenameKeepsUnchangedAbbreviation()
    {
        var shortcuts = new FakeShortcutRepository([]);
        var tempDirectory = Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var existing = CreateRuntimeWorkspace(directory: tempDirectory);
        existing.Abbreviation = "agents";
        var renamed = WorkspaceMapper.CloneWorkspace(existing);
        renamed.Name = "Agents Renamed";
        var repo = new InMemoryWorkspaceRepository(existing);

        Assert.True(
            WorkspaceValidation.TryValidateForSave(renamed, shortcuts, repo, existing.Name, out var error),
            error);
    }

    private static WorkspaceDiskRecord CreateDiskRecord(string? directory = @"C:\Projects\Foo", string? projectShortcutId = null) =>
        new()
        {
            Id = WorkspaceId,
            Name = "Agents",
            Directory = directory,
            ProjectShortcutId = projectShortcutId,
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
        };

    private static Workspace CreateRuntimeWorkspace(string directory) => new()
    {
        Id = WorkspaceId,
        Name = "Agents",
        Directory = directory,
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
    };

    private sealed class InMemoryWorkspaceRepository : IWorkspaceRepository
    {
        private readonly List<Workspace> _workspaces;

        public InMemoryWorkspaceRepository(params Workspace[] workspaces) =>
            _workspaces = workspaces.ToList();

        public string ConfigPath => string.Empty;

        public IReadOnlyList<Workspace> GetWorkspaces() => _workspaces;

        public Workspace? GetByName(string name) =>
            _workspaces.FirstOrDefault(workspace => workspace.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

        public Workspace? GetById(string id) => null;

        public IReadOnlyList<Workspace> GetByDirectory(string directory) => [];

        public void Reload()
        {
        }

        public void FlushPendingWrites()
        {
        }

        public void Upsert(Workspace workspace, string? originalName = null)
        {
        }

        public bool Delete(string name) => false;

        public bool TogglePinned(string name) => false;

        public Workspace? BuildDuplicate(string name) => null;

        public IEnumerable<Workspace> Search(string query) => [];

        public IEnumerable<Workspace> SearchForRootPalette(string query) => [];

        public string ResolveAvailableName(string desiredName, string? replacingOriginalName = null) => desiredName;

        public bool TryExportToFile(string path, out string error)
        {
            error = string.Empty;
            return false;
        }

        public Task<WorkspaceExportResult> TryExportToFileAsync(string path, CancellationToken cancellationToken = default) =>
            Task.FromResult(new WorkspaceExportResult(false, string.Empty));

        public bool TryReadImportFile(string path, out Workspace[] workspaces, out string error)
        {
            workspaces = [];
            error = string.Empty;
            return false;
        }

        public Task<WorkspaceImportReadResult> TryReadImportFileAsync(string path, CancellationToken cancellationToken = default) =>
            Task.FromResult(new WorkspaceImportReadResult(false, [], string.Empty));

        public WorkspaceTransferResult ImportMerge(string path) => new();

        public Task<WorkspaceTransferResult> ImportMergeAsync(string path, CancellationToken cancellationToken = default) =>
            Task.FromResult(new WorkspaceTransferResult());

        public WorkspaceTransferResult ImportReplace(string path) => new();

        public Task<WorkspaceTransferResult> ImportReplaceAsync(string path, CancellationToken cancellationToken = default) =>
            Task.FromResult(new WorkspaceTransferResult());
    }
}
