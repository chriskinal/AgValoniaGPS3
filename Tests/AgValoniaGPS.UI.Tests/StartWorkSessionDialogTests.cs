using AgValoniaGPS.Models;
using AgValoniaGPS.Models.Job;
using AgValoniaGPS.Models.State;
using AgValoniaGPS.Services;
using AgValoniaGPS.Services.Interfaces;
using AgValoniaGPS.ViewModels;
using NSubstitute;

namespace AgValoniaGPS.UI.Tests;

/// <summary>
/// Regression fence for #XXX: the Start Work Session dialog used to call
/// IFieldService.FindFieldsNear with a 100 km cap when a GPS fix was
/// available, silently dropping every field whose origin was &gt; 100 km
/// away or had not yet been georeferenced (origin == (0,0)). The
/// canonical row source is now IFieldService.GetAvailableFields, with
/// FindFieldsNear used only to enrich entries with distance for sorting.
/// </summary>
[TestFixture]
public class StartWorkSessionDialogTests
{
    private const string FieldsRoot = "/tmp/fields";

    private static StartWorkSessionDialogViewModel BuildVm(
        IFieldService fieldService,
        ApplicationState? appState = null)
    {
        var settings = Substitute.For<ISettingsService>();
        settings.Settings.Returns(new AppSettings { FieldsDirectory = FieldsRoot });

        var jobs = Substitute.For<IJobService>();
        jobs.SuggestWorkTypes().Returns(Array.Empty<string>());
        jobs.ListJobs(Arg.Any<string>()).Returns(Array.Empty<JobSummary>());

        return new StartWorkSessionDialogViewModel(
            fieldService: fieldService,
            jobService: jobs,
            settingsService: settings,
            appState: appState ?? new ApplicationState(),
            close: () => { },
            openField: (_, _) => { },
            openFieldStartingNewJob: (_, _, _, _, _) => { },
            openFieldResumingJob: (_, _, _) => { },
            confirm: (_, _) => { });
    }

    [Test]
    public void Refresh_WithFix_PopulatesAllAvailableFields_NearOnesFirst()
    {
        var fields = Substitute.For<IFieldService>();
        fields.GetAvailableFields(FieldsRoot)
            .Returns(new List<string> { "Aaa", "FarAway", "Nearby", "Zzz" });
        fields.FindFieldsNear(FieldsRoot, Arg.Any<double>(), Arg.Any<double>(), Arg.Any<double>())
            .Returns(new List<NearbyField>
            {
                new("Nearby",  $"{FieldsRoot}/Nearby",  DistanceKm: 0.5,    BoundaryAreaHectares: 12),
                new("FarAway", $"{FieldsRoot}/FarAway", DistanceKm: 5000.0, BoundaryAreaHectares: 7)
            });

        var appState = new ApplicationState();
        appState.Vehicle.Latitude = 42.0;
        appState.Vehicle.Longitude = -93.0;

        var vm = BuildVm(fields, appState);
        vm.Refresh();

        // All four fields show up.
        Assert.That(vm.Fields.Select(f => f.Name).ToArray(),
            Is.EqualTo(new[] { "Nearby", "FarAway", "Aaa", "Zzz" }),
            "Known-distance rows first (by distance), then unknown rows alphabetically.");
        Assert.That(vm.StatusMessage, Is.Null);
    }

    [Test]
    public void Refresh_WithFix_EmptyFindFieldsNear_StillShowsAllAvailableFields()
    {
        var fields = Substitute.For<IFieldService>();
        fields.GetAvailableFields(FieldsRoot)
            .Returns(new List<string> { "FieldA", "FieldB" });
        fields.FindFieldsNear(FieldsRoot, Arg.Any<double>(), Arg.Any<double>(), Arg.Any<double>())
            .Returns(Array.Empty<NearbyField>());

        var appState = new ApplicationState();
        appState.Vehicle.Latitude = 42.0;
        appState.Vehicle.Longitude = -93.0;

        var vm = BuildVm(fields, appState);
        vm.Refresh();

        Assert.That(vm.Fields.Select(f => f.Name).ToArray(),
            Is.EqualTo(new[] { "FieldA", "FieldB" }),
            "Refresh must not silently hide fields just because none have a usable origin.");
        Assert.That(vm.StatusMessage, Is.Null);
    }

    [Test]
    public void Refresh_NoFieldsOnDisk_LeavesListEmpty_AndSetsStatusMessage()
    {
        var fields = Substitute.For<IFieldService>();
        fields.GetAvailableFields(FieldsRoot).Returns(new List<string>());

        var vm = BuildVm(fields);
        vm.Refresh();

        Assert.That(vm.Fields, Is.Empty);
        Assert.That(vm.StatusMessage, Does.Contain("No fields found"));
    }
}
