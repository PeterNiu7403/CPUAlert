using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using WinMoe.Services;
using Xunit;

namespace WinMoe.Tests;

public sealed class RecycleBinDeletionServiceTests
{
    private const uint FileFlagOpenReparsePoint = 0x00200000;
    private const uint FileFlagBackupSemantics = 0x02000000;

    [Fact]
    public void DeleteFileOrDirectory_RefusesJunctionAndPreservesOutsideTarget()
    {
        var fixtureRoot = Path.Combine(
            Path.GetTempPath(),
            "WinMoeRecycleBinDeletionTests",
            Guid.NewGuid().ToString("N"));
        var outsideTarget = Path.Combine(
            Path.GetTempPath(),
            $"WinMoeRecycleBinDeletionOutside-{Guid.NewGuid():N}");
        var junctionPath = Path.Combine(fixtureRoot, "outside-link");
        var sentinelPath = Path.Combine(outsideTarget, "sentinel.txt");

        Directory.CreateDirectory(fixtureRoot);
        Directory.CreateDirectory(outsideTarget);
        File.WriteAllText(sentinelPath, "outside sentinel");

        try
        {
            CreateJunction(junctionPath, outsideTarget);
            Assert.True(
                (File.GetAttributes(junctionPath) & FileAttributes.ReparsePoint) != 0,
                "The test fixture must be a reparse point.");

            // Prevent the pre-fix implementation from moving the junction into the real
            // Recycle Bin during the RED run while still exercising the public seam.
            using var junctionHandle = OpenReparsePointWithoutDeleteSharing(junctionPath);

            var result = new RecycleBinDeletionService()
                .DeleteFileOrDirectory(junctionPath, sizeBytes: 16);

            Assert.False(result.Succeeded);
            Assert.Contains("reparse point", result.Message, StringComparison.OrdinalIgnoreCase);
            Assert.True(Directory.Exists(junctionPath));
            Assert.True(File.Exists(sentinelPath));
            Assert.Equal("outside sentinel", File.ReadAllText(sentinelPath));
        }
        finally
        {
            if (Directory.Exists(junctionPath))
            {
                Directory.Delete(junctionPath);
            }

            if (File.Exists(sentinelPath))
            {
                File.Delete(sentinelPath);
            }

            if (Directory.Exists(outsideTarget))
            {
                Directory.Delete(outsideTarget);
            }

            if (Directory.Exists(fixtureRoot))
            {
                Directory.Delete(fixtureRoot);
            }
        }
    }

    private static void CreateJunction(string junctionPath, string targetPath)
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/d /c mklink /J \"{junctionPath}\" \"{targetPath}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        }) ?? throw new InvalidOperationException("Could not start mklink.");

        var standardOutput = process.StandardOutput.ReadToEnd();
        var standardError = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Could not create junction. {standardOutput}{standardError}");
        }
    }

    private static SafeFileHandle OpenReparsePointWithoutDeleteSharing(string path)
    {
        var handle = CreateFile(
            path,
            desiredAccess: 0,
            FileShare.Read | FileShare.Write,
            securityAttributes: IntPtr.Zero,
            FileMode.Open,
            FileFlagOpenReparsePoint | FileFlagBackupSemantics,
            templateFile: IntPtr.Zero);

        if (handle.IsInvalid)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        return handle;
    }

    [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(
        string fileName,
        uint desiredAccess,
        FileShare shareMode,
        IntPtr securityAttributes,
        FileMode creationDisposition,
        uint flagsAndAttributes,
        IntPtr templateFile);
}
