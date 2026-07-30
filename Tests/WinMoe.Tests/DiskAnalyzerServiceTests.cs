using WinMoe.Models;
using WinMoe.Services;
using Xunit;

namespace WinMoe.Tests;

public sealed class DiskAnalyzerServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "WinMoeDiskTests", Guid.NewGuid().ToString("N"));
    private readonly string _outsideRoot = Path.Combine(Path.GetTempPath(), "WinMoeDiskOutsideTests", Guid.NewGuid().ToString("N"));

    public DiskAnalyzerServiceTests()
    {
        Directory.CreateDirectory(_root);
    }

    [Fact]
    public async Task AnalyzeAsync_ReturnsTotalSizeAndLargestChildrenFirst()
    {
        CreateFile(Path.Combine(_root, "root.bin"), 10);
        CreateFile(Path.Combine(_root, "Large", "a.bin"), 200);
        CreateFile(Path.Combine(_root, "Small", "b.bin"), 50);

        var service = new DiskAnalyzerService();

        var node = await service.AnalyzeAsync(_root, new DiskAnalysisOptions(MaxDepth: 1, MaxChildrenPerNode: 10));

        Assert.Equal(260, node.SizeBytes);
        Assert.Equal(2, node.Children.Count);
        Assert.Equal("Large", node.Children[0].Name);
        Assert.Equal(200, node.Children[0].SizeBytes);
        Assert.Equal("Small", node.Children[1].Name);
        Assert.Equal(50, node.Children[1].SizeBytes);
    }

    [Fact]
    public async Task AnalyzeAsync_RespectsChildLimit()
    {
        CreateFile(Path.Combine(_root, "A", "a.bin"), 10);
        CreateFile(Path.Combine(_root, "B", "b.bin"), 30);
        CreateFile(Path.Combine(_root, "C", "c.bin"), 20);

        var service = new DiskAnalyzerService();

        var node = await service.AnalyzeAsync(_root, new DiskAnalysisOptions(MaxDepth: 1, MaxChildrenPerNode: 2));

        Assert.Equal(2, node.Children.Count);
        Assert.Equal("B", node.Children[0].Name);
        Assert.Equal("C", node.Children[1].Name);
    }

    [Fact]
    public async Task AnalyzeAsync_IgnoresDirectoryLinksOutsideRootWhileScanningNestedDirectories()
    {
        CreateFile(Path.Combine(_root, "Nested", "Deeper", "inside.bin"), 25);
        CreateFile(Path.Combine(_outsideRoot, "outside.bin"), 100);
        CreateDirectoryJunction(Path.Combine(_root, "linked-outside"), _outsideRoot);

        var service = new DiskAnalyzerService();

        var node = await service.AnalyzeAsync(_root, new DiskAnalysisOptions(MaxDepth: 2, MaxChildrenPerNode: 10));

        Assert.Equal(25, node.SizeBytes);
        var nested = Assert.Single(node.Children);
        Assert.Equal("Nested", nested.Name);
        var deeper = Assert.Single(nested.Children);
        Assert.Equal("Deeper", deeper.Name);
        Assert.Equal(25, deeper.SizeBytes);
    }

    [Fact]
    public async Task AnalyzeAsync_RejectsDirectoryLinkAsRoot()
    {
        CreateFile(Path.Combine(_outsideRoot, "outside.bin"), 100);
        var outsideLink = Path.Combine(_root, "linked-outside");
        CreateDirectoryJunction(outsideLink, _outsideRoot);
        var service = new DiskAnalyzerService();

        var exception = await Assert.ThrowsAsync<IOException>(
            () => service.AnalyzeAsync(
                outsideLink,
                new DiskAnalysisOptions(MaxDepth: 2, MaxChildrenPerNode: 10)));

        Assert.Contains("reparse point", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AnalyzeAsync_IgnoresFileLinksOutsideRoot()
    {
        CreateFile(Path.Combine(_root, "inside.bin"), 7);
        var outsideFile = Path.Combine(_outsideRoot, "outside.bin");
        var outsideLink = Path.Combine(_root, "outside-file-link.bin");
        CreateFile(outsideFile, 101);
        try
        {
            File.CreateSymbolicLink(outsideLink, outsideFile);
        }
        catch (Exception ex) when (
            ex is IOException or UnauthorizedAccessException &&
            (ex.HResult & 0xFFFF) == 1314)
        {
            // This machine does not grant CreateSymbolicLink privilege. The attribute
            // policy below remains covered without weakening CI on such hosts.
            return;
        }

        Assert.True(
            (File.GetAttributes(outsideLink) & FileAttributes.ReparsePoint) != 0,
            "The test fixture must be a file reparse point.");
        var service = new DiskAnalyzerService();

        var node = await service.AnalyzeAsync(
            _root,
            new DiskAnalysisOptions(MaxDepth: 1, MaxChildrenPerNode: 10));

        Assert.Equal(7, node.SizeBytes);
    }

    [Theory]
    [InlineData(FileAttributes.Normal, true)]
    [InlineData(FileAttributes.Archive, true)]
    [InlineData(FileAttributes.ReparsePoint, false)]
    [InlineData(FileAttributes.Archive | FileAttributes.ReparsePoint, false)]
    public void ShouldIncludeFile_RejectsFileReparsePoints(
        FileAttributes attributes,
        bool expected)
    {
        Assert.Equal(expected, DiskAnalyzerService.ShouldIncludeFile(attributes));
    }

    [Fact]
    public async Task AnalyzeAsync_HandlesUnicodeSpacesAndLongPaths()
    {
        var nestedPath = _root;
        for (var index = 0; index < 7; index++)
        {
            nestedPath = Path.Combine(
                nestedPath,
                $"第 {index} 层 long path segment xxxxxxxxxxxxx");
        }

        var filePath = Path.Combine(nestedPath, "缓存 数据.bin");
        Assert.True(filePath.Length > 260, $"Fixture path was only {filePath.Length} characters.");
        CreateFile(filePath, 17);
        var service = new DiskAnalyzerService();

        var node = await service.AnalyzeAsync(
            _root,
            new DiskAnalysisOptions(MaxDepth: 8, MaxChildrenPerNode: 10));

        Assert.Equal(17, node.SizeBytes);
    }

    [Fact]
    public async Task AnalyzeAsync_ReadsSizeOfExclusivelyLockedFileWithoutOpeningItsContents()
    {
        var filePath = Path.Combine(_root, "locked metadata.bin");
        CreateFile(filePath, 23);
        using var lockStream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.ReadWrite,
            FileShare.None);
        var service = new DiskAnalyzerService();

        var node = await service.AnalyzeAsync(
            _root,
            new DiskAnalysisOptions(MaxDepth: 1, MaxChildrenPerNode: 10));

        Assert.Equal(23, node.SizeBytes);
    }

    public void Dispose()
    {
        var outsideFileLink = Path.Combine(_root, "outside-file-link.bin");
        if (File.Exists(outsideFileLink))
        {
            File.Delete(outsideFileLink);
        }

        var outsideLink = Path.Combine(_root, "linked-outside");
        if (Directory.Exists(outsideLink))
        {
            Directory.Delete(outsideLink);
        }

        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }

        if (Directory.Exists(_outsideRoot))
        {
            Directory.Delete(_outsideRoot, recursive: true);
        }
    }

    private static void CreateFile(string path, int bytes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, Enumerable.Repeat((byte)1, bytes).ToArray());
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
