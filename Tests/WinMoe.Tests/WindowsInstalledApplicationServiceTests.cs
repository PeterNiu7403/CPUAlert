using Microsoft.Win32;
using WinMoe.Models;
using WinMoe.Services;
using Xunit;

namespace WinMoe.Tests;

public sealed class WindowsInstalledApplicationServiceTests
{
    [Fact]
    public void GetRegistryLocations_CoversBothHivesAndBothRegistryViews()
    {
        const string uninstallSubKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";

        var locations = WindowsInstalledApplicationService.GetRegistryLocations();

        Assert.Collection(
            locations,
            location =>
            {
                Assert.Equal(RegistryHive.LocalMachine, location.Hive);
                Assert.Equal(RegistryView.Registry64, location.View);
                Assert.Equal(uninstallSubKey, location.SubKeyPath);
                Assert.Equal("Registry64", location.Source);
            },
            location =>
            {
                Assert.Equal(RegistryHive.LocalMachine, location.Hive);
                Assert.Equal(RegistryView.Registry32, location.View);
                Assert.Equal(uninstallSubKey, location.SubKeyPath);
                Assert.Equal("Registry32", location.Source);
            },
            location =>
            {
                Assert.Equal(RegistryHive.CurrentUser, location.Hive);
                Assert.Equal(RegistryView.Registry64, location.View);
                Assert.Equal(uninstallSubKey, location.SubKeyPath);
                Assert.Equal("UserRegistry64", location.Source);
            },
            location =>
            {
                Assert.Equal(RegistryHive.CurrentUser, location.Hive);
                Assert.Equal(RegistryView.Registry32, location.View);
                Assert.Equal(uninstallSubKey, location.SubKeyPath);
                Assert.Equal("UserRegistry32", location.Source);
            });
    }

    [Fact]
    public void LeftoverCandidate_DefaultsToNotSelected()
    {
        var candidate = new LeftoverCandidate("Local app data", @"C:\Users\me\AppData\Local\Example", 1);

        Assert.False(candidate.IsSelected);
    }

    [Fact]
    public void CreateApplicationFromRegistryValues_ReturnsApplication()
    {
        var values = new Dictionary<string, object?>
        {
            ["DisplayName"] = "Example App",
            ["Publisher"] = "Example Publisher",
            ["DisplayVersion"] = "1.2.3",
            ["InstallLocation"] = @"C:\Program Files\Example",
            ["UninstallString"] = "uninstall.exe",
            ["EstimatedSize"] = 1024
        };

        var app = WindowsInstalledApplicationService.CreateApplicationFromRegistryValues("example-key", values, "Registry");

        Assert.NotNull(app);
        Assert.Equal("Example App", app.Name);
        Assert.Equal("Example Publisher", app.Publisher);
        Assert.Equal("1.2.3", app.Version);
        Assert.Equal(1_048_576, app.SizeBytes);
        Assert.Equal("1 MB", app.SizeText);
    }

    [Fact]
    public void CreateApplicationFromRegistryValues_DoesNotAssignSharedDirectorySizeWhenEstimateIsMissing()
    {
        var installLocation = Path.Combine(
            Path.GetTempPath(),
            "WinMoeTests",
            Guid.NewGuid().ToString("N"),
            "Shared Launcher");
        Directory.CreateDirectory(installLocation);
        File.WriteAllBytes(Path.Combine(installLocation, "shared.bin"), new byte[17]);

        try
        {
            var app = WindowsInstalledApplicationService.CreateApplicationFromRegistryValues(
                "shared-app",
                new Dictionary<string, object?>
                {
                    ["DisplayName"] = "Shared App",
                    ["InstallLocation"] = installLocation
                },
                "Registry64");

            Assert.NotNull(app);
            Assert.Equal(0, app.SizeBytes);
            Assert.Equal("Unknown", app.SizeText);
        }
        finally
        {
            Directory.Delete(
                Path.GetDirectoryName(installLocation)!,
                recursive: true);
        }
    }

