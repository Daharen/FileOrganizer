using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FileOrganizer.Core.Classification;
using FileOrganizer.Core.Extraction;
using FileOrganizer.Core.Renaming;
using Xunit;

namespace FileOrganizer.Core.Tests;

public sealed class FilenameSuggestionServiceTests
{
    private readonly IFilenameSuggestionService _service = new DeterministicFilenameSuggestionService();

    [Fact]
    public void InvoiceLikeContent_WithSourceAndDate_ProducesDeterministicRename()
    {
        var suggestion = Suggest(
            "invoice.txt",
            "Invoice",
            0.92,
            "Invoice Number: INV-1001\nInvoice From: Acme Corp\nDate: 2026-03-15\nAmount Due: $108.00\nTotal: $108.00");

        Assert.True(suggestion.ShouldRename);
        Assert.Equal("Invoice_Acme_Corp_2026-03-15.txt", suggestion.SuggestedFilename);
        Assert.Equal("InvoicePattern", suggestion.Strategy);
    }

    [Fact]
    public void WeakInvoiceContent_DoesNotRename()
    {
        var suggestion = Suggest(
            "invoice.txt",
            "Invoice",
            0.90,
            "Invoice\nTotal due soon");

        Assert.False(suggestion.ShouldRename);
        Assert.Equal("invoice.txt", suggestion.SuggestedFilename);
    }

    [Fact]
    public void ResumeLikeContent_WithStrongCandidateName_ProducesRename()
    {
        var suggestion = Suggest(
            "resume draft.txt",
            "Resume",
            0.91,
            "Jane Doe\nProfessional Summary\nExperience\nEducation\nSkills");

        Assert.True(suggestion.ShouldRename);
        Assert.Equal("Resume_Jane_Doe.txt", suggestion.SuggestedFilename);
    }

    [Fact]
    public void WeakResumeContent_DoesNotRename()
    {
        var suggestion = Suggest(
            "resume.txt",
            "Resume",
            0.91,
            "Professional Summary\nExperience\nEducation\nSkills");

        Assert.False(suggestion.ShouldRename);
    }

    [Fact]
    public void NotesWithClearDate_ProduceRename()
    {
        var suggestion = Suggest(
            "weekly-notes.md",
            "Notes",
            0.84,
            "2026-03-18\nAgenda\n- review\n- action items");

        Assert.True(suggestion.ShouldRename);
        Assert.Equal("MeetingNotes_2026-03-18.md", suggestion.SuggestedFilename);
    }

    [Fact]
    public void AmbiguousNotes_PreserveOriginalFilename()
    {
        var suggestion = Suggest(
            "notes.md",
            "Notes",
            0.84,
            "Notes\n- follow up\n- todo");

        Assert.False(suggestion.ShouldRename);
        Assert.Equal("notes.md", suggestion.SuggestedFilename);
    }

    [Fact]
    public void GeneratedNames_PreserveOriginalExtension()
    {
        var suggestion = Suggest(
            "candidate.resume.pdf",
            "Resume",
            0.91,
            "Jane Doe\nExperience\nEducation\nSkills\nReferences");

        Assert.EndsWith(".pdf", suggestion.SuggestedFilename, StringComparison.Ordinal);
    }

    [Fact]
    public void InvalidFilenameCharacters_AreNormalizedSafely()
    {
        var normalized = FilenameNormalization.NormalizeSuggestedFilename("Invoice_ Acme:West / 2026?.pdf");

        Assert.Equal("Invoice_Acme:West_2026?.pdf".Replace(':', Path.GetInvalidFileNameChars().Contains(':') ? '_' : ':').Replace('?', Path.GetInvalidFileNameChars().Contains('?') ? '_' : '?').Replace('/', '_'), normalized);
        Assert.DoesNotContain("  ", normalized, StringComparison.Ordinal);
    }

    [Fact]
    public void OverlongFilenames_AreBoundedDeterministically()
    {
        var baseName = string.Join('_', Enumerable.Repeat("segment", 40));
        var normalized = FilenameNormalization.NormalizeSuggestedFilename($"{baseName}.txt");

        Assert.True(normalized.Length <= 120);
        Assert.EndsWith(".txt", normalized, StringComparison.Ordinal);
    }

