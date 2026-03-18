using System;
using System.IO;
using Xunit;

namespace FileOrganizer.Core.Tests;

public sealed class UndoExecutorTests
{
    [Fact]
    public void Execute_RestoresFileWhenRestoreTargetAbsent()
    {
        var root = CreateTempDirectory();
        var currentPath = CreateFile(Path.Combine(root, "organized"), "a.txt");
        var targetPath = Path.Combine(root, "source", "a.txt");
        var executor = new UndoExecutor();

        var result = executor.Execute("run-1", root, new[] { CreateOperation(currentPath, targetPath) });

        Assert.Equal(1, result.Restored);
        Assert.True(File.Exists(targetPath));
        Assert.False(File.Exists(currentPath));
    }

    [Fact]
    public void Execute_SkipsWhenCurrentSourceMissing()
    {
        var root = CreateTempDirectory();
        var currentPath = Path.Combine(root, "organized", "missing.txt");
        var targetPath = Path.Combine(root, "source", "missing.txt");
        var executor = new UndoExecutor();

        var result = executor.Execute("run-1", root, new[] { CreateOperation(currentPath, targetPath) });

        Assert.Equal(1, result.Skipped);
        Assert.Contains(result.Messages, message => message.Contains("UNDO_SKIP", StringComparison.Ordinal));
    }

    [Fact]
    public void Execute_PreservesBothFilesOnRestoreCollision()
    {
        var root = CreateTempDirectory();
        var currentPath = CreateFile(Path.Combine(root, "organized"), "a.txt", "moved-content");
        var targetDirectory = Path.Combine(root, "source");
        Directory.CreateDirectory(targetDirectory);
        var targetPath = Path.Combine(targetDirectory, "a.txt");
        File.WriteAllText(targetPath, "existing-content");
        var executor = new UndoExecutor();

        var result = executor.Execute("run-1", root, new[] { CreateOperation(currentPath, targetPath) });
        var preservedPath = Path.Combine(targetDirectory, "a.undo-preserved.txt");

        Assert.Equal(1, result.CollisionPreserved);
        Assert.True(File.Exists(targetPath));
        Assert.Equal("existing-content", File.ReadAllText(targetPath));
        Assert.True(File.Exists(preservedPath));
        Assert.Equal("moved-content", File.ReadAllText(preservedPath));
    }

    [Fact]
    public void Execute_ContinuesBatchAfterOneFailure()
    {
        var root = CreateTempDirectory();
        var missingCurrent = Path.Combine(root, "organized", "missing.txt");
        var missingTarget = Path.Combine(root, "source", "missing.txt");
        var currentPath = CreateFile(Path.Combine(root, "organized"), "b.txt");
        var targetPath = Path.Combine(root, "source", "b.txt");
        var executor = new UndoExecutor();

        var result = executor.Execute("run-1", root, new[]
        {
            new UndoOperation("run-1", "op-1", missingCurrent, missingTarget, DateTimeOffset.UtcNow, 0),
            new UndoOperation("run-1", "op-2", currentPath, targetPath, DateTimeOffset.UtcNow, 1)
        });

        Assert.Equal(1, result.Skipped);
        Assert.Equal(1, result.Restored);
        Assert.True(File.Exists(targetPath));
    }

    [Fact]
    public void UndoCollisionResolver_DeterministicallyNamesPreservedCollisionFile()
    {
        var root = CreateTempDirectory();
        var targetDirectory = Path.Combine(root, "source");
        Directory.CreateDirectory(targetDirectory);
        var restoreTarget = Path.Combine(targetDirectory, "a.txt");
        var currentPath = Path.Combine(root, "organized", "a.txt");
        File.WriteAllText(restoreTarget, "existing");
        File.WriteAllText(Path.Combine(targetDirectory, "a.undo-preserved.txt"), "reserved");
        var resolver = new UndoCollisionResolver();

        var preservedPath = resolver.ResolvePreservedUndoPath(restoreTarget, currentPath);

        Assert.Equal(Path.Combine(targetDirectory, "a.undo-preserved (1).txt"), preservedPath);
    }

    private static UndoOperation CreateOperation(string currentPath, string targetPath)
        => new("run-1", "op-1", currentPath, targetPath, DateTimeOffset.UtcNow, 0);

    private static string CreateFile(string directory, string fileName, string content = "content")
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, fileName);
        File.WriteAllText(path, content);
        return path;
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
