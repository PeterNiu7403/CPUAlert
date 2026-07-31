using WinMoe.Services;
using Xunit;
using Xunit.Abstractions;

namespace WinMoe.Tests;

public sealed class CleanupScanServiceTests
{
    private readonly ITestOutputHelper _output;

    public CleanupScanServiceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Measure_CountsFilesAndBytes_Recursively()
    {
        var root = Path.Combine(Path.GetTempPath(), "WinMoe-CleanupScanTests-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "sub"));
            File.WriteAllBytes(Path.Combine(root, "a.bin"), new byte[100]);
            File.WriteAllBytes(Path.Combine(root, "sub", "b.bin"), new byte[50]);

            var (sizeBytes, fileCount) = CleanupScanService.Measure(root);

            Assert.Equal(150, sizeBytes);
            Assert.Equal(2, fileCount);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Measure_MissingPath_ReturnsZero()
    {
        var (sizeBytes, fileCount) = CleanupScanService.Measure(
            Path.Combine(Path.GetTempPath(), "winmoe-definitely-missing-" + Guid.NewGuid().ToString("N")));

        Assert.Equal(0, sizeBytes);
        Assert.Equal(0, fileCount);
    }

    [Fact]
    public async Task ScanAsync_RealMachine_ReturnsWellFormedItems()
    {
        var service = new CleanupScanService();

        var items = await service.ScanAsync();

        _output.WriteLine($"items: {items.Count}, total: {items.Sum(i => i.SizeBytes)} bytes");
        foreach (var group in items.GroupBy(i => i.Category))
        {
            _output.WriteLine($"  {group.Key}: {group.Count()} items, {group.Sum(i => i.SizeBytes)} bytes");
        }

        foreach (var item in items)
        {
            Assert.False(string.IsNullOrWhiteSpace(item.Category));
            Assert.True(Path.IsPathFullyQualified(item.Path), $"path must be absolute: {item.Path}");
            Assert.True(item.SizeBytes > 0, $"size must be positive: {item.Path}");
            Assert.False(string.IsNullOrWhiteSpace(item.SizeText));
        }
    }

    [Fact]
    public void SelectVolumeTempRoots_FindsTempFoldersAcrossVolumes_AndDedupesUserTemp()
    {
        var root = Path.Combine(Path.GetTempPath(), "WinMoe-VolumeRoots-" + Guid.NewGuid().ToString("N"));
        try
        {
            var volC = Path.Combine(root, "volC");
            var volD = Path.Combine(root, "volD");
            Directory.CreateDirectory(Path.Combine(volC, "Temp"));
            Directory.CreateDirectory(Path.Combine(volD, "Temp"));
            Directory.CreateDirectory(Path.Combine(volD, "tmp"));

            // TEMP redirected to volC\Temp → that root must not be scanned twice.
            var roots = CleanupScanService.SelectVolumeTempRoots(
                new[] { volC + Path.DirectorySeparatorChar, volD + Path.DirectorySeparatorChar },
                Path.Combine(volC, "Temp"));

            Assert.Equal(
                new[] { Path.Combine(volD, "Temp"), Path.Combine(volD, "tmp") },
                roots);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void SelectVolumeTempRoots_SkipsVolumesWithoutTempFolders()
    {
        var root = Path.Combine(Path.GetTempPath(), "WinMoe-VolumeRoots-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(root);

            var roots = CleanupScanService.SelectVolumeTempRoots(new[] { root }, Path.GetTempPath());

            Assert.Empty(roots);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
