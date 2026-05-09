// AgValoniaGPS
// Copyright (C) 2024-2026 AgValoniaGPS Contributors
//
// Licensed under GNU GPL v3. See LICENSE.md.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using AgValoniaGPS.Models;
using AgValoniaGPS.Models.Job;
using AgValoniaGPS.Models.State;
using AgValoniaGPS.Services;
using AgValoniaGPS.Services.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AgValoniaGPS.ViewModels;

/// <summary>
/// Backs the <c>StartWorkSessionDialogPanel</c> — the two-column
/// "Start Work Session" picker (#349 M3). Left column lists nearby
/// fields; right column shows the selected field's job history plus a
/// New-Job mini-form.
/// </summary>
public partial class StartWorkSessionDialogViewModel : ObservableObject
{
    /// <summary>Distance budget (km) for the nearby-fields query.
    /// Outside this radius, fields are still listed but at the bottom.</summary>
    public const double DefaultMaxKm = 100.0;

    private readonly IFieldService _fieldService;
    private readonly IJobService _jobService;
    private readonly ISettingsService _settingsService;
    private readonly ApplicationState _appState;
    private readonly Action _close;
    private readonly Action<string, string> _openField;                                  // (fieldPath, fieldName)
    private readonly Action<string, string, string, string, string?> _openFieldStartingNewJob;
    private readonly Action<string, string, string> _openFieldResumingJob;
    private readonly Action<string, Action> _confirm;   // (message, onConfirm)
    private readonly double? _nearbyMaxKm;

    public StartWorkSessionDialogViewModel(
        IFieldService fieldService,
        IJobService jobService,
        ISettingsService settingsService,
        ApplicationState appState,
        Action close,
        Action<string, string> openField,
        Action<string, string, string, string, string?> openFieldStartingNewJob,
        Action<string, string, string> openFieldResumingJob,
        Action<string, Action> confirm,
        double? nearbyMaxKm = null)
    {
        _fieldService = fieldService;
        _jobService = jobService;
        _settingsService = settingsService;
        _appState = appState;
        _close = close;
        _openField = openField;
        _openFieldStartingNewJob = openFieldStartingNewJob;
        _openFieldResumingJob = openFieldResumingJob;
        _confirm = confirm;
        _nearbyMaxKm = nearbyMaxKm;

        StartNewJobCommand = new RelayCommand(StartNewJob, () => SelectedField != null);
        OpenFieldOnlyCommand = new RelayCommand(OpenFieldOnly, () => SelectedField != null);
        ResumeJobCommand = new RelayCommand<JobSummary?>(ResumeJob);
        DeleteJobCommand = new RelayCommand<JobSummary?>(DeleteJob, CanDeleteJob);
        UseLastNotesCommand = new RelayCommand(UseLastNotes,
            () => JobsForSelectedField.Count > 0);
        CancelCommand = new RelayCommand(() => _close());
    }

