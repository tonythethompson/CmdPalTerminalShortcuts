using System.Text;

namespace QuickShell.Services;

internal static class ShortcutCommandIds
{
    public const string CreateShortcut = "com.quickshell.shortcut-form.create";

    private const string OpenPrefix = "com.quickshell.shortcut.open.";

    public static string Open(string shortcutId) =>
        OpenPrefix + shortcutId;

    public static bool TryParseOpen(string commandId, out string key)
    {
        key = string.Empty;

        if (!commandId.StartsWith(OpenPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        key = commandId[OpenPrefix.Length..];
        if (key.EndsWith(".admin", StringComparison.Ordinal))
        {
            key = key[..^".admin".Length];
        }

        return !string.IsNullOrWhiteSpace(key);
    }

    public static bool TryDecodeLegacyNameKey(string key, out string shortcutName)
    {
        shortcutName = string.Empty;

        if (string.IsNullOrWhiteSpace(key) || IsStableShortcutId(key))
        {
            return false;
        }

        return TryDecodeHexUtf8(key, out shortcutName);
    }

    public static bool IsStableShortcutId(string key) =>
        key.Length == 32 && key.All(static c => Uri.IsHexDigit(c));

    private static bool TryDecodeHexUtf8(string encoded, out string value)
    {
        value = string.Empty;

        try
        {
            value = Encoding.UTF8.GetString(Convert.FromHexString(encoded));
            return !string.IsNullOrEmpty(value);
        }
        catch
        {
            return false;
        }
    }
}
