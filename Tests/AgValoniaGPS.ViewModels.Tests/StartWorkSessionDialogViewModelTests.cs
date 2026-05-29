using AgValoniaGPS.Models;
using AgValoniaGPS.Models.Job;
using AgValoniaGPS.Models.State;
using AgValoniaGPS.Services;
using AgValoniaGPS.Services.Interfaces;
using AgValoniaGPS.ViewModels;
using NSubstitute;

namespace AgValoniaGPS.ViewModels.Tests;

[TestFixture]
public class StartWorkSessionDialogViewModelTests
{
    private IFieldService _fields = null!;
    private IJobService _jobs = null!;
    private ISettingsService _settings = null!;
    private ApplicationState _appState = null!;

    private List<(string path, string name, string workType, string notes, string? taskName)> _newJobCalls = null!;
    private List<(string path, string name, string taskName)> _resumeCalls = null!;
    private List<(string path, string name)> _openOnlyCalls = null!;
    private List<(string message, Action onConfirm)> _confirmCalls = null!;
    private int _closeCount;

    [SetUp]
    public void SetUp()
    {
        _fields = Substitute.For<IFieldService>();
        _jobs = Substitute.For<IJobService>();
        _settings = Substitute.For<ISettingsService>();
        _settings.Settings.Returns(new AppSettings { FieldsDirectory = "/tmp/agtest" });
        _jobs.SuggestWorkTypes().Returns(JobWorkTypeSuggestions.Seed);
        _jobs.ListJobs(Arg.Any<string>()).Returns(Array.Empty<JobSummary>());
        _appState = new ApplicationState();

        _newJobCalls = new();
        _resumeCalls = new();
        _openOnlyCalls = new();
        _confirmCalls = new();
        _closeCount = 0;
    }

    private StartWorkSessionDialogViewModel BuildVm() =>
        new StartWorkSessionDialogViewModel(
            _fields, _jobs, _settings, _appState,
            close: () => _closeCount++,
            openField: (p, n) => _openOnlyCalls.Add((p, n)),
            openFieldStartingNewJob: (p, n, w, x, t) => _newJobCalls.Add((p, n, w, x, t)),
            openFieldResumingJob: (p, n, t) => _resumeCalls.Add((p, n, t)),
            confirm: (msg, action) => _confirmCalls.Add((msg, action)),
            confirmWithOption: (_, _, _, _, _) => { },
            showResumeHistory: () => { },
            driveIn: () => { });

    [Test]
    public void StartNewJob_DoesNotCallJobServiceCreate_DefersViaOpenField()
    {
        // Bug from manual testing: dialog used to call CreateJob directly,
        // mutating ActiveJob before CloseFieldAsync ran. Coverage from the
        // previous job got saved into the new job's folder. Fix routes the
        // create through MainViewModel's pending-intent so the previous job
        // is closed (and saved) cleanly first.
        var vm = BuildVm();
        vm.Fields.Add(new NearbyField("north40", "/tmp/agtest/north40", 0, 0));
        vm.SelectedField = vm.Fields[0];
        vm.NewJobWorkType = "spraying";
        vm.NewJobNotes = "first pass";

        vm.StartNewJobCommand.Execute(null);

        Assert.That(vm.StatusMessage, Is.Null, "no exception should be swallowed by StartNewJob");
        _jobs.DidNotReceiveWithAnyArgs().CreateJob(default!, default!, default!, default);
        Assert.That(_newJobCalls, Has.Count.EqualTo(1));
        Assert.That(_newJobCalls[0].path, Is.EqualTo("/tmp/agtest/north40"));
        Assert.That(_newJobCalls[0].name, Is.EqualTo("north40"));
        Assert.That(_newJobCalls[0].workType, Is.EqualTo("spraying"));
        Assert.That(_newJobCalls[0].notes, Is.EqualTo("first pass"));
        Assert.That(_closeCount, Is.EqualTo(1));
    }

    [Test]
    public void ResumeJob_DoesNotCallJobServiceResume_DefersViaOpenField()
    {
        var vm = BuildVm();
        vm.Fields.Add(new NearbyField("north40", "/tmp/agtest/north40", 0, 0));
        vm.SelectedField = vm.Fields[0];
        var summary = new JobSummary(
            Guid.NewGuid(), "north40", "2026-05-01_spraying",
            "spraying", "", DateTime.Now, null, DateTime.Now, JobStatus.InProgress);

        vm.ResumeJobCommand.Execute(summary);

        _jobs.DidNotReceiveWithAnyArgs().ResumeJob(default!, default!);
        Assert.That(_resumeCalls, Has.Count.EqualTo(1));
        Assert.That(_resumeCalls[0].taskName, Is.EqualTo("2026-05-01_spraying"));
        Assert.That(_closeCount, Is.EqualTo(1));
    }

    [Test]
    public void OnNewJobWorkTypeChanged_AutoFillsTaskName_UntilUserEdits()
    {
        var vm = BuildVm();
        vm.Fields.Add(new NearbyField("f", "/tmp/agtest/f", 0, 0));
        vm.SelectedField = vm.Fields[0];

        vm.NewJobWorkType = "spraying";
        var autoFilled = vm.NewJobTaskName;
        Assert.That(autoFilled, Does.EndWith("_spraying"));

        // User edits the task name (e.g. inserts date/time or types).
        vm.MarkTaskNameUserEdited();

        // Subsequent work-type change must NOT clobber the user's edit.
        vm.NewJobWorkType = "seeding";
        Assert.That(vm.NewJobTaskName, Is.EqualTo(autoFilled));
    }

