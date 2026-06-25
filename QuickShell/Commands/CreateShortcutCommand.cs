using QuickShell.Pages;

namespace QuickShell.Commands;

/// <summary>
/// Opens a fresh create-shortcut form. Separate type so Command Palette does not reuse
/// the same navigation slot as edit forms.
/// </summary>
internal sealed partial class CreateShortcutCommand : ShortcutFormPage
{
    public CreateShortcutCommand(Action onSaved)
        : base(existing: null, onSaved)
    {
    }
}
