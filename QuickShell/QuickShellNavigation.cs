using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace QuickShell;

internal static class QuickShellNavigation
{
    public const string HomePageId = "com.quickshell.home";

    public static CommandResult ReturnHome(string? toastMessage = null)
    {
        ShowToast(toastMessage);
        return CommandResult.GoToPage(new GoToPageArgs
        {
            PageId = HomePageId,
        });
    }

    public static CommandResult ReturnToShortcutsList(string? toastMessage = null)
    {
        ShowToast(toastMessage);
        return CommandResult.GoToPage(new GoToPageArgs
        {
            PageId = HomePageId,
        });
    }

    public static CommandResult StayOpen(string? toastMessage = null)
    {
        ShowToast(toastMessage);
        return CommandResult.KeepOpen();
    }

    public static CommandResult GoBack(string? toastMessage = null) =>
        ReturnToShortcutsList(toastMessage);

    private static void ShowToast(string? toastMessage)
    {
        if (!string.IsNullOrWhiteSpace(toastMessage))
        {
            new ToastStatusMessage(toastMessage).Show();
        }
    }
}
