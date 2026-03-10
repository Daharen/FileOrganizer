using System;
using System.IO;

namespace FileOrganizer.Core;

public static class OperationTypeClassifier
{
    public static string Classify(string sourcePath, string destinationPath)
    {
        var sourceDirectory = Path.GetDirectoryName(sourcePath) ?? string.Empty;
        var destinationDirectory = Path.GetDirectoryName(destinationPath) ?? string.Empty;

        var sourceFileName = Path.GetFileName(sourcePath);
        var destinationFileName = Path.GetFileName(destinationPath);

        var directoryChanged = !string.Equals(sourceDirectory, destinationDirectory, PathComparisonPolicy.PathComparison);
        var fileNameChanged = !string.Equals(sourceFileName, destinationFileName, StringComparison.Ordinal);

        if (directoryChanged && fileNameChanged)
        {
            return "MoveAndRename";
        }

        if (directoryChanged)
        {
            return "Move";
        }

        return "Rename";
    }
}
