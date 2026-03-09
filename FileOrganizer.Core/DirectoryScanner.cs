using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FileOrganizer.Core
{
    public class DirectoryScanner
    {
        public List<string> ScanDirectory(string path)
        {
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
            {
                return new List<string>();
            }

            try
            {
                // Return the full paths of the files.
                return Directory.GetFiles(path).ToList();
            }
            catch (Exception ex)
            {
                // In a real application, you'd want to log this exception.
                Console.WriteLine($"Error scanning directory: {ex.Message}");
                return new List<string>();
            }
        }
    }
}
