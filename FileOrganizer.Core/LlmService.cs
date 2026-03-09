using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FileOrganizer.Core
{
    // Represents a single file move operation.
    public class FileMoveOperation
    {
        public string SourcePath { get; set; }
        public string DestinationDirectory { get; set; }
    }

    // Represents the entire organization plan.
    public class OrganizationPlan
    {
        public List<FileMoveOperation> Operations { get; set; } = new List<FileMoveOperation>();
    }

    public class LlmService
    {
        // This is our placeholder method. It will categorize files based on their extension.
        public OrganizationPlan GetOrganizationPlan(string basePath, List<string> filePaths)
        {
            var plan = new OrganizationPlan();

            foreach (var filePath in filePaths)
            {
                var extension = Path.GetExtension(filePath).ToLower();
                string destinationSubfolder;

                switch (extension)
                {
                    case ".jpg":
                    case ".jpeg":
                    case ".png":
                    case ".gif":
                        destinationSubfolder = "Images";
                        break;

                    case ".txt":
                    case ".doc":
                    case ".docx":
                    case ".pdf":
                        destinationSubfolder = "Documents";
                        break;

                    case ".mp4":
                    case ".mov":
                    case ".avi":
                        destinationSubfolder = "Videos";
                        break;

                    default:
                        destinationSubfolder = "Miscellaneous";
                        break;
                }

                plan.Operations.Add(new FileMoveOperation
                {
                    SourcePath = filePath,
                    DestinationDirectory = Path.Combine(basePath, destinationSubfolder)
                });
            }

            return plan;
        }
    }
}
