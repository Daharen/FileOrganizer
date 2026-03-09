using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace FileOrganizer.Core.Tests;

public sealed class DeterministicCollisionResolverTests
{
    private readonly DeterministicCollisionResolver _resolver = new();

    [Fact]
    public void ResolveDestinationPath_NoCollision_ReturnsOriginal()
    {
        var path = Path.Combine(Path.GetTempPath(), "file.txt");
        var reserved = new HashSet<string>(PathComparisonPolicy.PathComparer);

        var result = _resolver.ResolveDestinationPath(path, reserved, PathComparisonPolicy.PathComparison);

        Assert.Equal(path, result, PathComparisonPolicy.PathComparer);
    }

    [Fact]
    public void ResolveDestinationPath_WithCollision_AppendsNumericSuffix()
    {
        var path = Path.Combine(Path.GetTempPath(), "file.txt");
        var reserved = new HashSet<string>(PathComparisonPolicy.PathComparer) { path };

        var result = _resolver.ResolveDestinationPath(path, reserved, PathComparisonPolicy.PathComparison);

        Assert.Equal(Path.Combine(Path.GetDirectoryName(path)!, "file (1).txt"), result, PathComparisonPolicy.PathComparer);
    }

    [Fact]
    public void ResolveDestinationPath_WithMultipleCollisions_FindsNextAvailable()
    {
        var directory = Path.GetTempPath();
        var path = Path.Combine(directory, "file.txt");
        var reserved = new HashSet<string>(PathComparisonPolicy.PathComparer)
        {
            path,
            Path.Combine(directory, "file (1).txt"),
            Path.Combine(directory, "file (2).txt")
        };

        var result = _resolver.ResolveDestinationPath(path, reserved, PathComparisonPolicy.PathComparison);

        Assert.Equal(Path.Combine(directory, "file (3).txt"), result, PathComparisonPolicy.PathComparer);
    }

    [Fact]
    public void ResolveDestinationPath_NoExtension_SuffixesCorrectly()
    {
        var directory = Path.GetTempPath();
        var path = Path.Combine(directory, "file");
        var reserved = new HashSet<string>(PathComparisonPolicy.PathComparer) { path };

        var result = _resolver.ResolveDestinationPath(path, reserved, PathComparisonPolicy.PathComparison);

        Assert.Equal(Path.Combine(directory, "file (1)"), result, PathComparisonPolicy.PathComparer);
    }

    [Fact]
    public void ResolveDestinationPath_CaseInsensitiveComparison_HonorsProvidedSemantics()
    {
        var directory = Path.GetTempPath();
        var path = Path.Combine(directory, "File.txt");
        var reserved = new List<string> { Path.Combine(directory, "file.txt") };

        var result = _resolver.ResolveDestinationPath(path, reserved, StringComparison.OrdinalIgnoreCase);

        Assert.Equal(Path.Combine(directory, "File (1).txt"), result, StringComparer.OrdinalIgnoreCase);
    }
}
