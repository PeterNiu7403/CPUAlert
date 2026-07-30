using MoleWindows.Models;
using Xunit;

namespace MoleWindows.Tests;

// Binds the Windows GUI to the optional conductor's unified result envelope:
// one shape per call, branch on `ok`. Mirrors the molewindows-cli-side tests.
// NOTE: authored without a local .NET/Windows toolchain — verified on CI only.
public sealed class MoleWindowsEnvelopeTests
{
    [Fact]
    public void Parses_Success_And_Branches_On_Ok()
    {
        var e = MoleWindowsEnvelope.Parse(
            """{"ok":true,"molewindows_cli":"0.0.1","engine":"molewindows-engine","command":"status","data":{"health_score":92}}""");

        Assert.True(e.Ok);
        Assert.Equal("status", e.Command);
        Assert.Equal(92, e.Data.GetProperty("health_score").GetInt32());
        Assert.Null(e.Error);
    }

    [Fact]
    public void Parses_Failure_With_Error_And_No_Data()
    {
        var e = MoleWindowsEnvelope.Parse(
            """{"ok":false,"molewindows_cli":"0.0.1","engine":"molewindows-engine","command":"uninstall","error":{"kind":"not_found","message":"needs an app","platform":"macos"}}""");

        Assert.False(e.Ok);
        Assert.Equal("uninstall", e.Command);
        Assert.Equal("not_found", e.Error?.Kind);
        Assert.Equal("needs an app", e.Error?.Message);
        Assert.Equal("macos", e.Error?.Platform);
    }
}
