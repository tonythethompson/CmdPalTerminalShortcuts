using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using QuickShell.Commands;
using QuickShell.Pages;
using QuickShell.Services;

namespace QuickShell;

public partial class QuickShellCommandsProvider : CommandProvider
{
    private readonly QuickShellPage _page;
    private readonly ICommandItem[] _commands;
    private readonly IFallbackCommandItem[] _fallbacks;

    public QuickShellCommandsProvider()
    {
        DisplayName = "Quick Shell";
        Icon = new IconInfo("\uE756");
        _page = new QuickShellPage();
        _commands = [new CommandItem(_page) { Title = DisplayName, Subtitle = "Open saved terminal directories and commands" }];
        _fallbacks = [new QuickShellFallback(_page)];
    }

    public override ICommandItem[] TopLevelCommands() => _commands;

    public override IFallbackCommandItem[] FallbackCommands() => _fallbacks;
}
