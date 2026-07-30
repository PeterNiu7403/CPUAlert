using WinMoe.Services;
using Xunit;

namespace WinMoe.Tests;

public sealed class InstallerCleanupServiceTests : IDisposable
{
    private readonly string _root;

    public InstallerCleanupServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "WinMoeInstallerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    [Fact]
    public async Task PreviewAsync_ReturnsOnlyOldTopLevelInstallersAndArchives()
    {
        var oldInstaller = CreateFile("setup.msi", 4096, DateTime.UtcNow.AddDays(-45));
        var oldArchive = CreateFile("sdk.tar.gz", 2048, DateTime.UtcNow.AddDays(-31));
        _ = CreateFile("notes.txt", 1024, DateTime.UtcNow.AddDays(-60));
        _ = CreateFile("fresh.exe", 1024, DateTime.UtcNow.AddDays(-2));

        var nested = Path.Combine(_root, "nested");
        Directory.CreateDirectory(nested);
        File.WriteAllText(Path.Combine(nested, "nested.msi"), "nested");
        File.SetLastWriteTimeUtc(Path.Combine(nested, "nested.msi"), DateTime.UtcNow.AddDays(-60));

        var service = new InstallerCleanupService(_root, daysOld: 30);

        var items = await service.PreviewAsync();

        Assert.Equal(2, items.Count);
        Assert.Contains(items, item => item.Path == oldInstaller && item.Kind == "MSI installer");
        Assert.Contains(items, item => item.Path == oldArchive && item.Kind == "Archive");
        Assert.DoesNotContain(items, item => item.Name == "fresh.exe");
        Assert.DoesNotContain(items, item => item.Name == "nested.msi");
    }

    [Fact]
    public async Task RemoveAsync_RemovesPreviewedInstallerFile()
    {
        var file = CreateFile("driver.iso", 1024, DateTime.UtcNow.AddDays(-90));
        var deletionService = new RecordingSafeDeletionService();
        var service = new InstallerCleanupService(_root, 30, deletionService);
        var candidate = (await service.PreviewAsync()).Single();

        var results = await service.RemoveAsync([candidate]);

        var result = Assert.Single(results);
        Assert.True(result.Succeeded);
        Assert.True(File.Exists(file));
        Assert.Single(deletionService.DeletedPaths);
        Assert.Equal(Path.GetFullPath(file), deletionService.DeletedPaths[0]);
    }

    [Fact]
    public async Task RemoveAsync_RejectsCandidateOutsideDownloadsRoot()
    {
        var outside = Path.Combine(Path.GetTempPath(), $"winmoe-outside-{Guid.NewGuid():N}.msi");
        await File.WriteAllTextAsync(outside, "outside");

        try
        {
            var deletionService = new RecordingSafeDeletionService();
            var service = new InstallerCleanupService(_root, 30, deletionService);
            var candidate = new Models.InstallerCleanupCandidate(
                "outside.msi",
                outside,
                "MSI installer",
                7,
                DateTimeOffset.UtcNow.AddDays(-90));

            var results = await service.RemoveAsync([candidate]);

            var result = Assert.Single(results);
            Assert.False(result.Succeeded);
            Assert.True(File.Exists(outside));
            Assert.Empty(deletionService.DeletedPaths);
        }
        finally
        {
            File.Delete(outside);
        }
    }

    [Fact]
    public async Task RemoveAsync_RejectsReparsePointCandidateAndPreservesOutsideTarget()
    {
        var outside = Path.Combine(Path.GetTempPath(), $"winmoe-junction-target-{Guid.NewGuid():N}");
        var outsideMarker = Path.Combine(outside, "outside.txt");
        var junction = CreateFile("linked-installer.msi", 7, DateTime.UtcNow.AddDays(-90));
        Directory.CreateDirectory(outside);
        await File.WriteAllTextAsync(outsideMarker, "outside");

        try
        {
            var deletionService = new RecordingSafeDeletionService();
            var service = new InstallerCleanupService(_root, 30, deletionService);
            var candidate = (await service.PreviewAsync()).Single();
            File.Delete(junction);
            CreateDirectoryJunction(junction, outside);

            var results = await service.RemoveAsync([candidate]);

            var result = Assert.Single(results);
            Assert.False(result.Succeeded);
            Assert.True(File.Exists(outsideMarker));
            Assert.Empty(deletionService.DeletedPaths);
        }
        finally
        {
            if (Directory.Exists(junction))
            {
                Directory.Delete(junction);
            }
            else if (File.Exists(junction))
            {
                File.Delete(junction);
            }

            Directory.Delete(outside, recursive: true);
        }
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task RemoveAsync_RejectsPreviewedInstallerWhenSizeOrLastWriteTimeUtcChanges(
        bool changeSize,
        bool changeLastWriteTimeUtc)
    {
        var file = CreateFile("previewed-installer.exe", 1024, DateTime.UtcNow.AddDays(-90));
        var deletionService = new RecordingSafeDeletionService();
        var service = new InstallerCleanupService(_root, 30, deletionService);
        var candidate = (await service.PreviewAsync()).Single();

        if (changeSize)
        {
            await File.AppendAllTextAsync(file, "changed");
            File.SetLastWriteTimeUtc(file, candidate.LastWriteTime.UtcDateTime);
        }

        if (changeLastWriteTimeUtc)
        {
            File.SetLastWriteTimeUtc(file, candidate.LastWriteTime.UtcDateTime.AddMinutes(5));
        }

        var results = await service.RemoveAsync([candidate]);

        var result = Assert.Single(results);
        Assert.False(result.Succeeded);
        Assert.True(File.Exists(file));
        Assert.Empty(deletionService.DeletedPaths);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private string CreateFile(string name, int bytes, DateTime lastWriteUtc)
    {
        var path = Path.Combine(_root, name);
        File.WriteAllBytes(path, Enumerable.Repeat((byte)42, bytes).ToArray());
        File.SetLastWriteTimeUtc(path, lastWriteUtc);
        return path;
    }

    private static void CreateDirectoryJunction(string junction, string target)
    {
        var startInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/d /c mklink /J \"{junction}\" \"{target}\"",
            CreateNoWindow = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        using var process = System.Diagnostics.Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start junction creation.");
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(process.StandardError.ReadToEnd());
        }
    }
}