    [Fact]
    public void CreateApplicationFromRegistryValues_FiltersProtectedOrSystemEntries()
    {
        var protectedValues = new Dictionary<string, object?>
        {
            ["DisplayName"] = "Microsoft Windows Desktop Runtime",
            ["EstimatedSize"] = 100
        };
        var systemValues = new Dictionary<string, object?>
        {
            ["DisplayName"] = "Vendor Helper",
            ["SystemComponent"] = 1
        };

        Assert.Null(WindowsInstalledApplicationService.CreateApplicationFromRegistryValues("protected", protectedValues, "Registry"));
        Assert.Null(WindowsInstalledApplicationService.CreateApplicationFromRegistryValues("system", systemValues, "Registry"));
    }

    [Fact]
    public void BuildLeftoverPaths_ContainsExpectedApplicationDataLocations()
    {
        var app = WindowsInstalledApplicationService.CreateApplicationFromRegistryValues(
            "example-key",
            new Dictionary<string, object?>
            {
                ["DisplayName"] = "Example: App",
                ["Publisher"] = "Example Publisher",
                ["InstallLocation"] = @"C:\Program Files\Example"
            },
            "Registry");

        Assert.NotNull(app);

        var paths = WindowsInstalledApplicationService.BuildLeftoverPaths(app);

        Assert.Contains(paths, path => path.Category == "Install location" && path.Path == @"C:\Program Files\Example");
        Assert.Contains(paths, path => path.Category == "Local app data" && path.Path.Contains("Example App", StringComparison.Ordinal));
        Assert.Contains(paths, path => path.Category == "Publisher roaming data" && path.Path.Contains("Example Publisher", StringComparison.Ordinal));
        Assert.DoesNotContain(paths, path => path.Path.Contains(':', StringComparison.Ordinal) && !path.Path.StartsWith("C:", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task PreviewLeftoversAsync_DoesNotMeasureFilesBehindDirectoryReparsePoint()
    {
        var fixtureId = Guid.NewGuid().ToString("N");
        var fixtureRoot = Path.Combine(Path.GetTempPath(), "WinMoeTests", fixtureId);
        var installRoot = Path.Combine(fixtureRoot, "Install");
        var outsideRoot = Path.Combine(Path.GetTempPath(), "WinMoeTests", $"{fixtureId}-outside");
        var outsideLink = Path.Combine(installRoot, "outside-link");
        Directory.CreateDirectory(installRoot);
        Directory.CreateDirectory(outsideRoot);
        await File.WriteAllBytesAsync(Path.Combine(installRoot, "local.bin"), [1, 2, 3]);
        await File.WriteAllBytesAsync(Path.Combine(outsideRoot, "outside.bin"), new byte[11]);
        CreateDirectoryJunction(outsideLink, outsideRoot);

        try
        {
            var application = new InstalledApplication(
                fixtureId,
                $"WinMoe Reparse {fixtureId}",
                null,
                null,
                installRoot,
                null,
                "Test",
                0);
            var service = new WindowsInstalledApplicationService();

            var leftovers = await service.PreviewLeftoversAsync(application);

            var installLocation = Assert.Single(
                leftovers,
                candidate => candidate.Category == "Install location");
            Assert.Equal(3, installLocation.SizeBytes);
        }
        finally
        {
            if (Directory.Exists(outsideLink))
            {
                Directory.Delete(outsideLink);
            }

            if (Directory.Exists(fixtureRoot))
            {
                Directory.Delete(fixtureRoot, recursive: true);
            }

            if (Directory.Exists(outsideRoot))
            {
                Directory.Delete(outsideRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task PreviewLeftoversAsync_DoesNotMeasureTopLevelDirectoryReparsePoint()
    {
        var fixtureId = Guid.NewGuid().ToString("N");
        var fixtureRoot = Path.Combine(Path.GetTempPath(), "WinMoeTests", fixtureId);
        var outsideRoot = Path.Combine(Path.GetTempPath(), "WinMoeTests", $"{fixtureId}-outside");
        var outsideLink = Path.Combine(fixtureRoot, "outside-link");
        Directory.CreateDirectory(fixtureRoot);
        Directory.CreateDirectory(outsideRoot);
        await File.WriteAllBytesAsync(Path.Combine(outsideRoot, "outside.bin"), new byte[11]);
        CreateDirectoryJunction(outsideLink, outsideRoot);

        try
        {
            var application = new InstalledApplication(
                fixtureId,
                $"WinMoe Reparse Root {fixtureId}",
                null,
                null,
                outsideLink,
                null,
                "Test",
                0);
            var service = new WindowsInstalledApplicationService();

            var leftovers = await service.PreviewLeftoversAsync(application);

            Assert.DoesNotContain(
                leftovers,
                candidate => candidate.Category == "Install location");
        }
        finally
        {
            if (Directory.Exists(outsideLink))
            {
                Directory.Delete(outsideLink);
            }

            if (Directory.Exists(fixtureRoot))
            {
                Directory.Delete(fixtureRoot, recursive: true);
            }

            if (Directory.Exists(outsideRoot))
            {
                Directory.Delete(outsideRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void TrySplitCommandLine_ParsesQuotedExecutable()
    {
        var parsed = WindowsInstalledApplicationService.TrySplitCommandLine(
            "\"C:\\Program Files\\Demo\\uninstall.exe\" /remove /prompt",
            out var fileName,
            out var arguments);

        Assert.True(parsed);
        Assert.Equal("C:\\Program Files\\Demo\\uninstall.exe", fileName);
        Assert.Equal("/remove /prompt", arguments);
    }

    [Fact]
    public void IsSafeDeletionTarget_BlocksRoots()
    {
        var root = Path.GetPathRoot(Environment.SystemDirectory)!;

        Assert.False(WindowsInstalledApplicationService.IsSafeDeletionTarget(root));
        Assert.False(WindowsInstalledApplicationService.IsSafeDeletionTarget(Environment.GetFolderPath(Environment.SpecialFolder.Windows)));
        Assert.False(WindowsInstalledApplicationService.IsSafeDeletionTarget(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            "System32")));
        Assert.False(WindowsInstalledApplicationService.IsSafeDeletionTarget(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "Demo")));
    }

    [Fact]
    public void IsSafeLeftoverCandidate_AllowsOnlyGeneratedAppDataTargets()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        Assert.True(WindowsInstalledApplicationService.IsSafeLeftoverCandidate(
            new("Local app data", Path.Combine(localAppData, "ExampleApp"), 1)));
        Assert.False(WindowsInstalledApplicationService.IsSafeLeftoverCandidate(
            new("Local app data", Path.Combine(localAppData, "Microsoft", "Windows"), 1)));
        Assert.False(WindowsInstalledApplicationService.IsSafeLeftoverCandidate(
            new("Install location", Path.Combine(profile, "Downloads", "ExampleApp"), 1)));
        Assert.False(WindowsInstalledApplicationService.IsSafeLeftoverCandidate(
            new("Program data", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "ExampleApp"), 1)));
    }

    [Fact]
    public async Task RemoveLeftoversAsync_RoutesSafeDirectoryThroughDeletionService()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "WinMoeTests", Guid.NewGuid().ToString("N"));
        var target = Path.Combine(tempRoot, "DemoApp");
        Directory.CreateDirectory(target);
        await File.WriteAllTextAsync(Path.Combine(target, "cache.bin"), "data");

        try
        {
            var deletionService = new RecordingSafeDeletionService();
            var service = new WindowsInstalledApplicationService(deletionService);

            var results = await service.RemoveLeftoversAsync([new("Test", target, 4)]);

            var result = Assert.Single(results);
            Assert.True(result.Succeeded, result.Message);
            Assert.Single(deletionService.DeletedPaths);
            Assert.Equal(Path.GetFullPath(target), deletionService.DeletedPaths[0]);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    private static void CreateDirectoryJunction(string path, string target)
    {
        var startInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "cmd.exe",
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("/d");
        startInfo.ArgumentList.Add("/c");
        startInfo.ArgumentList.Add("mklink");
        startInfo.ArgumentList.Add("/j");
        startInfo.ArgumentList.Add(path);
        startInfo.ArgumentList.Add(target);

        using var process = System.Diagnostics.Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start junction creation.");
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new IOException($"Failed to create directory junction. {output} {error}".Trim());
        }
    }
}
