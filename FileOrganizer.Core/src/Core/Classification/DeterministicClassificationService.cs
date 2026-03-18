using System;
using System.IO;
using FileOrganizer.Core.Extraction;

namespace FileOrganizer.Core.Classification;

public sealed class DeterministicClassificationService : IClassificationService
{
    private readonly IExtractionService _extractionService;
    private readonly IHeuristicDocumentClassifier _heuristicDocumentClassifier;

    public DeterministicClassificationService(
        IExtractionService extractionService,
        IHeuristicDocumentClassifier? heuristicDocumentClassifier = null)
    {
        _extractionService = extractionService;
        _heuristicDocumentClassifier = heuristicDocumentClassifier ?? new HeuristicDocumentClassifier();
    }

    public ClassificationResult Classify(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var originalFileName = Path.GetFileName(path);
        var extension = Path.GetExtension(path);

        try
        {
            var artifact = _extractionService.Extract(path);
            return Classify(path, artifact);
        }
        catch
        {
            return CreateExtensionFallback(path, originalFileName, extension, null);
        }
    }

    public ClassificationResult Classify(string path, ExtractionArtifact artifact)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(artifact);

        var originalFileName = Path.GetFileName(path);
        var extension = Path.GetExtension(path);

        var detectedType = string.IsNullOrWhiteSpace(artifact.FileType.Category) ? "Unknown" : artifact.FileType.Category;
            var semanticCategory = FolderMapping.GetSemanticCategoryForDetectedType(detectedType);
            var shouldFallback = !artifact.Status.Success || semanticCategory.Equals("Unclassified", StringComparison.OrdinalIgnoreCase);

            if (shouldFallback)
            {
                return CreateExtensionFallback(path, originalFileName, extension, artifact.Status.ErrorMessage);
            }

            var analysisStage = DetermineAnalysisStage(artifact);
            var confidence = artifact.FileType.Confidence >= 0.9
                ? artifact.FileType.Confidence
                : Math.Max(artifact.FileType.Confidence, 0.75);
            var result = new ClassificationResult
            {
                FilePath = path,
                DetectedType = detectedType,
                SemanticCategory = semanticCategory,
                SuggestedFolder = FolderMapping.GetFolder(semanticCategory, detectedType),
                SuggestedFilename = originalFileName,
                ConfidenceScore = confidence,
                ReasoningSource = "Deterministic",
                AnalysisStage = analysisStage,
                ReasoningSummary = BuildReasoningSummary(artifact, detectedType, analysisStage)
            };

            if (ShouldApplyHeuristics(artifact))
            {
                var heuristic = _heuristicDocumentClassifier.Classify(artifact);
                if (heuristic.Matched && heuristic.ConfidenceScore > result.ConfidenceScore)
                {
                    return new ClassificationResult
                    {
                        FilePath = path,
                        DetectedType = detectedType,
                        SemanticCategory = heuristic.SemanticCategory,
                        SuggestedFolder = FolderMapping.GetFolder(heuristic.SemanticCategory, detectedType),
                        SuggestedFilename = originalFileName,
                        ConfidenceScore = heuristic.ConfidenceScore,
                        ReasoningSource = "Deterministic",
                        AnalysisStage = "heuristic_content",
                        ReasoningSummary = heuristic.ReasoningSummary
                    };
                }
            }

        return result;
    }

    private static bool ShouldApplyHeuristics(ExtractionArtifact artifact)
    {
        var category = artifact.FileType.Category;
        if (category.Equals("TextDocument", StringComparison.OrdinalIgnoreCase)
            || category.Equals("StructuredDocument", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!category.Equals("CodeFile", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var preview = artifact.Content.TextPreview;
        if (string.IsNullOrWhiteSpace(preview))
        {
            return false;
        }

        var trimmed = preview.TrimStart();
        return trimmed.StartsWith("{", StringComparison.Ordinal)
            || trimmed.StartsWith("[", StringComparison.Ordinal)
            || trimmed.StartsWith("<", StringComparison.Ordinal)
            || preview.Contains("created_at", StringComparison.OrdinalIgnoreCase)
            || preview.Contains("updated_at", StringComparison.OrdinalIgnoreCase)
            || preview.Contains("connectionString", StringComparison.OrdinalIgnoreCase)
            || preview.Contains("connection_string", StringComparison.OrdinalIgnoreCase)
            || preview.Contains("port", StringComparison.OrdinalIgnoreCase)
            || preview.Contains("host", StringComparison.OrdinalIgnoreCase);
    }

    private static string DetermineAnalysisStage(ExtractionArtifact artifact)
    {
        if (artifact.FileType.DetectedMime.Equals("application/pdf", StringComparison.OrdinalIgnoreCase))
        {
            return artifact.Content.TextPreview is not null || artifact.Structure.PageCount > 0 || artifact.Metadata.Additional.ContainsKey("PdfVersion")
                ? "pdf_extraction"
                : "signature";
        }

        if (artifact.Metadata.Additional.ContainsKey("ContainerSubtype") || artifact.Structure.EntryCount > 0)
        {
            return "container_structure";
        }

        return "signature";
    }

    private static string BuildReasoningSummary(ExtractionArtifact artifact, string detectedType, string analysisStage)
    {
        return analysisStage switch
        {
            "pdf_extraction" => $"PDF extraction confirmed {detectedType} with page/metadata signals.",
            "container_structure" => $"Container structure confirmed {detectedType} from OpenXML entry names.",
            "signature" => $"Signature-backed type {detectedType}.",
            _ => $"Extraction confirmed {detectedType}."
        };
    }

    private static ClassificationResult CreateExtensionFallback(string path, string originalFileName, string extension, string? errorMessage)
    {
        FolderMapping.TryGetFolderForExtension(extension, out var semanticCategory, out var folder, out var reason);

        return new ClassificationResult
        {
            FilePath = path,
            DetectedType = string.IsNullOrWhiteSpace(extension) ? "Unknown" : extension,
            SemanticCategory = semanticCategory,
            SuggestedFolder = folder,
            SuggestedFilename = originalFileName,
            ConfidenceScore = FolderMapping.GetConfidenceForExtension(extension),
            ReasoningSource = "Deterministic",
            AnalysisStage = "extension_fallback",
            ReasoningSummary = string.IsNullOrWhiteSpace(errorMessage)
                ? reason
                : $"{reason}; extraction unavailable."
        };
    }
}
