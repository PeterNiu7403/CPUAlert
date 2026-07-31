using WinMoe.Models;
using WinMoe.Services;
using Xunit;

namespace WinMoe.Tests;

/// <summary>
/// Opt-in machine proof that the Clean page's native scan → plan validation →
/// recycle-bin apply loop works end to end on a controlled temp fixture
/// (the same production services CleanupViewModel composes). Set
/// WINMOE_RUN_MACHINE_DELETE_TESTS=1 to enable.
/// </summary>
public sealed class MachineCleanFlowIntegrationTests
{
    private const string EnableVariable = "WINMOE_RUN_MACHINE_DELETE_TESTS";

    [Fact]
    [Trait("Category", "Machine")]
    public async Task CleanFlow_ScanValidateApply_RecyclesControlledTempFixture()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable(EnableVariable),
                "1",
                StringComparison.Ordinal))
        {
            return;
        }

        var fixtureId = Guid.NewGuid().ToString("N");
        var tempRoot = Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var fixtureRoot = Path.Combine(tempRoot, "WinMoeCleanE2E-" + fixtureId);
        var freshRoot = Path.Combine(tempRoot, "WinMoeCleanE2E-fresh-" + fixtureId);
        Assert.StartsWith(
            tempRoot + Path.DirectorySeparatorChar,
            Path.GetFullPath(fixtureRoot),
            StringComparison.OrdinalIgnoreCase);

        Directory.CreateDirectory(fixtureRoot);
        Directory.CreateDirectory(freshRoot);
        var junkPath = Path.Combine(fixtureRoot, "junk 测试.bin");
        var sentinelPath = Path.Combine(freshRoot, "fresh-sentinel.txt");
        await File.WriteAllBytesAsync(junkPath, new byte[4096]);
        await File.WriteAllTextAsync(sentinelPath, "fresh sentinel");
        // Only entries older than 24h are offered; backdate the fixture.
        File.SetLastWriteTime(junkPath, DateTime.Now.AddDays(-2));
        Directory.SetLastWriteTime(fixtureRoot, DateTime.Now.AddDays(-2));

        try
        {
            // 1. Native temp selection discovers the backdated fixture and skips the fresh one.
            var entries = CleanupScanService.SelectTempEntries(
                tempRoot,
                TimeSpan.FromHours(24),
                maxEntries: int.MaxValue);
            var fixtureEntry = entries.FirstOrDefault(
                candidate => string.Equals(candidate.Path, fixtureRoot, StringComparison.OrdinalIgnoreCase));
            Assert.NotEqual(default, fixtureEntry);
            Assert.True(fixtureEntry.SizeBytes >= 4096, $"fixture size expected, got {fixtureEntry.SizeBytes}");
            Assert.DoesNotContain(
                entries,
                candidate => string.Equals(candidate.Path, freshRoot, StringComparison.OrdinalIgnoreCase));

            // Full scan must also stay well-formed (capped review list is by design).
            var scanService = new CleanupScanService();
            var items = await scanService.ScanAsync();
            Assert.All(items, item => Assert.True(item.SizeBytes > 0));

            // 2. The same operation-plan contract the Clean page uses must accept the fixture.
            var planItem = new OperationPlanItem(
                Id: "0:" + fixtureEntry.Path,
                Title: "用户临时文件",
                TargetPath: fixtureEntry.Path,
                SizeBytes: fixtureEntry.SizeBytes,
                Risk: OperationRisk.Low,
                IsSelected: true);
            var plan = OperationPlan.Create("clean", [planItem], DateTimeOffset.UtcNow, TimeSpan.FromMinutes(15));
            var validation = new OperationPlanValidator().ValidateForApply(
                plan,
                [planItem],
                userConfirmed: true,
                DateTimeOffset.UtcNow);
            Assert.True(validation.IsValid, validation.Message);
            Assert.True(OperationPlanValidator.IsConcreteDeletablePath(fixtureEntry.Path));

            // 3. Recycle-bin apply removes the fixture; the fresh entry is untouched.
            var result = new RecycleBinDeletionService().DeleteFileOrDirectory(
                fixtureEntry.Path,
                fixtureEntry.SizeBytes);
            Assert.True(result.Succeeded, result.Message);
            Assert.False(Directory.Exists(fixtureRoot));
            Assert.True(File.Exists(sentinelPath));
            Assert.Equal("fresh sentinel", await File.ReadAllTextAsync(sentinelPath));
        }
        finally
        {
            if (Directory.Exists(fixtureRoot))
            {
                Directory.Delete(fixtureRoot, recursive: true);
            }

            if (Directory.Exists(freshRoot))
            {
                Directory.Delete(freshRoot, recursive: true);
            }
        }
    }
}
