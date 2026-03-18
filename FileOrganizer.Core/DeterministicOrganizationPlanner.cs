using System;
using System.Collections.Generic;
using System.IO;
using FileOrganizer.Core.Classification;

namespace FileOrganizer.Core;

public sealed class DeterministicOrganizationPlanner
{
    public OrganizationPlan GetOrganizationPlan(string basePath, IReadOnlyCollection<ScannedFile> scannedFiles)
    {
        ArgumentNullException.ThrowIfNull(scannedFiles);

        var classifications = new List<ClassificationResult>(scannedFiles.Count);
        foreach (var file in scannedFiles)
        {
            classifications.Add(new ClassificationResult
            {
                FilePath = file.SourcePath,
                DetectedType = file.Extension,
                SemanticCategory = FolderMapping.GetSemanticCategoryForExtension(file.Extension),
                SuggestedFolder = FolderMapping.GetFolderForExtension(file.Extension),
                SuggestedFilename = Path.GetFileName(file.SourcePath),
                ConfidenceScore = FolderMapping.GetConfidenceForExtension(file.Extension),
                ReasoningSource = "Deterministic",
                AnalysisStage = "extension_fallback",
                ReasoningSummary = FolderMapping.TryGetFolderForExtension(file.Extension, out _, out _, out var reason)
                    ? reason
                    : $"Unknown extension: {file.Extension}"
            });
        }

        return GetOrganizationPlan(basePath, scannedFiles, classifications);
    }

    public OrganizationPlan GetOrganizationPlan(
        string basePath,
        IReadOnlyCollection<ScannedFile> scannedFiles,
        IReadOnlyCollection<ClassificationResult> classifications)
    {
        ArgumentNullException.ThrowIfNull(scannedFiles);
        ArgumentNullException.ThrowIfNull(classifications);

        var plan = new OrganizationPlan();
        var classificationMap = new Dictionary<string, ClassificationResult>(StringComparer.OrdinalIgnoreCase);
        foreach (var classification in classifications)
        {
            classificationMap[classification.FilePath] = classification;
        }

        foreach (var file in scannedFiles)
        {
            if (file.IsProtectedExecutable)
            {
                plan.SkippedFiles.Add(new PlanSkipRecord
                {
                    SourcePath = file.SourcePath,
                    Reason = "Protected executable file."
                });

                continue;
            }

            var classification = classificationMap.TryGetValue(file.SourcePath, out var matched)
                ? matched
                : CreateFallbackClassification(file.SourcePath, file.Extension);

            var category = string.IsNullOrWhiteSpace(classification.SuggestedFolder)
                ? FolderMapping.GetFolder(classification.SemanticCategory, classification.DetectedType)
                : classification.SuggestedFolder;

            if (file.IsAlreadyInCategoryFolder &&
                string.Equals(GetImmediateParentFolder(file.SourcePath), category, StringComparison.OrdinalIgnoreCase))
            {
                plan.SkippedFiles.Add(new PlanSkipRecord
                {
                    SourcePath = file.SourcePath,
                    Reason = "Already located in matching category folder."
                });

                continue;
            }

            plan.Operations.Add(new FileMoveOperation
            {
                SourcePath = file.SourcePath,
                DestinationDirectory = Path.Combine(basePath, category),
                ProposedFileName = string.IsNullOrWhiteSpace(classification.SuggestedFilename)
                    ? Path.GetFileName(file.SourcePath)
                    : classification.SuggestedFilename,
                Category = category,
                ConfidenceScore = classification.ConfidenceScore,
                ReasoningSummary = $"{classification.AnalysisStage}: {classification.ReasoningSummary}"
            });
        }

        return plan;
    }

    private static ClassificationResult CreateFallbackClassification(string sourcePath, string extension)
    {
        FolderMapping.TryGetFolderForExtension(extension, out var semanticCategory, out var folder, out var reason);

        return new ClassificationResult
        {
            FilePath = sourcePath,
            DetectedType = extension,
            SemanticCategory = semanticCategory,
            SuggestedFolder = folder,
            SuggestedFilename = Path.GetFileName(sourcePath),
            ConfidenceScore = FolderMapping.GetConfidenceForExtension(extension),
            ReasoningSource = "Deterministic",
            AnalysisStage = "extension_fallback",
            ReasoningSummary = reason
        };
    }

    private static string GetImmediateParentFolder(string sourcePath)
    {
        var parent = Directory.GetParent(sourcePath);
        return parent?.Name ?? string.Empty;
    }
}
