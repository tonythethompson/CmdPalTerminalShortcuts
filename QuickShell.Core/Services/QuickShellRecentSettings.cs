namespace QuickShell.Services;

internal static class QuickShellRecentSettings
{
    public const string SettingKey = "recentWorkspaceCount";
    public const int DefaultCount = 8;
    public const int MinCount = 0;
    public const int MaxCount = 100;

    public static int NormalizeCount(int? value) =>
        value switch
        {
            null => DefaultCount,
            < MinCount => MinCount,
            > MaxCount => MaxCount,
            _ => value.Value,
        };
}
