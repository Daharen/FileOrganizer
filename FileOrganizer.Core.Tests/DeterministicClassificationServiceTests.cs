using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using FileOrganizer.Core.Classification;
using FileOrganizer.Core.Extraction;
using Xunit;

namespace FileOrganizer.Core.Tests;

public sealed class DeterministicClassificationServiceTests
{
    private readonly IClassificationService _service =
        new DeterministicClassificationService(new ExtractionService(new FileTypeDetector(), new ExtractionDispatcher([new TextFileExtractor()])));

    [Fact]
    public void RealPdfSignature_ClassifiesToDocuments()
    {
        var path = CreateTempFile("sample.pdf", Encoding.ASCII.GetBytes("%PDF-1.7 test"));

        var result = _service.Classify(path);

        Assert.Equal("StructuredDocument", result.DetectedType);
        Assert.Equal("Documents", result.SemanticCategory);
        Assert.Equal("Documents", result.SuggestedFolder);
        Assert.Equal("signature", result.AnalysisStage);
        Assert.True(result.ConfidenceScore > 0.9);
    }

    [Fact]
    public void TxtFile_UsesExtractionAndRoutesToDocuments()
    {
        var path = CreateTempFile("notes.txt", Encoding.UTF8.GetBytes("alpha\nbeta\ngamma"));

        var result = _service.Classify(path);

        Assert.Equal("TextDocument", result.DetectedType);
        Assert.Equal("Documents", result.SuggestedFolder);
        Assert.Equal("text_extraction", result.AnalysisStage);
        Assert.InRange(result.ConfidenceScore, 0.75, 0.9);
    }

    [Fact]
    public void CsFile_UsesExtractionAndRoutesToCode()
    {
        var path = CreateTempFile("Program.cs", Encoding.UTF8.GetBytes("using System;\nclass Program { static void Main() { } }"));

        var result = _service.Classify(path);

        Assert.Equal("CodeFile", result.DetectedType);
        Assert.Equal("Code", result.SemanticCategory);
        Assert.Equal("Code", result.SuggestedFolder);
        Assert.Equal("Program.cs", result.SuggestedFilename);
        Assert.Equal("text_extraction", result.AnalysisStage);
    }

    [Fact]
    public void StrongerSignature_WinsOverMisleadingExtensionWhenSafe()
    {
        var path = CreateTempFile("tricky.txt", Encoding.ASCII.GetBytes("%PDF-1.7 disguised"));

        var result = _service.Classify(path);

        Assert.Equal("StructuredDocument", result.DetectedType);
        Assert.Equal("Documents", result.SuggestedFolder);
        Assert.Equal("signature", result.AnalysisStage);
        Assert.DoesNotContain(".txt", result.ReasoningSummary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExtractionFailure_FallsBackToExtensionCategory()
    {
        var service = new DeterministicClassificationService(new ThrowingExtractionService());
        var path = CreateTempFile("archive.zip", new byte[] { 1, 2, 3, 4 });

        var result = service.Classify(path);

        Assert.Equal("Archives", result.SuggestedFolder);
        Assert.Equal("extension_fallback", result.AnalysisStage);
        Assert.Equal("archive.zip", result.SuggestedFilename);
        Assert.InRange(result.ConfidenceScore, 0.59, 0.61);
    }

    [Fact]
    public void PlannerOutput_RemainsDeterministicForSameInputs()
    {
        var root = CreateTempDirectory();
        var codePath = Path.Combine(root, "app.cs");
        var docPath = Path.Combine(root, "readme.txt");
        File.WriteAllText(codePath, "class App { }", Encoding.UTF8);
        File.WriteAllText(docPath, "hello", Encoding.UTF8);

        var files = new List<ScannedFile>
        {
            new() { SourcePath = codePath, RelativePath = "app.cs" },
            new() { SourcePath = docPath, RelativePath = "readme.txt" }
        };
        var classifications = new List<ClassificationResult>
        {
            _service.Classify(codePath),
            _service.Classify(docPath)
        };
        var planner = new DeterministicOrganizationPlanner();

        var first = planner.GetOrganizationPlan(root, files, classifications);
        var second = planner.GetOrganizationPlan(root, files, classifications);

        Assert.Equal(first.Operations.Count, second.Operations.Count);
        for (var i = 0; i < first.Operations.Count; i++)
        {
            Assert.Equal(first.Operations[i].SourcePath, second.Operations[i].SourcePath);
            Assert.Equal(first.Operations[i].DestinationDirectory, second.Operations[i].DestinationDirectory);
            Assert.Equal(first.Operations[i].ProposedFileName, second.Operations[i].ProposedFileName);
            Assert.Equal(first.Operations[i].Category, second.Operations[i].Category);
            Assert.Equal(first.Operations[i].ConfidenceScore, second.Operations[i].ConfidenceScore);
            Assert.Equal(first.Operations[i].ReasoningSummary, second.Operations[i].ReasoningSummary);
        }
    }

    private static string CreateTempFile(string fileName, byte[] content)
    {
        var directory = CreateTempDirectory();
        var path = Path.Combine(directory, fileName);
        File.WriteAllBytes(path, content);
        return path;
    }

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private sealed class ThrowingExtractionService : IExtractionService
    {
        public ExtractionArtifact Extract(string path) => throw new InvalidOperationException("boom");
    }
}
