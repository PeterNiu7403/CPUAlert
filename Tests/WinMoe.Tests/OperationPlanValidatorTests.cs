using WinMoe.Models;
using WinMoe.Services;
using Xunit;

namespace WinMoe.Tests;

public sealed class OperationPlanValidatorTests
{
    private static readonly DateTimeOffset CreatedAt =
        DateTimeOffset.Parse("2026-07-30T04:00:00Z");

    private readonly OperationPlanValidator _validator = new();

    [Fact]
    public void ValidateForApply_AcceptsCurrentExplicitlyConfirmedPlan()
    {
        var items = new[]
        {
            Item(@"C:\Users\Alice\AppData\Local\Temp\mole-cache.tmp", selected: true)
        };
        var plan = OperationPlan.Create("clean", items, CreatedAt, TimeSpan.FromMinutes(10));

        var result = _validator.ValidateForApply(
            plan,
            items,
            userConfirmed: true,
            CreatedAt.AddMinutes(1));

        Assert.True(result.IsValid, $"{result.Code}: {result.Message}");
        Assert.Equal(OperationPlanValidationCode.Valid, result.Code);
    }

    [Fact]
    public void ValidateForApply_RejectsMissingConfirmation()
    {
        var items = new[] { Item(@"C:\Users\Alice\AppData\Local\Temp\cache.tmp", selected: true) };
        var plan = OperationPlan.Create("clean", items, CreatedAt, TimeSpan.FromMinutes(10));

        var result = _validator.ValidateForApply(plan, items, false, CreatedAt.AddMinutes(1));

        Assert.Equal(OperationPlanValidationCode.ConfirmationRequired, result.Code);
    }

    [Fact]
    public void ValidateForApply_RejectsExpiredPlan()
    {
        var items = new[] { Item(@"C:\Users\Alice\AppData\Local\Temp\cache.tmp", selected: true) };
        var plan = OperationPlan.Create("clean", items, CreatedAt, TimeSpan.FromMinutes(10));

        var result = _validator.ValidateForApply(plan, items, true, CreatedAt.AddMinutes(11));

        Assert.Equal(OperationPlanValidationCode.Expired, result.Code);
    }

    [Fact]
    public void ValidateForApply_RejectsChangedTargets()
    {
        var items = new[] { Item(@"C:\Users\Alice\AppData\Local\Temp\cache.tmp", selected: true) };
        var plan = OperationPlan.Create("clean", items, CreatedAt, TimeSpan.FromMinutes(10));
        var changed = new[] { items[0] with { SizeBytes = items[0].SizeBytes + 1 } };

        var result = _validator.ValidateForApply(plan, changed, true, CreatedAt.AddMinutes(1));

        Assert.Equal(OperationPlanValidationCode.ContentChanged, result.Code);
    }

    [Fact]
    public void ValidateForApply_RejectsFileSystemRoot()
    {
        var path = Path.GetPathRoot(Environment.CurrentDirectory)!;
        var items = new[] { Item(path, selected: true) };
        var plan = OperationPlan.Create("clean", items, CreatedAt, TimeSpan.FromMinutes(10));

        var result = _validator.ValidateForApply(plan, items, true, CreatedAt.AddMinutes(1));

        Assert.Equal(OperationPlanValidationCode.UnsafeTarget, result.Code);
    }

    [Fact]
    public void ValidateForApply_RejectsWindowsDirectory()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            "Temp",
            "cache.tmp");
        var items = new[] { Item(path, selected: true) };
        var plan = OperationPlan.Create("clean", items, CreatedAt, TimeSpan.FromMinutes(10));

        var result = _validator.ValidateForApply(plan, items, true, CreatedAt.AddMinutes(1));

        Assert.Equal(OperationPlanValidationCode.UnsafeTarget, result.Code);
    }

    [Theory]
    [InlineData(@"\\server\share\cache.tmp")]
    [InlineData(@"\\?\C:\WinMoeFixture\cache.tmp")]
    [InlineData(@"\\.\C:\WinMoeFixture\cache.tmp")]
    public void ValidateForApply_RejectsRemoteAndDevicePaths(string path)
    {
        var items = new[] { Item(path, selected: true) };
        var plan = OperationPlan.Create("clean", items, CreatedAt, TimeSpan.FromMinutes(10));

        var result = _validator.ValidateForApply(plan, items, true, CreatedAt.AddMinutes(1));

        Assert.Equal(OperationPlanValidationCode.UnsafeTarget, result.Code);
    }

    [Fact]
    public void ValidateForApply_RejectsRelativePath()
    {
        var items = new[] { Item(@"relative\cache.tmp", selected: true) };
        var plan = OperationPlan.Create("clean", items, CreatedAt, TimeSpan.FromMinutes(10));

        var result = _validator.ValidateForApply(plan, items, true, CreatedAt.AddMinutes(1));

        Assert.Equal(OperationPlanValidationCode.UnsafeTarget, result.Code);
    }

    [Fact]
    public void ValidateForApply_RejectsEmptySelection()
    {
        var items = new[] { Item(@"C:\Users\Alice\AppData\Local\Temp\cache.tmp", selected: false) };
        var plan = OperationPlan.Create("clean", items, CreatedAt, TimeSpan.FromMinutes(10));

        var result = _validator.ValidateForApply(plan, items, true, CreatedAt.AddMinutes(1));

        Assert.Equal(OperationPlanValidationCode.EmptySelection, result.Code);
    }

    private static OperationPlanItem Item(string path, bool selected)
    {
        return new OperationPlanItem(
            Guid.NewGuid().ToString("N"),
            "Cache",
            path,
            1024,
            OperationRisk.Low,
            selected);
    }
}
