using QuickShell.Models;

namespace QuickShell.Services;

internal static class ShortcutEditorNavigationState
{
    private static readonly Action NoOp = () => { };

    private static TerminalShortcut? _editorShortcut;
    private static string? _editorOriginalName;
    private static Action? _onSaved;

    private static TerminalShortcut? _entryFormShortcut;
    private static WorkspaceEntry? _entryFormLaunch;
    private static Action<TerminalShortcut>? _entryFormOnChanged;
    private static bool _entryFormIsNew;

    public static void SetEditor(TerminalShortcut shortcut, string? originalName, Action onSaved)
    {
        _editorShortcut = shortcut;
        _editorOriginalName = originalName;
        _onSaved = onSaved;
    }

    public static bool TryTakeEditor(out TerminalShortcut shortcut, out string? originalName, out Action onSaved)
    {
        if (_editorShortcut is null || _onSaved is null)
        {
            shortcut = new TerminalShortcut();
            originalName = null;
            onSaved = NoOp;
            return false;
        }

        shortcut = _editorShortcut;
        originalName = _editorOriginalName;
        onSaved = _onSaved;
        _editorShortcut = null;
        _editorOriginalName = null;
        _onSaved = null;
        return true;
    }

    public static void SetLaunchForm(
        TerminalShortcut shortcut,
        WorkspaceEntry launch,
        Action<TerminalShortcut> onChanged,
        bool isNew = false)
    {
        _entryFormShortcut = shortcut;
        _entryFormLaunch = launch;
        _entryFormOnChanged = onChanged;
        _entryFormIsNew = isNew;
    }

    public static bool TryTakeLaunchForm(
        out TerminalShortcut shortcut,
        out WorkspaceEntry launch,
        out Action<TerminalShortcut> onChanged,
        out bool isNew)
    {
        if (_entryFormShortcut is null || _entryFormLaunch is null || _entryFormOnChanged is null)
        {
            shortcut = new TerminalShortcut();
            launch = new WorkspaceEntry();
            onChanged = static _ => { };
            isNew = false;
            return false;
        }

        shortcut = _entryFormShortcut;
        launch = _entryFormLaunch;
        onChanged = _entryFormOnChanged;
        isNew = _entryFormIsNew;
        _entryFormShortcut = null;
        _entryFormLaunch = null;
        _entryFormOnChanged = null;
        _entryFormIsNew = false;
        return true;
    }
}
