using System;
using System.IO;
using FileOrganizer.Core.Extraction;

namespace FileOrganizer.Core.Classification;

public sealed class DeterministicClassificationService : IClassificationService
{
    private readonly IExtractionService _extractionService;

    public DeterministicClassificationService(IExtractionService extractionService)
    {
        _extractionService = extractionService;
    }

    public ClassificationResult Classify(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var originalFileName = Path.GetFileName(path);
        var extension = Path.GetExtension(path);

        try
        {
            var artifact = _extractionService.Extract(path);
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

            return new ClassificationResult
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
        }
        catch
        {
            return CreateExtensionFallback(path, originalFileName, extension, null);
        }
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
