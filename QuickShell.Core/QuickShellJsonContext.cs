using System.Text.Json.Serialization;
using QuickShell.Models;

namespace QuickShell;

[JsonSourceGenerationOptions(
    PropertyNameCaseInsensitive = true,
    ReadCommentHandling = System.Text.Json.JsonCommentHandling.Skip,
    AllowTrailingCommas = true,
    WriteIndented = true)]
[JsonSerializable(typeof(TerminalShortcut))]
[JsonSerializable(typeof(TerminalShortcut[]))]
[JsonSerializable(typeof(List<TerminalShortcut>))]
internal sealed partial class QuickShellJsonContext : JsonSerializerContext;
