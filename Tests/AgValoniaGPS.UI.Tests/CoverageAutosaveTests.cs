using AgValoniaGPS.Models;
using AgValoniaGPS.Models.Job;
using AgValoniaGPS.Services.Interfaces;
using NSubstitute;

namespace AgValoniaGPS.UI.Tests;

/// <summary>
/// Regression fence for the coverage autosave loop in MainViewModel
/// (see MainViewModel.Autosave.cs). Real bug it guards against: if the
/// app crashes mid-session the operator loses every paint stroke since
/// CloseFieldAsync was last called. The autosave timer ticks every
/// 30 s while a field+job is open and persists coverage on a
/// background thread — these tests drive the same code path the timer
/// drives, but synchronously, so we don't have to wait 30 s.
/// </summary>
[TestFixture]
public class CoverageAutosaveTests
{
    [Test]
    public async Task TryAutosaveCoverageAsync_WithFieldAndJobOpen_CallsSaveToFile()
    {
        var builder = new MainViewModelBuilder();
        var vm = builder.Build();

        // Simulate "field is open with an active job" — same state the
        // OpenFieldAsync path leaves the VM in after a successful open.
        vm.IsFieldOpen = true;
        vm.ActiveField = new Field
        {
            Name = "TestField",
            DirectoryPath = "/tmp/TestField"
        };
        builder.JobService.ActiveJob.Returns(new Job
        {
            FieldName = "TestField",
            TaskName = "TestField_2026-05-09_tilling"
        });

        builder.CoverageMapService.ClearReceivedCalls();

        await vm.TryAutosaveCoverageAsync();

        builder.CoverageMapService.Received(1).SaveToFile(
            "/tmp/TestField",
            "TestField_2026-05-09_tilling");
    }

    [Test]
    public async Task TryAutosaveCoverageAsync_WithNoActiveJob_DoesNotSave()
    {
        var builder = new MainViewModelBuilder();
        var vm = builder.Build();

        // Field is open but no job has been started (field-only open).
        // Coverage isn't being written, so there is nothing to persist.
        vm.IsFieldOpen = true;
        vm.ActiveField = new Field
        {
            Name = "TestField",
            DirectoryPath = "/tmp/TestField"
        };
        builder.JobService.ActiveJob.Returns((Job?)null);

        builder.CoverageMapService.ClearReceivedCalls();

        await vm.TryAutosaveCoverageAsync();

        builder.CoverageMapService.DidNotReceive().SaveToFile(
            Arg.Any<string>(),
            Arg.Any<string>());
    }

    [Test]
    public async Task TryAutosaveCoverageAsync_WithNoFieldOpen_DoesNotSave()
    {
        var builder = new MainViewModelBuilder();
        var vm = builder.Build();

        // Pre-condition guard: even if a stale ActiveJob mock is around,
        // a closed field must not trigger a save (would write to whatever
        // path the previous field had).
        vm.IsFieldOpen = false;
        builder.JobService.ActiveJob.Returns(new Job
        {
            FieldName = "TestField",
            TaskName = "TestField_2026-05-09_tilling"
        });

        builder.CoverageMapService.ClearReceivedCalls();

        await vm.TryAutosaveCoverageAsync();

        builder.CoverageMapService.DidNotReceive().SaveToFile(
            Arg.Any<string>(),
            Arg.Any<string>());
    }
}