    [Fact]
    public void PlannerOutput_RemainsDeterministicAcrossRepeatedRuns_WithRenameSuggestions()
    {
        var root = CreateTempDirectory();
        var source = Path.Combine(root, "resume.txt");
        File.WriteAllText(source, "Jane Doe\nExperience\nEducation\nSkills\nReferences");

        var files = new[] { new ScannedFile { SourcePath = source, RelativePath = "resume.txt" } };
        var classifications = new[]
        {
            new ClassificationResult
            {
                FilePath = source,
                DetectedType = "TextDocument",
                SemanticCategory = "Resume",
                SuggestedFolder = "Documents/Career",
                SuggestedFilename = "resume.txt",
                ConfidenceScore = 0.91,
                AnalysisStage = "heuristic_content",
                ReasoningSummary = "Career profile section signals detected."
            }
        };
        var suggestions = new[]
        {
            Suggest(source, "Resume", 0.91, "Jane Doe\nExperience\nEducation\nSkills\nReferences")
        };
        var planner = new DeterministicOrganizationPlanner();

        var first = planner.GetOrganizationPlan(root, files, classifications, suggestions);
        var second = planner.GetOrganizationPlan(root, files, classifications, suggestions);

        Assert.Equal(first.Operations[0].ProposedFileName, second.Operations[0].ProposedFileName);
        Assert.Equal("Resume_Jane_Doe.txt", first.Operations[0].ProposedFileName);
        Assert.StartsWith("rename_deterministic:", first.Operations[0].ReasoningSummary, StringComparison.Ordinal);
    }

    [Fact]
    public void ExecuteAndUndoBehavior_RemainUnaffectedByRenameEnabledPlans()
    {
        var root = CreateTempDirectory();
        var source = Path.Combine(root, "resume.txt");
        File.WriteAllText(source, "Jane Doe\nExperience\nEducation\nSkills\nReferences");

        var validatedPlan = new ValidatedOrganizationPlan
        {
            AuthorizedRootPath = root,
            ApprovedOperations = new List<ValidatedOperation>
            {
                new()
                {
                    OperationId = "op-1",
                    SourcePath = source,
                    DestinationPath = Path.Combine(root, "Documents", "Career", "Resume_Jane_Doe.txt"),
                    OriginalProposedDestinationPath = Path.Combine(root, "Documents", "Career", "Resume_Jane_Doe.txt"),
                    ResolvedDestinationPath = Path.Combine(root, "Documents", "Career", "Resume_Jane_Doe.txt"),
                    ProposedFileName = "Resume_Jane_Doe.txt",
                    ConfidenceScore = 0.91,
                    PlanningStage = "rename_deterministic",
                    StableOrderIndex = 0
                }
            }
        };

        var journal = new InMemoryExecutionJournal();
        var executor = new OrganizationExecutor(journal, Path.Combine(root, "execution-journal.ndjson"));
        var execution = executor.ExecutePlan(validatedPlan);

        Assert.Equal(1, execution.Executed);
        Assert.Single(journal.Entries);
        Assert.True(File.Exists(validatedPlan.ApprovedOperations[0].DestinationPath));

        var undoExecutor = new UndoExecutor();
        var undo = undoExecutor.Execute(execution.RunId, root, new[]
        {
            new UndoOperation(execution.RunId, "op-1", validatedPlan.ApprovedOperations[0].DestinationPath, source, DateTimeOffset.UtcNow, 0)
        });

        Assert.Equal(1, undo.Restored);
        Assert.True(File.Exists(source));
        Assert.False(File.Exists(validatedPlan.ApprovedOperations[0].DestinationPath));
    }

    private FilenameSuggestion Suggest(string fileNameOrPath, string semanticCategory, double confidence, string preview)
    {
        var path = Path.IsPathRooted(fileNameOrPath) ? fileNameOrPath : Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), fileNameOrPath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        if (!File.Exists(path))
        {
            File.WriteAllText(path, preview);
        }

        return _service.Suggest(path, new ClassificationResult
        {
            FilePath = path,
            DetectedType = "TextDocument",
            SemanticCategory = semanticCategory,
            SuggestedFolder = "Documents",
            SuggestedFilename = Path.GetFileName(path),
            ConfidenceScore = confidence,
            AnalysisStage = "heuristic_content",
            ReasoningSummary = "deterministic test"
        }, CreateArtifact(path, preview));
    }

    private static ExtractionArtifact CreateArtifact(string path, string preview)
        => new()
        {
            Identity = new FileIdentity { Path = path, Size = preview.Length },
            FileType = new FileTypeInfo { Category = "TextDocument", Confidence = 0.95, DetectedMime = "text/plain", Extension = Path.GetExtension(path) },
            Metadata = new MetadataInfo(),
            Content = new ContentSummary { TextPreview = preview, LineCount = preview.Split('\n').Length, Encoding = "utf-8" },
            Structure = new StructuralFeatures { TokenCount = preview.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length, SectionCount = 1 },
            Status = new ExtractionStatus { Success = true }
        };

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class InMemoryExecutionJournal : IExecutionJournal
    {
        public List<ExecutionJournalEntry> Entries { get; } = new();

        public System.Threading.Tasks.Task AppendAsync(ExecutionJournalEntry entry, System.Threading.CancellationToken cancellationToken = default)
        {
            Entries.Add(entry);
            return System.Threading.Tasks.Task.CompletedTask;
        }
    }
}
