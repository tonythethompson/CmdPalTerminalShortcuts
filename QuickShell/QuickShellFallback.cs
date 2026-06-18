using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using QuickShell.Pages;

namespace QuickShell;

internal sealed partial class QuickShellFallback : FallbackCommandItem
{
    public QuickShellFallback(QuickShellPage page)
        : base("com.quickshell.fallback", "Quick Shell shortcut")
    {
        Command = page;
    }

    public override void UpdateQuery(string query)
    {
        if (Command is QuickShellPage page)
        {
            page.UpdateSearchText(string.Empty, query);
        }
    }
}
