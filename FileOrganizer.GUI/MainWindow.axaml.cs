using Avalonia.Controls;
using Avalonia.Interactivity;
using FileOrganizer.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Platform.Storage;

namespace FileOrganizer.GUI
{
    public partial class MainWindow : Window
    {
        private readonly DirectoryScanner _scanner = new DirectoryScanner();
        private readonly LlmService _llmService = new LlmService();
        private IStorageFolder _currentFolder;
        private List<string> _currentFiles = new List<string>();

        public MainWindow()
        {
            InitializeComponent();

            var selectFolderButton = this.FindControl<Button>("SelectFolderButton");
            var organizeButton = this.FindControl<Button>("OrganizeButton");

            if (selectFolderButton != null)
            {
                selectFolderButton.Click += SelectFolderButton_Click;
            }

            if (organizeButton != null)
            {
                organizeButton.Click += OrganizeButton_Click;
            }
        }

        private async void SelectFolderButton_Click(object sender, RoutedEventArgs e)
        {
            var topLevel = TopLevel.GetTopLevel(this);

            var result = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Select Folder to Organize",
                AllowMultiple = false
            });

            if (result.Count >= 1)
            {
                _currentFolder = result[0];
                var fileListBox = this.FindControl<ListBox>("FileListBox");

                _currentFiles = _scanner.ScanDirectory(_currentFolder.Path.LocalPath);

                if (fileListBox != null)
                {
                    fileListBox.ItemsSource = _currentFiles.Select(Path.GetFileName).ToList();
                }
            }
        }

        private void OrganizeButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentFolder == null || !_currentFiles.Any())
            {
                return;
            }

            var plan = _llmService.GetOrganizationPlan(_currentFolder.Path.LocalPath, _currentFiles);

            foreach (var operation in plan.Operations)
            {
                try
                {
                    Directory.CreateDirectory(operation.DestinationDirectory);
                    var destinationFilePath = Path.Combine(operation.DestinationDirectory, Path.GetFileName(operation.SourcePath));
                    File.Move(operation.SourcePath, destinationFilePath);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to move file {operation.SourcePath}: {ex.Message}");
                }
            }

            var fileListBox = this.FindControl<ListBox>("FileListBox");
            if (fileListBox != null)
            {
                fileListBox.ItemsSource = null;
            }
            _currentFiles.Clear();
        }
    }
}
