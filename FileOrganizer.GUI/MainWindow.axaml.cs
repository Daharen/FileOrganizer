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
    private readonly IExecutionJournalReader _journalReader;
    private readonly UndoPlanBuilder _undoPlanBuilder = new();
    private readonly UndoExecutor _undoExecutor = new();

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
        _journalReader = new FileExecutionJournalReader(journalPath);
        _executor = new OrganizationExecutor(journal, journalPath);

        WireButton("SelectFolderButton", SelectFolderButton_Click);
        WireButton("PreviewButton", PreviewButton_Click);
        WireButton("ExecuteButton", ExecuteButton_Click);
        WireButton("UndoLastRunButton", UndoLastRunButton_Click);
        WireButton("UndoRunIdButton", UndoRunIdButton_Click);
    }

    private void WireButton(string name, EventHandler<RoutedEventArgs> handler)
    {
        var button = this.FindControl<Button>(name);
        if (button is not null)
        {
            button.Click += handler;
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

        UpdateFileList();
        ClearPlanList();
        SetStatus($"Loaded folder: {_currentFolder.Path.LocalPath}");
        SetSummary($"Scanned {_currentFiles.Count} files.");
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

        SetPlanList(executionResult.Messages);
        RefreshCurrentFolderFiles();

        SetSummary(
            $"Execution complete.{Environment.NewLine}" +
            $"Approved {executionResult.Approved}. Rejected {executionResult.Rejected}.{Environment.NewLine}" +
            $"Attempted {executionResult.Attempted}. Executed {executionResult.Executed}. Failed {executionResult.Failed}. Skipped {executionResult.Skipped}.{Environment.NewLine}" +
            $"Journal run id {executionResult.RunId}.{Environment.NewLine}" +
            $"Journal path {executionResult.JournalPath}.{Environment.NewLine}" +
            $"Journal entries appended {executionResult.JournalEntriesAppended}. Journal append failures {executionResult.JournalAppendFailures}.{Environment.NewLine}" +
            $"Remaining visible files {_currentFiles.Count}.");
    }

    private void UndoLastRunButton_Click(object? sender, RoutedEventArgs e)
    {
        var latestRunId = _journalReader.ReadLatestRunId();
        if (string.IsNullOrWhiteSpace(latestRunId))
        {
            SetSummary("No journaled run is available to undo.");
            return;
        }

        ExecuteUndo(latestRunId);
    }

    private void UndoRunIdButton_Click(object? sender, RoutedEventArgs e)
    {
        var runIdTextBox = this.FindControl<TextBox>("UndoRunIdTextBox");
        var runId = runIdTextBox?.Text?.Trim();
        if (string.IsNullOrWhiteSpace(runId))
        {
            SetSummary("Enter a run id before requesting undo.");
            return;
        }

        ExecuteUndo(runId);
    }

    private void ExecuteUndo(string runId)
    {
        if (_currentFolder is null)
        {
            SetSummary("Select a folder before running undo so the authorized root is known.");
            return;
        }

        var entries = _journalReader.ReadByRunId(runId);
        if (entries.Count == 0)
        {
            SetSummary($"No journal entries were found for run id {runId}.");
            return;
        }

        var undoOperations = _undoPlanBuilder.Build(entries);
        var undoResult = _undoExecutor.Execute(runId, _currentFolder.Path.LocalPath, undoOperations);

        var combinedMessages = new List<string>();
        if (_journalReader is FileExecutionJournalReader fileReader && fileReader.ParseFailures.Count > 0)
        {
            combinedMessages.AddRange(fileReader.ParseFailures);
        }

        combinedMessages.AddRange(undoResult.Messages);
        SetPlanList(combinedMessages);
        RefreshCurrentFolderFiles();

        SetSummary(
            $"Undo complete.{Environment.NewLine}" +
            $"Run id {undoResult.RunId}.{Environment.NewLine}" +
            $"Attempted {undoResult.Attempted}. Restored {undoResult.Restored}. Failed {undoResult.Failed}. Skipped {undoResult.Skipped}. Collision-preserved {undoResult.CollisionPreserved}.{Environment.NewLine}" +
            $"Remaining visible files {_currentFiles.Count}.");
    }

    private void RefreshCurrentFolderFiles()
    {
        if (_currentFolder is null)
        {
            return;
        }

        _currentFiles = _scanner.ScanDirectory(_currentFolder.Path.LocalPath);
        UpdateFileList();
    }

    private void UpdateFileList()
    {
        var fileListBox = this.FindControl<ListBox>("FileListBox");
        if (fileListBox is not null)
        {
            fileListBox.ItemsSource = _currentFiles
                .Select(file => $"{file.RelativePath} | {file.SizeBytes} bytes")
                .ToList();
        }
    }

    private void SetPlanList(IEnumerable<string> items)
    {
        var planListBox = this.FindControl<ListBox>("PlanListBox");
        if (planListBox is not null)
        {
            planListBox.ItemsSource = items.ToList();
        }
    }

    private void ClearPlanList() => SetPlanList(Array.Empty<string>());

    private void SetStatus(string message)
    {
        var statusTextBlock = this.FindControl<TextBlock>("StatusTextBlock");
        if (statusTextBlock is not null)
        {
            statusTextBlock.Text = message;
        }
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
