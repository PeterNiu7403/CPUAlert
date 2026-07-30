using WinMoe.Models;
using WinMoe.Services;
using Xunit;

namespace WinMoe.Tests;

public sealed class MachineDeletionIntegrationTests
{
    private const string EnableVariable = "WINMOE_RUN_MACHINE_DELETE_TESTS";

    [Fact]
    [Trait("Category", "Machine")]
    public async Task ProductionServices_FindAndRecycleOnlyControlledFixtureTargets()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable(EnableVariable),
                "1",
                StringComparison.Ordinal))
        {
            return;
        }

        var fixtureId = Guid.NewGuid().ToString("N");
        var fixtureBase = Path.GetFullPath(Path.Combine(
            Path.GetTempPath(),
            "WinMoeMachineDeletionTests"));
        var outsideBase = Path.GetFullPath(Path.Combine(
            Path.GetTempPath(),
            "WinMoeMachineDeletionOutside"));
        var localAppDataBase = Path.GetFullPath(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WinMoeMachineDeletionTests"));
        var fixtureRoot = Path.Combine(fixtureBase, fixtureId);
        var outsideRoot = Path.Combine(outsideBase, fixtureId);
        var localAppDataRoot = Path.Combine(localAppDataBase, fixtureId);

        AssertControlledChild(fixtureRoot, fixtureBase);
        AssertControlledChild(outsideRoot, outsideBase);
        AssertControlledChild(localAppDataRoot, localAppDataBase);

        Directory.CreateDirectory(fixtureRoot);
        Directory.CreateDirectory(outsideRoot);
        Directory.CreateDirectory(localAppDataRoot);
        var sentinelPath = Path.Combine(outsideRoot, "outside-sentinel.txt");
        await File.WriteAllTextAsync(sentinelPath, "outside sentinel");

        try
        {
            var deletionService = new RecycleBinDeletionService();

            await VerifyInstallerCleanupAsync(fixtureRoot, deletionService);
            await VerifyProjectPurgeAsync(fixtureRoot, deletionService);
            await VerifyLeftoverCleanupAsync(localAppDataRoot, deletionService);

            Assert.True(File.Exists(sentinelPath));
            Assert.Equal("outside sentinel", await File.ReadAllTextAsync(sentinelPath));
        }
        finally
        {
            DeleteControlledTree(fixtureRoot, fixtureBase);
            DeleteControlledTree(outsideRoot, outsideBase);
            DeleteControlledTree(localAppDataRoot, localAppDataBase);
        }
    }

    private static async Task VerifyInstallerCleanupAsync(
        string fixtureRoot,
        ISafeDeletionService deletionService)
    {
        var downloadsRoot = Path.Combine(fixtureRoot, "Downloads");
        Directory.CreateDirectory(downloadsRoot);
        var installerPath = Path.Combine(downloadsRoot, "old setup 测试.exe");
        await File.WriteAllBytesAsync(installerPath, new byte[37]);
        File.SetLastWriteTimeUtc(installerPath, DateTime.UtcNow.AddDays(-60));

        var service = new InstallerCleanupService(
            downloadsRoot,
            daysOld: 30,
            deletionService);
        var preview = await service.PreviewAsync();

        var candidate = Assert.Single(preview);
        Assert.Equal(Path.GetFullPath(installerPath), candidate.Path);
        Assert.Equal(37, candidate.SizeBytes);

        var removals = await service.RemoveAsync(preview);
        var removal = Assert.Single(removals);
        Assert.True(removal.Succeeded, removal.Message);
        Assert.False(File.Exists(installerPath));
    }

    private static async Task VerifyProjectPurgeAsync(
        string fixtureRoot,
        ISafeDeletionService deletionService)
    {
        var projectsRoot = Path.Combine(fixtureRoot, "Projects");
        var projectRoot = Path.Combine(projectsRoot, "Sample Project");
        var artifactRoot = Path.Combine(projectRoot, "node_modules");
        Directory.CreateDirectory(artifactRoot);
        await File.WriteAllTextAsync(Path.Combine(projectRoot, "package.json"), "{}");
        await File.WriteAllBytesAsync(Path.Combine(artifactRoot, "cache.bin"), new byte[53]);

        var service = new PurgeArtifactService(
            projectsRoot,
            Path.Combine(fixtureRoot, "missing-purge-roots.txt"),
            deletionService);
        var preview = await service.PreviewAsync([projectsRoot]);

        var project = Assert.Single(preview);
        Assert.Equal("Sample Project", project.Name);
        var artifact = Assert.Single(project.Artifacts);
        Assert.Equal("node_modules", artifact.Name);
        Assert.Equal(53, artifact.SizeBytes);

        var removals = await service.RemoveAsync(preview);
        var removal = Assert.Single(removals);
        Assert.True(removal.Succeeded, removal.Message);
        Assert.False(Directory.Exists(artifactRoot));
        Assert.True(File.Exists(Path.Combine(projectRoot, "package.json")));
    }

    private static async Task VerifyLeftoverCleanupAsync(
        string localAppDataRoot,
        ISafeDeletionService deletionService)
    {
        var leftoverRoot = Path.Combine(localAppDataRoot, "DemoApp");
        Directory.CreateDirectory(leftoverRoot);
        var cachePath = Path.Combine(leftoverRoot, "cache.bin");
        await File.WriteAllBytesAsync(cachePath, new byte[29]);

        var service = new WindowsInstalledApplicationService(deletionService);
        var removals = await service.RemoveLeftoversAsync(
            [new LeftoverCandidate("Local app data", leftoverRoot, 29)]);

        var removal = Assert.Single(removals);
        Assert.True(removal.Succeeded, removal.Message);
        Assert.False(Directory.Exists(leftoverRoot));
    }

    private static void AssertControlledChild(string path, string expectedParent)
    {
        var fullPath = Path.GetFullPath(path);
        var fullParent = Path
            .GetFullPath(expectedParent)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        Assert.StartsWith(
            fullParent + Path.DirectorySeparatorChar,
            fullPath,
            StringComparison.OrdinalIgnoreCase);
        Assert.NotEqual(fullParent, fullPath);
    }

    private static void DeleteControlledTree(string path, string expectedParent)
    {
        AssertControlledChild(path, expectedParent);
        if (!Directory.Exists(path))
        {
            return;
        }

        Assert.False(
            (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0,
            "Controlled cleanup roots must never be reparse points.");
        Directory.Delete(path, recursive: true);
    }
}
