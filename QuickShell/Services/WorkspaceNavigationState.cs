using QuickShell.Models;

namespace QuickShell.Services;

internal static class WorkspaceNavigationState
{
    private static readonly Action NoOp = () => { };

    private static Workspace? _editorWorkspace;
    private static string? _editorOriginalName;
    private static Action? _onSaved;
    private static bool _pickerForCreate;
    private static bool _pickerChangeDirectory;

    public static void SetEditor(Workspace workspace, string? originalName, Action onSaved)
    {
        _editorWorkspace = workspace;
        _editorOriginalName = originalName;
        _onSaved = onSaved;
    }

    public static bool TryTakeEditor(out Workspace workspace, out string? originalName, out Action onSaved)
    {
        if (_editorWorkspace is null || _onSaved is null)
        {
            workspace = new Workspace();
            originalName = null;
            onSaved = NoOp;
            return false;
        }

        workspace = _editorWorkspace;
        originalName = _editorOriginalName;
        onSaved = _onSaved;
        _editorWorkspace = null;
        _editorOriginalName = null;
        _onSaved = null;
        return true;
    }

    public static void PeekEditor(out Workspace workspace, out string? originalName, out Action onSaved)
    {
        workspace = _editorWorkspace ?? new Workspace();
        originalName = _editorOriginalName;
        onSaved = _onSaved ?? NoOp;
    }

    public static void SetPicker(Action onSaved, bool forCreate, bool changeDirectory = false)
    {
        _onSaved = onSaved;
        _pickerForCreate = forCreate;
        _pickerChangeDirectory = changeDirectory;
    }

    public static void PeekPicker(out Action onSaved, out bool forCreate, out bool changeDirectory)
    {
        onSaved = _onSaved ?? NoOp;
        forCreate = _pickerForCreate;
        changeDirectory = _pickerChangeDirectory;
    }

    private static Workspace? _entryFormWorkspace;
    private static WorkspaceEntry? _entryFormEntry;
    private static Action<Workspace>? _entryFormOnChanged;

    public static void SetEntryForm(Workspace workspace, WorkspaceEntry entry, Action<Workspace> onChanged)
    {
        _entryFormWorkspace = workspace;
        _entryFormEntry = entry;
        _entryFormOnChanged = onChanged;
    }

    public static bool TryTakeEntryForm(out Workspace workspace, out WorkspaceEntry entry, out Action<Workspace> onChanged)
    {
        if (_entryFormWorkspace is null || _entryFormEntry is null || _entryFormOnChanged is null)
        {
            workspace = new Workspace();
            entry = new WorkspaceEntry();
            onChanged = static _ => { };
            return false;
        }

        workspace = _entryFormWorkspace;
        entry = _entryFormEntry;
        onChanged = _entryFormOnChanged;
        _entryFormWorkspace = null;
        _entryFormEntry = null;
        _entryFormOnChanged = null;
        return true;
    }
}
