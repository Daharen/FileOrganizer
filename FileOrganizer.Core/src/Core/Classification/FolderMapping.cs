using System;
using System.Collections.Generic;

namespace FileOrganizer.Core.Classification;

public static class FolderMapping
{
    private const string UnknownSemanticCategory = "Unclassified";
    private const string UnknownFolder = "Miscellaneous";
    private const double UnknownConfidence = 0.30;

    private static readonly Dictionary<string, (string SemanticCategory, string Folder, double Confidence)> ExtensionMap =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [".jpg"] = ("Images", "Images", 0.60),
            [".jpeg"] = ("Images", "Images", 0.60),
            [".png"] = ("Images", "Images", 0.60),
            [".gif"] = ("Images", "Images", 0.55),
            [".bmp"] = ("Images", "Images", 0.55),
            [".webp"] = ("Images", "Images", 0.55),
            [".txt"] = ("Documents", "Documents", 0.55),
            [".doc"] = ("Documents", "Documents", 0.60),
            [".docx"] = ("Documents", "Documents", 0.60),
            [".pdf"] = ("Documents", "Documents", 0.60),
            [".rtf"] = ("Documents", "Documents", 0.55),
            [".md"] = ("Documents", "Documents", 0.55),
            [".log"] = ("Documents", "Documents", 0.50),
            [".csv"] = ("Data Files", "Data Files", 0.60),
            [".xlsx"] = ("Data Files", "Data Files", 0.60),
            [".xls"] = ("Data Files", "Data Files", 0.60),
            [".mp4"] = ("Videos", "Videos", 0.60),
            [".mov"] = ("Videos", "Videos", 0.60),
            [".avi"] = ("Videos", "Videos", 0.60),
            [".mkv"] = ("Videos", "Videos", 0.60),
            [".wmv"] = ("Videos", "Videos", 0.55),
            [".mp3"] = ("Audio", "Audio", 0.60),
            [".wav"] = ("Audio", "Audio", 0.60),
            [".flac"] = ("Audio", "Audio", 0.60),
            [".m4a"] = ("Audio", "Audio", 0.55),
            [".zip"] = ("Archives", "Archives", 0.60),
            [".rar"] = ("Archives", "Archives", 0.60),
            [".7z"] = ("Archives", "Archives", 0.60),
            [".tar"] = ("Archives", "Archives", 0.55),
            [".gz"] = ("Archives", "Archives", 0.55),
            [".cs"] = ("Code", "Code", 0.60),
            [".js"] = ("Code", "Code", 0.60),
            [".ts"] = ("Code", "Code", 0.60),
            [".py"] = ("Code", "Code", 0.60),
            [".json"] = ("Code", "Code", 0.55),
            [".xml"] = ("Code", "Code", 0.55),
            [".html"] = ("Code", "Code", 0.55),
            [".css"] = ("Code", "Code", 0.55)
        };

    private static readonly Dictionary<string, string> SemanticCategoryFolderMap =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Code"] = "Code",
            ["Configuration"] = "Code/Config",
            ["DataExport"] = "Data",
            ["Documents"] = "Documents",
            ["Invoice"] = "Documents/Finance",
            ["Notes"] = "Documents/Notes",
            ["Report"] = "Documents/Reports",
            ["Resume"] = "Documents/Career",
            ["Images"] = "Images",
            ["Audio"] = "Audio",
            ["Videos"] = "Videos",
            ["Archives"] = "Archives",
            ["Data Files"] = "Data Files",
            [UnknownSemanticCategory] = UnknownFolder
        };

    private static readonly Dictionary<string, string> DetectedTypeSemanticMap =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["CodeFile"] = "Code",
            ["TextDocument"] = "Documents",
            ["StructuredDocument"] = "Documents",
            ["Image"] = "Images",
            ["Audio"] = "Audio",
            ["Video"] = "Videos",
            ["Archive"] = "Archives",
            ["Unknown"] = UnknownSemanticCategory
        };

    public static bool TryGetFolderForExtension(string extension, out string semanticCategory, out string folder, out string reason)
    {
        if (ExtensionMap.TryGetValue(extension, out var mapped))
        {
            semanticCategory = mapped.SemanticCategory;
            folder = mapped.Folder;
            reason = $"Extension match: {extension}";
            return true;
        }

        semanticCategory = UnknownSemanticCategory;
        folder = UnknownFolder;
        reason = $"Unknown extension: {extension}";
        return false;
    }

    public static string GetSemanticCategoryForDetectedType(string detectedType)
        => DetectedTypeSemanticMap.TryGetValue(detectedType, out var semanticCategory)
            ? semanticCategory
            : UnknownSemanticCategory;

    public static string GetSemanticCategoryForExtension(string extension)
        => TryGetFolderForExtension(extension, out var semanticCategory, out _, out _)
            ? semanticCategory
            : UnknownSemanticCategory;

    public static string GetFolderForExtension(string extension)
        => TryGetFolderForExtension(extension, out _, out var folder, out _)
            ? folder
            : UnknownFolder;

    public static double GetConfidenceForExtension(string extension)
        => ExtensionMap.TryGetValue(extension, out var mapped)
            ? mapped.Confidence
            : UnknownConfidence;

    public static string GetFolder(string semanticCategory, string detectedType)
    {
        if (SemanticCategoryFolderMap.TryGetValue(semanticCategory, out var folder))
        {
            return folder;
        }

        var inferredSemanticCategory = GetSemanticCategoryForDetectedType(detectedType);
        return SemanticCategoryFolderMap.TryGetValue(inferredSemanticCategory, out var inferredFolder)
            ? inferredFolder
            : UnknownFolder;
    }
}
