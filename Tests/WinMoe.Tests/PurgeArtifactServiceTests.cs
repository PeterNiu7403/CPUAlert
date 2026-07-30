using WinMoe.Services;
using Xunit;

namespace WinMoe.Tests;

public sealed class PurgeArtifactServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "WinMoePurgeTests", Guid.NewGuid().ToString("N"));
    private readonly string _outsideRoot = Path.Combine(Path.GetTempPath(), "WinMoePurgeOutsideTests", Guid.NewGuid().ToString("N"));

    public PurgeArtifactServiceTests()
    {
        Directory.CreateDirectory(_root);
    }

    [Fact]
    public async Task PreviewAsync_ReturnsProjectsWithArtifactsLargestFirst()
    {
        CreateFile(Path.Combine(_root, "SmallProject", "package.json"), 2);
        CreateFile(Path.Combine(_root, "SmallProject", "node_modules", "a.bin"), 10);
        CreateFile(Path.Combine(_root, "LargeProject", "LargeProject.csproj"), 2);
        CreateFile(Path.Combine(_root, "LargeProject", "bin", "app.dll"), 90);
        CreateFile(Path.Combine(_root, "LargeProject", "build.log"), 12);
        CreateFile(Path.Combine(_root, "NoArtifacts", "package.json"), 2);

        var service = new PurgeArtifactService(_root, Path.Combine(_root, "missing.txt"));

        var projects = await service.PreviewAsync([_root]);

        Assert.Equal(2, projects.Count);
        Assert.Equal("LargeProject", projects[0].Name);
        Assert.Equal(102, projects[0].TotalSizeBytes);
        Assert.Equal(2, projects[0].ArtifactCount);
        Assert.Equal("SmallProject", projects[1].Name);
        Assert.Contains(projects[0].Artifacts, artifact => artifact.Name == "bin");
        Assert.Contains(projects[0].Artifacts, artifact => artifact.Name == "build.log");
    }

    [Fact]
    public async Task RemoveAsync_RemovesOnlyPreviewArtifacts()
    {
        var sourceFile = Path.Combine(_root, "Project", "package.json");
        var artifactFile = Path.Combine(_root, "Project", "node_modules", "a.bin");
        CreateFile(sourceFile, 2);
        CreateFile(artifactFile, 10);
        var deletionService = new RecordingSafeDeletionService();
        var service = new PurgeArtifactService(_root, Path.Combine(_root, "missing.txt"), deletionService);
        var projects = await service.PreviewAsync([_root]);

        var results = await service.RemoveAsync(projects);

        Assert.Single(results);
        Assert.True(results[0].Succeeded);
        Assert.True(Directory.Exists(Path.Combine(_root, "Project", "node_modules")));
        Assert.True(File.Exists(sourceFile));
        Assert.Single(deletionService.DeletedPaths);
        Assert.Equal(Path.GetFullPath(Path.Combine(_root, "Project", "node_modules")), deletionService.DeletedPaths[0]);
    }

    [Fact]
    public async Task PreviewAsync_RequiresMarkersForGenericArtifactNames()
    {
        CreateFile(Path.Combine(_root, "NodeProject", "package.json"), 2);
        CreateFile(Path.Combine(_root, "NodeProject", "node_modules", "cache.bin"), 10);
        CreateFile(Path.Combine(_root, "NodeProject", "build", "bundle.js"), 20);
        CreateFile(Path.Combine(_root, "DotNetProject", "App.csproj"), 2);
        CreateFile(Path.Combine(_root, "DotNetProject", "obj", "cache.bin"), 30);
        CreateFile(Path.Combine(_root, "CargoProject", "Cargo.toml"), 2);
        CreateFile(Path.Combine(_root, "CargoProject", "target", "cache.bin"), 40);

        var service = new PurgeArtifactService(_root, Path.Combine(_root, "missing.txt"));

        var projects = await service.PreviewAsync([_root]);

        var node = Assert.Single(projects, project => project.Name == "NodeProject");
        Assert.DoesNotContain(node.Artifacts, artifact => artifact.Name == "build");

        var dotnet = Assert.Single(projects, project => project.Name == "DotNetProject");
        Assert.Contains(dotnet.Artifacts, artifact => artifact.Name == "obj");

        var cargo = Assert.Single(projects, project => project.Name == "CargoProject");
        Assert.Contains(cargo.Artifacts, artifact => artifact.Name == "target");
    }

    [Fact]
    public async Task PreviewAsync_IgnoresDirectoryLinksOutsideRootWhileDiscoveringNestedProjects()
    {
        CreateFile(Path.Combine(_root, "Nested", "InsideProject", "package.json"), 2);
        CreateFile(Path.Combine(_root, "Nested", "InsideProject", "node_modules", "inside.bin"), 10);
        CreateFile(Path.Combine(_outsideRoot, "OutsideProject", "package.json"), 2);
        CreateFile(Path.Combine(_outsideRoot, "OutsideProject", "node_modules", "outside.bin"), 100);
        CreateDirectoryJunction(Path.Combine(_root, "linked-outside"), _outsideRoot);
        var deletionService = new RecordingSafeDeletionService();
        var service = new PurgeArtifactService(
            _root,
            Path.Combine(_root, "missing.txt"),
            deletionService);

        var projects = await service.PreviewAsync([_root]);

        var project = Assert.Single(projects);
        Assert.Equal("InsideProject", project.Name);
        var artifact = Assert.Single(project.Artifacts);
        Assert.Equal("node_modules", artifact.Name);
        Assert.Equal(10, artifact.SizeBytes);
        Assert.Empty(deletionService.DeletedPaths);
    }

    [Fact]
    public async Task PreviewAsync_IgnoresLinkedArtifactsOutsideRoot()
    {
        CreateFile(Path.Combine(_root, "Project", "package.json"), 2);
        CreateFile(Path.Combine(_outsideRoot, "outside.bin"), 100);
        CreateDirectoryJunction(Path.Combine(_root, "Project", "node_modules"), _outsideRoot);
        var deletionService = new RecordingSafeDeletionService();
        var service = new PurgeArtifactService(
            _root,
            Path.Combine(_root, "missing.txt"),
            deletionService);

        var projects = await service.PreviewAsync([_root]);

        Assert.Empty(projects);
        Assert.Empty(deletionService.DeletedPaths);
    }

    [Fact]
    public async Task PreviewAsync_DoesNotMeasureFilesThroughNestedDirectoryLinks()
    {
        CreateFile(Path.Combine(_root, "Project", "package.json"), 2);
        CreateFile(Path.Combine(_root, "Project", "node_modules", "cache", "inside.bin"), 10);
        CreateFile(Path.Combine(_outsideRoot, "outside.bin"), 100);
        CreateDirectoryJunction(
            Path.Combine(_root, "Project", "node_modules", "linked-outside"),
            _outsideRoot);
        var deletionService = new RecordingSafeDeletionService();
        var service = new PurgeArtifactService(
            _root,
            Path.Combine(_root, "missing.txt"),
            deletionService);

        var projects = await service.PreviewAsync([_root]);

        var project = Assert.Single(projects);
        var artifact = Assert.Single(project.Artifacts);
        Assert.Equal("node_modules", artifact.Name);
        Assert.Equal(10, artifact.SizeBytes);
        Assert.Empty(deletionService.DeletedPaths);
    }

    [Fact]
    public async Task PreviewAsync_IgnoresDirectoryLinkAsSearchRoot()
    {
        CreateFile(Path.Combine(_outsideRoot, "OutsideProject", "package.json"), 2);
        CreateFile(Path.Combine(_outsideRoot, "OutsideProject", "node_modules", "outside.bin"), 100);
        var outsideLink = Path.Combine(_root, "linked-outside");
        CreateDirectoryJunction(outsideLink, _outsideRoot);
        var deletionService = new RecordingSafeDeletionService();
        var service = new PurgeArtifactService(
            _root,
            Path.Combine(_root, "missing.txt"),
            deletionService);

        var projects = await service.PreviewAsync([outsideLink]);

        Assert.Empty(projects);
        Assert.Empty(deletionService.DeletedPaths);
    }

    public void Dispose()
    {
        DeleteDirectoryJunction(Path.Combine(_root, "linked-outside"));
        DeleteDirectoryJunction(Path.Combine(_root, "Project", "node_modules", "linked-outside"));
        DeleteDirectoryJunction(Path.Combine(_root, "Project", "node_modules"));

        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }

        if (Directory.Exists(_outsideRoot))
        {
            Directory.Delete(_outsideRoot, recursive: true);
        }
    }

    private static void DeleteDirectoryJunction(string path)
    {
        if (Directory.Exists(path) &&
            (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
        {
            Directory.Delete(path);
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
