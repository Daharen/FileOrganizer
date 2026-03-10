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
    private readonly OperationPlanValidator _validator = new();
    private readonly OrganizationExecutor _executor;

    private IStorageFolder? _currentFolder;
    private List<ScannedFile> _currentFiles = new();
    private OrganizationPlan? _currentPlan;
    private ValidatedOrganizationPlan? _currentValidatedPlan;

    public MainWindow()
    {
        InitializeComponent();

        var journalDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FileOrganizer");
        var journalPath = Path.Combine(journalDirectory, "execution-journal.ndjson");
        var journal = new FileExecutionJournal(journalPath);
        _executor = new OrganizationExecutor(journal, journalPath);

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

        var result = await topLevel.StorageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions
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
        _currentValidatedPlan = null;

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
        _currentValidatedPlan = _validator.Validate(_currentFolder.Path.LocalPath, _currentPlan);

        var planListBox = this.FindControl<ListBox>("PlanListBox");
        if (planListBox is not null)
        {
            var items = new List<string>();

            items.AddRange(_currentValidatedPlan.ApprovedOperations.Select(op =>
                op.CollisionResolutionApplied
                    ? $"APPROVED | id={op.OperationId} | {Path.GetFileName(op.SourcePath)} -> {op.OriginalProposedDestinationPath} | Resolved to {op.ResolvedDestinationPath} due to collision | confidence={op.ConfidenceScore:0.00}"
                    : $"APPROVED | id={op.OperationId} | {Path.GetFileName(op.SourcePath)} -> {op.ResolvedDestinationPath} | confidence={op.ConfidenceScore:0.00}"));

            items.AddRange(_currentValidatedPlan.RejectedOperations.Select(rej =>
                $"REJECTED | {Path.GetFileName(rej.SourcePath)} | {rej.Code} | {rej.Message}"));

            items.AddRange(_currentPlan.SkippedFiles.Select(skip =>
                $"SKIP | {Path.GetFileName(skip.SourcePath)} | {skip.Reason}"));

            planListBox.ItemsSource = items;
        }

        SetSummary(
            $"Plan validated.{Environment.NewLine}" +
            $"Approved {_currentValidatedPlan.ApprovedOperations.Count}.{Environment.NewLine}" +
            $"Rejected {_currentValidatedPlan.RejectedOperations.Count}.{Environment.NewLine}" +
            $"Skipped by planner {_currentPlan.SkippedFiles.Count}.");
    }

    private void ExecuteButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_currentFolder is null)
        {
            SetSummary("No folder selected.");
            return;
        }

        if (_currentValidatedPlan is null)
        {
            SetSummary("Preview and validation must complete before execution.");
            return;
        }

        var executionResult = _executor.ExecutePlan(_currentValidatedPlan);

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
            $"Execution complete.{Environment.NewLine}" +
            $"Approved {executionResult.Approved}. Rejected {executionResult.Rejected}.{Environment.NewLine}" +
            $"Attempted {executionResult.Attempted}. Executed {executionResult.Executed}. Failed {executionResult.Failed}. Skipped {executionResult.Skipped}.{Environment.NewLine}" +
            $"Journal run id {executionResult.RunId}.{Environment.NewLine}" +
            $"Journal path {executionResult.JournalPath}.{Environment.NewLine}" +
            $"Journal entries appended {executionResult.JournalEntriesAppended}. Journal append failures {executionResult.JournalAppendFailures}.{Environment.NewLine}" +
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
