using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using FileOrganizer.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FileOrganizer.GUI;

public partial class MainWindow : Window
{
    private readonly DirectoryScanner _scanner = new();
    private readonly DeterministicOrganizationPlanner _planner = new();
    private readonly OrganizationExecutor _executor = new();

    private IStorageFolder? _currentFolder;
    private List<ScannedFile> _currentFiles = new();
    private OrganizationPlan? _currentPlan;

    public MainWindow()
    {
        InitializeComponent();

        var selectFolderButton = this.FindControl<Button>("SelectFolderButton");
        var previewButton = this.FindControl<Button>("PreviewButton");
        var executeButton = this.FindControl<Button>("ExecuteButton");

        if (selectFolderButton is not null)
        {
            selectFolderButton.Click += SelectFolderButton_Click;
        }

        if (previewButton is not null)
        {
            previewButton.Click += PreviewButton_Click;
        }

        if (executeButton is not null)
        {
            executeButton.Click += ExecuteButton_Click;
        }
    }

    private async void SelectFolderButton_Click(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null)
        {
            return;
        }

        var result = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select Folder to Organize",
            AllowMultiple = false
        });

        if (result.Count < 1)
        {
            return;
        }

        _currentFolder = result[0];
        _currentFiles = _scanner.ScanDirectory(_currentFolder.Path.LocalPath);
        _currentPlan = null;

        var fileListBox = this.FindControl<ListBox>("FileListBox");
        var planListBox = this.FindControl<ListBox>("PlanListBox");
        var statusTextBlock = this.FindControl<TextBlock>("StatusTextBlock");
        var summaryTextBlock = this.FindControl<TextBlock>("SummaryTextBlock");

        if (fileListBox is not null)
        {
            fileListBox.ItemsSource = _currentFiles
                .Select(file => $"{file.RelativePath} | {file.SizeBytes} bytes")
                .ToList();
        }

        if (planListBox is not null)
        {
            planListBox.ItemsSource = null;
        }

        if (statusTextBlock is not null)
        {
            statusTextBlock.Text = $"Loaded folder: {_currentFolder.Path.LocalPath}";
        }

        if (summaryTextBlock is not null)
        {
            summaryTextBlock.Text = $"Scanned {_currentFiles.Count} files.";
        }
    }

    private void PreviewButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_currentFolder is null || _currentFiles.Count == 0)
        {
            SetSummary("No files are loaded.");
            return;
        }

        _currentPlan = _planner.GetOrganizationPlan(_currentFolder.Path.LocalPath, _currentFiles);

        var planListBox = this.FindControl<ListBox>("PlanListBox");
        if (planListBox is not null)
        {
            var items = new List<string>();

            items.AddRange(_currentPlan.Operations.Select(operation =>
                $"MOVE | {Path.GetFileName(operation.SourcePath)} -> {operation.Category}\\{operation.ProposedFileName} | confidence={operation.ConfidenceScore:0.00} | {operation.ReasoningSummary}"));

            items.AddRange(_currentPlan.SkippedFiles.Select(skip =>
                $"SKIP | {Path.GetFileName(skip.SourcePath)} | {skip.Reason}"));

            planListBox.ItemsSource = items;
        }

        SetSummary($"Plan ready. {_currentPlan.Operations.Count} moves. {_currentPlan.SkippedFiles.Count} skipped.");
    }

    private void ExecuteButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_currentFolder is null)
        {
            SetSummary("No folder selected.");
            return;
        }

        if (_currentPlan is null)
        {
            SetSummary("Preview the plan before execution.");
            return;
        }

        var executionResult = _executor.ExecutePlan(_currentPlan);

        var planListBox = this.FindControl<ListBox>("PlanListBox");
        if (planListBox is not null)
        {
            planListBox.ItemsSource = executionResult.Messages;
        }

        _currentFiles = _scanner.ScanDirectory(_currentFolder.Path.LocalPath);
        var fileListBox = this.FindControl<ListBox>("FileListBox");
        if (fileListBox is not null)
        {
            fileListBox.ItemsSource = _currentFiles
                .Select(file => $"{file.RelativePath} | {file.SizeBytes} bytes")
                .ToList();
        }

        SetSummary(
            $"Execution complete. Attempted {executionResult.Attempted}. " +
            $"Executed {executionResult.Executed}. Failed {executionResult.Failed}. " +
            $"Remaining visible files {_currentFiles.Count}.");
    }

    private void SetSummary(string message)
    {
        var summaryTextBlock = this.FindControl<TextBlock>("SummaryTextBlock");
        if (summaryTextBlock is not null)
        {
            summaryTextBlock.Text = message;
        }
    }
}
