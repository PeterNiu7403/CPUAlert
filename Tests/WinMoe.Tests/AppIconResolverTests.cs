using WinMoe.Services;
using Xunit;

namespace WinMoe.Tests;

public sealed class AppIconResolverTests
{
    [Theory]
    [InlineData(@"C:\Apps\demo.exe,0", @"C:\Apps\demo.exe")]
    [InlineData("\"C:\\Apps\\demo.exe\",-1", @"C:\Apps\demo.exe")]
    [InlineData(@"C:\Apps\icon.ico", @"C:\Apps\icon.ico")]
    public void NormalizeIconPath_StripsRegistryIndex(string input, string expected)
    {
        Assert.Equal(expected, AppIconResolver.NormalizeIconPath(input));
    }

    [Fact]
    public void ResolveDirectImagePath_AcceptsIcoOnlyWhenExists()
    {
        Assert.Null(AppIconResolver.ResolveDirectImagePath(@"C:\definitely-missing-winmoe-icon.ico"));
    }
}