    [Test]
    public void SelectingDifferentField_ResetsUserEditedFlag()
    {
        var vm = BuildVm();
        vm.Fields.Add(new NearbyField("a", "/tmp/agtest/a", 0, 0));
        vm.Fields.Add(new NearbyField("b", "/tmp/agtest/b", 0, 0));

        vm.SelectedField = vm.Fields[0];
        vm.NewJobWorkType = "spraying";
        vm.MarkTaskNameUserEdited();
        var aTaskName = vm.NewJobTaskName;

        // Switching fields starts a fresh form. The auto-fill should re-arm.
        vm.SelectedField = vm.Fields[1];
        vm.NewJobWorkType = "seeding";
        Assert.That(vm.NewJobTaskName, Does.EndWith("_seeding"));
        Assert.That(vm.NewJobTaskName, Is.Not.EqualTo(aTaskName));
    }

    [Test]
    public void StartNewJob_NoFieldSelected_DoesNothing()
    {
        var vm = BuildVm();
        Assert.That(vm.SelectedField, Is.Null);

        vm.StartNewJobCommand.Execute(null);

        Assert.That(_newJobCalls, Is.Empty);
        Assert.That(_closeCount, Is.EqualTo(0));
    }

    [Test]
    public void Refresh_AutoSelectsActiveField_WhenOneIsOpen()
    {
        // Operator already has a field open and reaches for "Start Session".
        // The dialog should highlight that field rather than the first row.
        var activeField = new Field { Name = "south20", DirectoryPath = "/tmp/agtest/south20" };
        _fields.ActiveField.Returns(activeField);
        _fields.GetAvailableFields(Arg.Any<string>())
            .Returns(new List<string> { "north40", "south20", "east10" });

        var vm = BuildVm();
        vm.Refresh();

        Assert.That(vm.SelectedField, Is.Not.Null);
        Assert.That(vm.SelectedField!.Name, Is.EqualTo("south20"));
    }

    [Test]
    public void Refresh_NoActiveField_FallsBackToFirstRow()
    {
        _fields.ActiveField.Returns((Field?)null);
        _fields.GetAvailableFields(Arg.Any<string>())
            .Returns(new List<string> { "alpha", "bravo" });

        var vm = BuildVm();
        vm.Refresh();

        Assert.That(vm.SelectedField, Is.Not.Null);
        Assert.That(vm.SelectedField!.Name, Is.EqualTo("alpha"));
    }

    [Test]
    public void DeleteJob_GoesThroughConfirmation_BeforeCallingService()
    {
        var vm = BuildVm();
        vm.Fields.Add(new NearbyField("f", "/tmp/agtest/f", 0, 0));
        vm.SelectedField = vm.Fields[0];
        var summary = new JobSummary(
            Guid.NewGuid(), "f", "2026-04-01_spraying", "spraying", "",
            DateTime.Now, null, DateTime.Now, JobStatus.Done);

        vm.DeleteJobCommand.Execute(summary);

        // Service must NOT be called until the operator confirms.
        Assert.That(_confirmCalls, Has.Count.EqualTo(1));
        _jobs.DidNotReceiveWithAnyArgs().DeleteJob(default!, default!);

        // Simulate confirm.
        _confirmCalls[0].onConfirm();

        _jobs.Received(1).DeleteJob("f", "2026-04-01_spraying");
    }

    [Test]
    public void DeleteJob_NullJob_NoOp()
    {
        var vm = BuildVm();
        vm.Fields.Add(new NearbyField("f", "/tmp/agtest/f", 0, 0));
        vm.SelectedField = vm.Fields[0];

        vm.DeleteJobCommand.Execute(null);

        Assert.That(_confirmCalls, Is.Empty);
    }

    [Test]
    public void DeleteJobCommand_DisabledForActiveJob_EnabledForOthers()
    {
        // Setup: one job that's active, one that isn't.
        var activeId = Guid.NewGuid();
        var active = new Job
        {
            Id = activeId,
            FieldName = "f",
            TaskName = "2026-05-05_active",
            WorkType = "spraying",
            Status = JobStatus.InProgress
        };
        _jobs.ActiveJob.Returns(active);

        var vm = BuildVm();

        var idleSummary = new JobSummary(
            Guid.NewGuid(), "f", "2026-04-01_old", "spraying", "",
            DateTime.Now, null, DateTime.Now, JobStatus.Done);
        var activeSummary = new JobSummary(
            activeId, "f", "2026-05-05_active", "spraying", "",
            DateTime.Now, null, DateTime.Now, JobStatus.InProgress);

        Assert.That(vm.DeleteJobCommand.CanExecute(idleSummary), Is.True);
        Assert.That(vm.DeleteJobCommand.CanExecute(activeSummary), Is.False,
            "Cannot delete the currently-active job");
        Assert.That(vm.DeleteJobCommand.CanExecute(null), Is.False);
    }
}
