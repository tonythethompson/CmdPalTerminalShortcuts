using System.Text.Json.Serialization;
using QuickShell.Models;

namespace QuickShell;

[JsonSourceGenerationOptions(
    PropertyNameCaseInsensitive = true,
    ReadCommentHandling = System.Text.Json.JsonCommentHandling.Skip,
    AllowTrailingCommas = true,
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(TerminalShortcut))]
[JsonSerializable(typeof(TerminalShortcut[]))]
[JsonSerializable(typeof(List<TerminalShortcut>))]
[JsonSerializable(typeof(WorkspaceDiskRecord))]
[JsonSerializable(typeof(WorkspaceDiskRecord[]))]
[JsonSerializable(typeof(List<WorkspaceDiskRecord>))]
[JsonSerializable(typeof(WorkspaceWriteRecord))]
[JsonSerializable(typeof(WorkspaceWriteRecord[]))]
[JsonSerializable(typeof(List<WorkspaceWriteRecord>))]
[JsonSerializable(typeof(WorkspaceEntry))]
[JsonSerializable(typeof(List<WorkspaceEntry>))]
internal sealed partial class QuickShellJsonContext : JsonSerializerContext;
