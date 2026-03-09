using System;
using System.Collections.Generic;
using System.IO;

namespace FileOrganizer.Core;

public sealed class DeterministicOrganizationPlanner
{
    private static readonly Dictionary<string, (string Category, double Confidence)> ExtensionMap =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [".jpg"] = ("Images", 0.60),
            [".jpeg"] = ("Images", 0.60),
            [".png"] = ("Images", 0.60),
            [".gif"] = ("Images", 0.55),
            [".bmp"] = ("Images", 0.55),
            [".webp"] = ("Images", 0.55),

            [".txt"] = ("Documents", 0.55),
            [".doc"] = ("Documents", 0.60),
            [".docx"] = ("Documents", 0.60),
            [".pdf"] = ("Documents", 0.60),
            [".rtf"] = ("Documents", 0.55),

            [".mp4"] = ("Videos", 0.60),
            [".mov"] = ("Videos", 0.60),
            [".avi"] = ("Videos", 0.60),
            [".mkv"] = ("Videos", 0.60),
            [".wmv"] = ("Videos", 0.55),

            [".mp3"] = ("Audio", 0.60),
            [".wav"] = ("Audio", 0.60),
            [".flac"] = ("Audio", 0.60),
            [".m4a"] = ("Audio", 0.55),

            [".zip"] = ("Archives", 0.60),
            [".rar"] = ("Archives", 0.60),
            [".7z"] = ("Archives", 0.60),
            [".tar"] = ("Archives", 0.55),
            [".gz"] = ("Archives", 0.55),

            [".cs"] = ("Code", 0.60),
            [".js"] = ("Code", 0.60),
            [".ts"] = ("Code", 0.60),
            [".py"] = ("Code", 0.60),
            [".json"] = ("Code", 0.55),
            [".xml"] = ("Code", 0.55),
            [".html"] = ("Code", 0.55),
            [".css"] = ("Code", 0.55),

            [".csv"] = ("Data Files", 0.60),
            [".xlsx"] = ("Data Files", 0.60),
            [".xls"] = ("Data Files", 0.60)
        };

    public OrganizationPlan GetOrganizationPlan(string basePath, IReadOnlyCollection<ScannedFile> scannedFiles)
    {
        var plan = new OrganizationPlan();

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

            var (category, confidence, reason) = Classify(file);

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
                ProposedFileName = Path.GetFileName(file.SourcePath),
                Category = category,
                ConfidenceScore = confidence,
                ReasoningSummary = reason
            });
        }

        return plan;
    }

    private static (string Category, double Confidence, string Reason) Classify(ScannedFile file)
    {
        if (ExtensionMap.TryGetValue(file.Extension, out var mapped))
        {
            return (mapped.Category, mapped.Confidence, $"Extension match: {file.Extension}");
        }

        return ("Miscellaneous", 0.30, $"Unknown extension: {file.Extension}");
    }

    private static string GetImmediateParentFolder(string sourcePath)
    {
        var parent = Directory.GetParent(sourcePath);
        return parent?.Name ?? string.Empty;
    }
}
