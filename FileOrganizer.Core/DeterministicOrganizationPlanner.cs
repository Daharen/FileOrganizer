using System;
using System.Collections.Generic;
using System.IO;
using FileOrganizer.Core.Classification;
using FileOrganizer.Core.Renaming;

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
        IReadOnlyCollection<ClassificationResult> classifications,
        IReadOnlyCollection<FilenameSuggestion> filenameSuggestions)
    {
        ArgumentNullException.ThrowIfNull(filenameSuggestions);

        var suggestionMap = new Dictionary<string, FilenameSuggestion>(StringComparer.OrdinalIgnoreCase);
        using var fileEnumerator = scannedFiles.GetEnumerator();
        using var suggestionEnumerator = filenameSuggestions.GetEnumerator();
        while (fileEnumerator.MoveNext() && suggestionEnumerator.MoveNext())
        {
            suggestionMap[fileEnumerator.Current.SourcePath] = suggestionEnumerator.Current;
        }

        return GetOrganizationPlan(basePath, scannedFiles, classifications, suggestionMap);
    }

    public OrganizationPlan GetOrganizationPlan(
        string basePath,
        IReadOnlyCollection<ScannedFile> scannedFiles,
        IReadOnlyCollection<ClassificationResult> classifications)
    {
        return GetOrganizationPlan(basePath, scannedFiles, classifications, null);
    }

    private OrganizationPlan GetOrganizationPlan(
        string basePath,
        IReadOnlyCollection<ScannedFile> scannedFiles,
        IReadOnlyCollection<ClassificationResult> classifications,
        IReadOnlyDictionary<string, FilenameSuggestion>? suggestionMap)
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

            var originalFileName = Path.GetFileName(file.SourcePath);
            var renameSuggestion = suggestionMap is not null && suggestionMap.TryGetValue(file.SourcePath, out var suggestion)
                ? suggestion
                : null;
            var proposedFileName = renameSuggestion is not null && renameSuggestion.ShouldRename
                ? renameSuggestion.SuggestedFilename
                : string.IsNullOrWhiteSpace(classification.SuggestedFilename)
                    ? originalFileName
                    : classification.SuggestedFilename;
            var stage = renameSuggestion is not null && renameSuggestion.ShouldRename
                ? "rename_deterministic"
                : classification.AnalysisStage;
            var reasoningSummary = renameSuggestion is not null && renameSuggestion.ShouldRename
                ? $"{stage}: {renameSuggestion.ReasoningSummary}"
                : $"{classification.AnalysisStage}: {classification.ReasoningSummary}";

            plan.Operations.Add(new FileMoveOperation
            {
                SourcePath = file.SourcePath,
                DestinationDirectory = Path.Combine(basePath, category),
                ProposedFileName = proposedFileName,
                Category = category,
                ConfidenceScore = renameSuggestion is not null && renameSuggestion.ShouldRename
                    ? Math.Max(classification.ConfidenceScore, renameSuggestion.ConfidenceScore)
                    : classification.ConfidenceScore,
                ReasoningSummary = reasoningSummary
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