    /// <summary>
    /// False when no job is selected, or when the selected job is the
    /// currently <see cref="IJobService.ActiveJob"/>. The active job
    /// can't be deleted while it owns the in-memory coverage; the
    /// service throws if you try.
    /// </summary>
    private bool CanDeleteJob(JobSummary? summary)
    {
        if (summary == null) return false;
        var active = _jobService.ActiveJob;
        if (active == null) return true;
        return !(string.Equals(active.FieldName, summary.FieldName, StringComparison.OrdinalIgnoreCase)
                 && string.Equals(active.TaskName, summary.TaskName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Fields under <c>FieldsRoot</c>, ordered by distance from
    /// the current GPS position (or filesystem order if no fix).</summary>
    public ObservableCollection<NearbyField> Fields { get; } = new();

    /// <summary>Jobs for <see cref="SelectedField"/>, most-recent first.</summary>
    public ObservableCollection<JobSummary> JobsForSelectedField { get; } = new();

    /// <summary>Suggestions for the WorkType autocomplete: seed labels
    /// plus distinct prior values across all known jobs.</summary>
    public ObservableCollection<string> WorkTypeSuggestions { get; } = new();

    [ObservableProperty]
    private NearbyField? _selectedField;

    /// <summary>
    /// Two-way bound to the Jobs ListBox's <c>SelectedItem</c> so the
    /// Delete button's CanExecute can react to the active-job check.
    /// </summary>
    [ObservableProperty]
    private JobSummary? _selectedJob;

    [ObservableProperty]
    private string _newJobWorkType = string.Empty;

    [ObservableProperty]
    private string _newJobNotes = string.Empty;

    [ObservableProperty]
    private string _newJobTaskName = string.Empty;

    /// <summary>
    /// True once the user has edited the Task name field by hand (or
    /// inserted via the Date/Time buttons). Auto-recompute from
    /// <see cref="NewJobWorkType"/> stops as soon as this flips.
    /// </summary>
    private bool _taskNameUserEdited;

    [ObservableProperty]
    private string? _statusMessage;

    public RelayCommand StartNewJobCommand { get; }
    public RelayCommand OpenFieldOnlyCommand { get; }
    public RelayCommand<JobSummary?> ResumeJobCommand { get; }
    public RelayCommand<JobSummary?> DeleteJobCommand { get; }
    public RelayCommand UseLastNotesCommand { get; }
    public ICommand CancelCommand { get; }

    /// <summary>
    /// Refresh the fields list and suggestion set from disk. Called when
    /// the dialog is shown so it reflects any new/deleted fields and
    /// any work-type strings introduced in another session.
    /// </summary>
    public void Refresh()
    {
        var root = _settingsService.Settings.FieldsDirectory;
        var lat = _appState.Vehicle.Latitude;
        var lon = _appState.Vehicle.Longitude;

        Fields.Clear();

        var allNames = _fieldService.GetAvailableFields(root);

        // Always show every field on disk. When we have a fix, enrich with
        // distance (uncapped) for sort ranking; rows whose origin is (0,0)
        // or unreadable fall through to distance 0.0 and sort to the bottom.
        var known = (lat != 0 || lon != 0)
            ? _fieldService.FindFieldsNear(root, lat, lon, double.MaxValue)
                .ToDictionary(f => f.Name, f => f, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, NearbyField>(StringComparer.OrdinalIgnoreCase);

        var rows = new List<NearbyField>(allNames.Count);
        foreach (var name in allNames)
        {
            if (known.TryGetValue(name, out var nf))
                rows.Add(nf);
            else
            {
                var dir = System.IO.Path.Combine(root, name);
                rows.Add(new NearbyField(
                    Name: name,
                    DirectoryPath: dir,
                    DistanceKm: 0,
                    BoundaryAreaHectares: 0));
            }
        }
        rows.Sort((a, b) =>
        {
            bool aKnown = known.ContainsKey(a.Name);
            bool bKnown = known.ContainsKey(b.Name);
            if (aKnown && bKnown) return a.DistanceKm.CompareTo(b.DistanceKm);
            if (aKnown) return -1;
            if (bKnown) return 1;
            return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
        });
        foreach (var f in rows) Fields.Add(f);

        WorkTypeSuggestions.Clear();
        foreach (var s in _jobService.SuggestWorkTypes())
            WorkTypeSuggestions.Add(s);

        // Prefer the field that's already open — if the operator is mid-job
        // and reaches for "Start Session", they almost certainly want the
        // current field highlighted, not whichever one happens to be nearest.
        var activeName = _fieldService.ActiveField?.Name;
        SelectedField = (activeName != null
                ? Fields.FirstOrDefault(f =>
                    string.Equals(f.Name, activeName, StringComparison.OrdinalIgnoreCase))
                : null)
            ?? Fields.FirstOrDefault();

        StatusMessage = Fields.Count == 0
            ? "No fields found. Create one from Field Operations → Create Field."
            : null;
    }

    partial void OnSelectedFieldChanged(NearbyField? value)
    {
        JobsForSelectedField.Clear();
        if (value != null)
        {
            foreach (var j in _jobService.ListJobs(value.Name))
                JobsForSelectedField.Add(j);
        }

        // Reset the New-Job form for the newly selected field. Switching
        // fields re-arms auto-recompute — the user hasn't typed anything
        // for THIS field yet.
        _taskNameUserEdited = false;
        NewJobNotes = string.Empty;
        NewJobWorkType = string.Empty;
        RecomputeDefaultTaskName();

        StartNewJobCommand.NotifyCanExecuteChanged();
        OpenFieldOnlyCommand.NotifyCanExecuteChanged();
        UseLastNotesCommand.NotifyCanExecuteChanged();
    }

    partial void OnNewJobWorkTypeChanged(string value)
    {
        if (!_taskNameUserEdited) RecomputeDefaultTaskName();
    }

    partial void OnSelectedJobChanged(JobSummary? value)
    {
        DeleteJobCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// Marks the task-name field as user-edited so subsequent work-type
    /// changes don't clobber it. Called from the AXAML code-behind on
    /// real keyboard input and from the Date/Time insert handlers.
    /// </summary>
    public void MarkTaskNameUserEdited() => _taskNameUserEdited = true;

    private void RecomputeDefaultTaskName()
    {
        var date = DateTime.Now.ToString("yyyy-MM-dd");
        var slug = NormalizeWorkType(NewJobWorkType);
        NewJobTaskName = string.IsNullOrEmpty(slug) ? date : $"{date}_{slug}";
    }

    private static string NormalizeWorkType(string? wt)
    {
        if (string.IsNullOrWhiteSpace(wt)) return string.Empty;
        var lowered = wt.Trim().ToLowerInvariant();
        var chars = new char[lowered.Length];
        for (int i = 0; i < lowered.Length; i++)
        {
            var c = lowered[i];
            chars[i] = c == ' '
                ? '_'
                : (char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '_');
        }
        return new string(chars);
    }

    private void StartNewJob()
    {
        if (SelectedField == null) return;
        try
        {
            var taskName = string.IsNullOrWhiteSpace(NewJobTaskName) ? null : NewJobTaskName.Trim();
            // Important: do NOT call _jobService.CreateJob here. That would
            // flip ActiveJob to the new job before MainViewModel.CloseFieldAsync
            // ran, and CloseFieldAsync would then save the *previous* job's
            // in-memory coverage to the *new* job's folder. Defer the create
            // until after the close, via the pending-intent plumbing.
            _openFieldStartingNewJob(
                SelectedField.DirectoryPath,
                SelectedField.Name,
                NewJobWorkType?.Trim() ?? string.Empty,
                NewJobNotes ?? string.Empty,
                taskName);
            _close();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    private void OpenFieldOnly()
    {
        if (SelectedField == null) return;
        // Field-only open (Decision #2): geometry loads, no job is
        // activated, coverage isn't loaded. The _openField callback maps
        // to MainViewModel.OpenFieldOnlyAsync which sets the field-only
        // pending-intent flag.
        _openField(SelectedField.DirectoryPath, SelectedField.Name);
        _close();
    }

    private void DeleteJob(JobSummary? summary)
    {
        if (summary == null || SelectedField == null) return;
        var fieldName = SelectedField.Name;

        _confirm(
            $"Delete job '{summary.TaskName}' from field '{fieldName}'? This permanently removes its coverage and section log.",
            () =>
            {
                try
                {
                    _jobService.DeleteJob(summary.FieldName, summary.TaskName);

                    // Refresh the jobs list for the still-selected field.
                    JobsForSelectedField.Clear();
                    foreach (var j in _jobService.ListJobs(fieldName))
                        JobsForSelectedField.Add(j);
                    UseLastNotesCommand.NotifyCanExecuteChanged();
                }
                catch (Exception ex)
                {
                    StatusMessage = ex.Message;
                }
            });
    }

    private void ResumeJob(JobSummary? summary)
    {
        if (summary == null || SelectedField == null) return;
        try
        {
            // Same reasoning as StartNewJob: defer the resume so the previous
            // job's coverage is correctly saved to its own folder first.
            _openFieldResumingJob(
                SelectedField.DirectoryPath,
                SelectedField.Name,
                summary.TaskName);
            _close();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    private void UseLastNotes()
    {
        var mostRecent = JobsForSelectedField.FirstOrDefault();
        if (mostRecent != null)
            NewJobNotes = mostRecent.Notes;
    }
}
