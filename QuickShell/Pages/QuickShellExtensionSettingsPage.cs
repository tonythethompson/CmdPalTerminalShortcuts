using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace QuickShell.Pages;

internal sealed partial class QuickShellExtensionSettingsPage : ContentPage
{
    private readonly Settings _settings;

    public QuickShellExtensionSettingsPage(Settings settings)
    {
        _settings = settings;
        Name = "Settings";
        Title = "Quick Shell settings";
        Icon = new IconInfo("\uE713");
    }

    public override IContent[] GetContent() => _settings.ToContent();
}
