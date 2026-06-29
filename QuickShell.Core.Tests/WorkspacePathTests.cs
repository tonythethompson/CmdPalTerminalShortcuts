using QuickShell.Models;
using QuickShell.Services;

namespace QuickShell.Core.Tests;

public sealed class WorkspacePathTests
{
    [Theory]
    [InlineData(@"C:\Projects\Foo", @"c:\projects\foo", true)]
    [InlineData(@"C:\Projects\Foo\", @"C:\Projects\Foo", true)]
    [InlineData(@"C:\Projects\Foo\.\Bar", @"C:\Projects\Foo\Bar", true)]
    [InlineData(@"\\server\share\Foo", @"\\SERVER\share\Foo", true)]
    [InlineData(@"\\wsl$\Ubuntu\home\user", @"\\wsl$\ubuntu\home\user", false)]
    [InlineData(@"C:\A", @"C:\B", false)]
    public void PathsEqual_UsesNamespacePolicy(string left, string right, bool expected)
    {
        Assert.Equal(expected, WorkspacePath.PathsEqual(left, right));
    }

    [Theory]
    [InlineData(@".\Trackdub")]
    [InlineData(@"Trackdub")]
    [InlineData(@"..\Projects\Trackdub")]
    public void TryNormalizeLexical_RejectsRelativePaths(string path)
    {
        Assert.False(WorkspacePath.TryNormalizeLexical(path, out _, out var error));
        Assert.Contains("absolute", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryNormalizeLexical_RejectsRawLinuxPaths()
    {
        Assert.False(WorkspacePath.TryNormalizeLexical("/home/me/project", out _, out var error));
        Assert.Contains("Linux-style", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryNormalizeLexical_AcceptsRootedWindowsPath()
    {
        Assert.True(WorkspacePath.TryNormalizeLexical(@"C:\Projects\Foo", out var normalized, out _));
        Assert.StartsWith(@"C:\", normalized, StringComparison.Ordinal);
        Assert.DoesNotContain(@".\", normalized, StringComparison.Ordinal);
    }

    [Fact]
    public void PathsEqual_ReturnsFalseForInvalidInputWithoutThrowing()
    {
        Assert.False(WorkspacePath.PathsEqual("not-a-path", @"C:\Valid"));
        Assert.False(WorkspacePath.PathsEqual(null, @"C:\Valid"));
    }
}
