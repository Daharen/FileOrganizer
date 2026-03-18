using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using FileOrganizer.Core.Classification;
using FileOrganizer.Core.Extraction;
using Xunit;

namespace FileOrganizer.Core.Tests;

public sealed class DeterministicClassificationServiceTests
{
    private readonly IClassificationService _service =
        new DeterministicClassificationService(new ExtractionService(new FileTypeDetector(), new ExtractionDispatcher([new TextFileExtractor(), new PdfFileExtractor(), new OpenXmlContainerExtractor()])));

    [Fact]
    public void InvoiceLikeText_RoutesToDocumentsFinance()
    {
        var result = ClassifyText("invoice.txt", "Invoice Number: INV-1001\nBill To: Example Corp\nSubtotal: 100.00\nTax: 8.00\nAmount Due: $108.00\nTotal: $108.00");

        Assert.Equal("Invoice", result.SemanticCategory);
        Assert.Equal("Documents/Finance", result.SuggestedFolder);
        Assert.Equal("heuristic_content", result.AnalysisStage);
        Assert.Equal("invoice.txt", result.SuggestedFilename);
        Assert.DoesNotContain("Invoice Number", result.ReasoningSummary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResumeLikeText_RoutesToDocumentsCareer()
    {
        var result = ClassifyText("resume.txt", "Professional Summary\nExperience\nEmployment History\nEducation\nSkills\nReferences");

        Assert.Equal("Resume", result.SemanticCategory);
        Assert.Equal("Documents/Career", result.SuggestedFolder);
        Assert.Equal("heuristic_content", result.AnalysisStage);
    }

    [Fact]
    public void NotesLikeText_RoutesToDocumentsNotes()
    {
        var result = ClassifyText("meeting-notes.txt", "2026-03-18\nAgenda\n- Notes from weekly meeting\n- Todo review\n- Action items for follow-up");

        Assert.Equal("Notes", result.SemanticCategory);
        Assert.Equal("Documents/Notes", result.SuggestedFolder);
        Assert.Equal("heuristic_content", result.AnalysisStage);
    }

    [Fact]
    public void ReportLikeText_RoutesToDocumentsReports()
    {
        var result = ClassifyText("quarterly-report.txt", "# Executive Summary\n# Introduction\n# Methodology\n# Results\n# Conclusion\nDetailed findings are listed below.");

        Assert.Equal("Report", result.SemanticCategory);
        Assert.Equal("Documents/Reports", result.SuggestedFolder);
        Assert.Equal("heuristic_content", result.AnalysisStage);
    }

    [Fact]
    public void JsonConfiguration_RoutesToCodeConfig()
    {
        var result = ClassifyText("appsettings.json", "{\n  \"host\": \"localhost\",\n  \"port\": 5432,\n  \"enabled\": true,\n  \"timeout\": 30,\n  \"connectionString\": \"Server=db;\"\n}");

        Assert.Equal("Configuration", result.SemanticCategory);
        Assert.Equal("Code/Config", result.SuggestedFolder);
        Assert.Equal("heuristic_content", result.AnalysisStage);
    }

    [Fact]
    public void CsvDataExport_RoutesToData()
    {
        var result = ClassifyText("customers.csv", "id,email,status,created_at,updated_at\n1,a@example.com,active,2026-01-01,2026-01-02\n2,b@example.com,inactive,2026-01-03,2026-01-04\n3,c@example.com,active,2026-01-05,2026-01-06");

        Assert.Equal("DataExport", result.SemanticCategory);
        Assert.Equal("Data", result.SuggestedFolder);
        Assert.Equal("heuristic_content", result.AnalysisStage);
    }

    [Fact]
    public void AmbiguousText_DoesNotOverClassify()
    {
        var result = ClassifyText("scratch.txt", "hello\nthis is a short file\nwith no strong routing signal");

        Assert.Equal("Documents", result.SemanticCategory);
        Assert.Equal("Documents", result.SuggestedFolder);
        Assert.Equal("signature", result.AnalysisStage);
    }

    [Fact]
    public void RealPdfSignature_ClassifiesToDocuments()
    {
        var path = CreateTempFile("sample.pdf", Encoding.ASCII.GetBytes("%PDF-1.7\n1 0 obj << /Type /Page >>\n/Title (Quarterly Report)"));

        var result = _service.Classify(path);

        Assert.Equal("StructuredDocument", result.DetectedType);
        Assert.Equal("Documents", result.SemanticCategory);
        Assert.Equal("Documents", result.SuggestedFolder);
        Assert.Equal("pdf_extraction", result.AnalysisStage);
        Assert.True(result.ConfidenceScore > 0.9);
    }

    [Fact]
    public void CsFile_UsesDeterministicRoutingAndRoutesToCode()
    {
        var path = CreateTempFile("Program.cs", Encoding.UTF8.GetBytes("using System;\nclass Program { static void Main() { } }"));

        var result = _service.Classify(path);

        Assert.Equal("CodeFile", result.DetectedType);
        Assert.Equal("Code", result.SemanticCategory);
        Assert.Equal("Code", result.SuggestedFolder);
        Assert.Equal("Program.cs", result.SuggestedFilename);
        Assert.Equal("signature", result.AnalysisStage);
    }

    [Fact]
    public void StrongerSignature_WinsOverMisleadingExtensionWhenSafe()
    {
        var path = CreateTempFile("tricky.txt", Encoding.ASCII.GetBytes("%PDF-1.7 disguised /Type /Page"));

        var result = _service.Classify(path);

        Assert.Equal("StructuredDocument", result.DetectedType);
        Assert.Equal("Documents", result.SuggestedFolder);
        Assert.Equal("pdf_extraction", result.AnalysisStage);
        Assert.DoesNotContain(".txt", result.ReasoningSummary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void StructuredDocumentContainer_IsPreferredOverArchiveFallback()
    {
        var path = CreateZipFile("report.zip", "[Content_Types].xml", "word/document.xml", "docProps/core.xml");

        var result = _service.Classify(path);

        Assert.Equal("StructuredDocument", result.DetectedType);
        Assert.Equal("Documents", result.SuggestedFolder);
        Assert.Equal("container_structure", result.AnalysisStage);
        Assert.Contains("OpenXML", result.ReasoningSummary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GenericZip_StillRoutesAsArchive()
    {
        var path = CreateZipFile("archive.zip", "folder/file.txt");

        var result = _service.Classify(path);

        Assert.Equal("Archive", result.DetectedType);
        Assert.Equal("Archives", result.SuggestedFolder);
        Assert.Equal("signature", result.AnalysisStage);
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
        var invoicePath = Path.Combine(root, "invoice.txt");
        var reportPath = Path.Combine(root, "report.txt");
        File.WriteAllText(invoicePath, "Invoice Number: 10\nBill To: Co\nSubtotal: 12.00\nTax: 1.00\nAmount Due: $13.00\nTotal: $13.00", Encoding.UTF8);
        File.WriteAllText(reportPath, "# Executive Summary\n# Introduction\n# Methodology\n# Results\n# Conclusion", Encoding.UTF8);

        var files = new List<ScannedFile>
        {
            new() { SourcePath = invoicePath, RelativePath = "invoice.txt" },
            new() { SourcePath = reportPath, RelativePath = "report.txt" }
        };
        var classifications = new List<ClassificationResult>
        {
            _service.Classify(invoicePath),
            _service.Classify(reportPath)
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

    private ClassificationResult ClassifyText(string fileName, string content)
    {
        var path = CreateTempFile(fileName, Encoding.UTF8.GetBytes(content));
        return _service.Classify(path);
    }

    private static string CreateTempFile(string fileName, byte[] content)
    {
        var directory = CreateTempDirectory();
        var path = Path.Combine(directory, fileName);
        File.WriteAllBytes(path, content);
        return path;
    }

    private static string CreateZipFile(string fileName, params string[] entryNames)
    {
        var directory = CreateTempDirectory();
        var path = Path.Combine(directory, fileName);

        using (var stream = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: false))
        {
            foreach (var entryName in entryNames)
            {
                var entry = archive.CreateEntry(entryName);
                using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
                writer.Write(entryName);
            }
        }

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
