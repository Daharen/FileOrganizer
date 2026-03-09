using System;
using System.IO;
using System.Linq;
using Xunit;

namespace FileOrganizer.Core.Tests;

public sealed class OperationPlanValidatorCollisionIntegrationTests
{
    [Fact]
    public void Validate_TwoPlannedOperationsSameDestination_BothApprovedWithDistinctResolvedPaths()
    {
        using var fixture = new TestDirectoryFixture();
        var sourceA = fixture.CreateFile("source-a.txt", "a");
        var sourceB = fixture.CreateFile("source-b.txt", "b");

        var plan = new OrganizationPlan();
        plan.Operations.Add(new FileMoveOperation { OperationId = "op1", SourcePath = sourceA, DestinationDirectory = fixture.RootPath, ProposedFileName = "target.txt" });
        plan.Operations.Add(new FileMoveOperation { OperationId = "op2", SourcePath = sourceB, DestinationDirectory = fixture.RootPath, ProposedFileName = "target.txt" });

        var validator = new OperationPlanValidator();
        var result = validator.Validate(fixture.RootPath, plan);

        Assert.Equal(2, result.ApprovedOperations.Count);
        Assert.Empty(result.RejectedOperations);
        Assert.Equal(Path.Combine(fixture.RootPath, "target.txt"), result.ApprovedOperations[0].DestinationPath, PathComparisonPolicy.PathComparer);
        Assert.Equal(Path.Combine(fixture.RootPath, "target (1).txt"), result.ApprovedOperations[1].DestinationPath, PathComparisonPolicy.PathComparer);
        Assert.False(result.ApprovedOperations[0].CollisionResolutionApplied);
        Assert.True(result.ApprovedOperations[1].CollisionResolutionApplied);
    }

    [Fact]
    public void Validate_ExistingDestinationOnDisk_ApprovesWithResolvedPath()
    {
        using var fixture = new TestDirectoryFixture();
        var source = fixture.CreateFile("source.txt", "a");
        fixture.CreateFile("target.txt", "existing");

        var plan = new OrganizationPlan();
        plan.Operations.Add(new FileMoveOperation { OperationId = "op1", SourcePath = source, DestinationDirectory = fixture.RootPath, ProposedFileName = "target.txt" });

        var validator = new OperationPlanValidator();
        var result = validator.Validate(fixture.RootPath, plan);

        Assert.Single(result.ApprovedOperations);
        Assert.Equal(Path.Combine(fixture.RootPath, "target (1).txt"), result.ApprovedOperations[0].DestinationPath, PathComparisonPolicy.PathComparer);
        Assert.True(result.ApprovedOperations[0].CollisionResolutionApplied);
    }

    [Fact]
    public void Validate_ResolvedPathOutsideAuthorizedRoot_RemainsRejected()
    {
        using var fixture = new TestDirectoryFixture();
        var source = fixture.CreateFile("source.txt", "a");

        var plan = new OrganizationPlan();
        plan.Operations.Add(new FileMoveOperation
        {
            OperationId = "op1",
            SourcePath = source,
            DestinationDirectory = Path.Combine(fixture.RootPath, "..", "outside"),
            ProposedFileName = "target.txt"
        });

        var validator = new OperationPlanValidator();
        var result = validator.Validate(fixture.RootPath, plan);

        Assert.Empty(result.ApprovedOperations);
        Assert.Single(result.RejectedOperations);
        Assert.Equal(ValidationFailureCode.DestinationOutsideAuthorizedRoot, result.RejectedOperations[0].Code);
    }

    [Fact]
    public void Validate_ProtectedRepositoryDestination_RemainsRejected()
    {
        using var fixture = new TestDirectoryFixture();
        var source = fixture.CreateFile("source.txt", "a");
        var repoFolder = fixture.CreateDirectory("repo");
        fixture.CreateFile(Path.Combine("repo", "package.json"), "{}");

        var plan = new OrganizationPlan();
        plan.Operations.Add(new FileMoveOperation
        {
            OperationId = "op1",
            SourcePath = source,
            DestinationDirectory = repoFolder,
            ProposedFileName = "target.txt"
        });

        var validator = new OperationPlanValidator();
        var result = validator.Validate(fixture.RootPath, plan);

        Assert.Empty(result.ApprovedOperations);
        Assert.Single(result.RejectedOperations.Where(r => r.Code == ValidationFailureCode.RepositoryProtected));
    }

    private sealed class TestDirectoryFixture : IDisposable
    {
        public string RootPath { get; } = Path.Combine(Path.GetTempPath(), "fileorganizer-tests", Guid.NewGuid().ToString("N"));

        public TestDirectoryFixture()
        {
            Directory.CreateDirectory(RootPath);
        }

        public string CreateDirectory(string relativePath)
        {
            var path = Path.Combine(RootPath, relativePath);
            Directory.CreateDirectory(path);
            return path;
        }

        public string CreateFile(string relativePath, string contents)
        {
            var path = Path.Combine(RootPath, relativePath);
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(path, contents);
            return path;
        }

        public void Dispose()
        {
            if (Directory.Exists(RootPath))
            {
                Directory.Delete(RootPath, recursive: true);
            }
        }
    }
}
