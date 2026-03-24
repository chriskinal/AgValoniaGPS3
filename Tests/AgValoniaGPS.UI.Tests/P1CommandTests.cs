using AgValoniaGPS.Models.Track;
using AgValoniaGPS.Models.Base;

namespace AgValoniaGPS.UI.Tests;

/// <summary>
/// Tests for U-turn command (#46), headland extend/shrink (#47),
/// cycle lines and flag placement (#48).
/// </summary>
[TestFixture]
public class P1CommandTests
{
    private static Track CreateTestTrack() => new()
    {
        Name = "Test AB",
        Type = TrackType.ABLine,
        Points = new List<Vec3> { new(0, 0, 0), new(0, 100, 0) },
        IsVisible = true,
        IsActive = true
    };

    #region Issue #46 - U-Turn Command

    [Test]
    public void UTurnCommand_NoTrack_ShowsError()
    {
        var vm = new MainViewModelBuilder().Build();

        vm.UTurnCommand!.Execute(null);

        Assert.That(vm.StatusMessage, Does.Contain("No track selected"));
    }

    [Test]
    public void UTurnCommand_NotEngaged_ShowsError()
    {
        var vm = new MainViewModelBuilder().Build();
        var track = CreateTestTrack();
        vm.SavedTracks.Add(track);
        vm.SelectedTrack = track;

        vm.UTurnCommand!.Execute(null);

        Assert.That(vm.StatusMessage, Does.Contain("Enable autosteer"));
    }

    #endregion

    #region Issue #48 - CycleLines

    [Test]
    public void CycleABLines_NoTracks_ShowsMessage()
    {
        var vm = new MainViewModelBuilder().Build();

        vm.CycleABLinesCommand!.Execute(null);

        Assert.That(vm.StatusMessage, Does.Contain("No tracks"));
    }

    [Test]
    public void CycleABLines_WithTracks_CyclesToNext()
    {
        var vm = new MainViewModelBuilder().Build();
        var track1 = new Track { Name = "Track 1", Type = TrackType.ABLine, Points = new List<Vec3> { new(0, 0, 0), new(0, 100, 0) } };
        var track2 = new Track { Name = "Track 2", Type = TrackType.ABLine, Points = new List<Vec3> { new(10, 0, 0), new(10, 100, 0) } };
        vm.SavedTracks.Add(track1);
        vm.SavedTracks.Add(track2);
        vm.SelectedTrack = track1;

        vm.CycleABLinesCommand!.Execute(null);

        Assert.That(vm.SelectedTrack, Is.SameAs(track2));
    }

    [Test]
    public void CycleABLines_AtEnd_WrapsToFirst()
    {
        var vm = new MainViewModelBuilder().Build();
        var track1 = new Track { Name = "Track 1", Type = TrackType.ABLine, Points = new List<Vec3> { new(0, 0, 0), new(0, 100, 0) } };
        var track2 = new Track { Name = "Track 2", Type = TrackType.ABLine, Points = new List<Vec3> { new(10, 0, 0), new(10, 100, 0) } };
        vm.SavedTracks.Add(track1);
        vm.SavedTracks.Add(track2);
        vm.SelectedTrack = track2;

        vm.CycleABLinesCommand!.Execute(null);

        Assert.That(vm.SelectedTrack, Is.SameAs(track1));
    }

    #endregion

    #region Issue #48 - Flags

    [Test]
    public void PlaceRedFlag_NoGps_ShowsError()
    {
        var vm = new MainViewModelBuilder().Build();

        vm.PlaceRedFlagCommand!.Execute(null);

        Assert.That(vm.StatusMessage, Does.Contain("No GPS position"));
    }

    [Test]
    public void PlaceRedFlag_WithPosition_PlacesFlag()
    {
        var vm = new MainViewModelBuilder().Build();
        vm.Easting = 100.0;
        vm.Northing = 200.0;
        vm.Heading = 45.0;

        vm.PlaceRedFlagCommand!.Execute(null);

        Assert.That(vm.StatusMessage, Does.Contain("Red flag"));
    }

    [Test]
    public void DeleteAllFlags_ClearsFlags()
    {
        var vm = new MainViewModelBuilder().Build();
        vm.Easting = 100.0;
        vm.Northing = 200.0;
        vm.PlaceRedFlagCommand!.Execute(null);
        vm.PlaceGreenFlagCommand!.Execute(null);

        vm.DeleteAllFlagsCommand!.Execute(null);

        Assert.That(vm.StatusMessage, Does.Contain("Deleted 2 flags"));
    }

    [Test]
    public void DeleteAllFlags_Empty_ShowsNoFlags()
    {
        var vm = new MainViewModelBuilder().Build();

        vm.DeleteAllFlagsCommand!.Execute(null);

        Assert.That(vm.StatusMessage, Does.Contain("No flags"));
    }

    #endregion

    #region Issue #47 - Headland Extend/Shrink

    [Test]
    public void ExtendHeadlandA_NoHeadland_ShowsError()
    {
        var vm = new MainViewModelBuilder().Build();

        vm.ExtendHeadlandACommand!.Execute(null);

        Assert.That(vm.StatusMessage, Does.Contain("No headland"));
    }

    [Test]
    public void ShrinkHeadlandA_NoHeadland_ShowsError()
    {
        var vm = new MainViewModelBuilder().Build();

        vm.ShrinkHeadlandACommand!.Execute(null);

        Assert.That(vm.StatusMessage, Does.Contain("No headland"));
    }

    [Test]
    public void HeadlandDistance_ClampsToMinimum()
    {
        var vm = new MainViewModelBuilder().Build();
        vm.HeadlandDistance = 0.5;

        Assert.That(vm.HeadlandDistance, Is.EqualTo(1.0));
    }

    [Test]
    public void HeadlandDistance_ClampsToMaximum()
    {
        var vm = new MainViewModelBuilder().Build();
        vm.HeadlandDistance = 150.0;

        Assert.That(vm.HeadlandDistance, Is.EqualTo(100.0));
    }

    #endregion
}
