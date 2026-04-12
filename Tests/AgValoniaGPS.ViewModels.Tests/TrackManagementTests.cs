using System.ComponentModel;
using AgValoniaGPS.Models.Base;
using AgValoniaGPS.Models.Track;

namespace AgValoniaGPS.ViewModels.Tests;

[TestFixture]
public class TrackManagementTests
{
    [Test]
    public void SavedTracks_IsAccessible()
    {
        var vm = new MainViewModelBuilder().Build();

        Assert.That(vm.SavedTracks, Is.Not.Null);
        Assert.That(vm.SavedTracks, Is.Empty);
    }

    [Test]
    public void SelectedTrack_PropertyChange_FiresNotification()
    {
        var vm = new MainViewModelBuilder().Build();

        bool fired = false;
        vm.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(vm.SelectedTrack))
                fired = true;
        };

        vm.SelectedTrack = new Track
        {
            Name = "Test",
            Points = new List<Vec3> { new(0, 0, 0), new(0, 100, 0) }
        };

        Assert.That(fired, Is.True);
    }

    [Test]
    public void SettingSelectedTrackToNull_ClearsHasActiveTrack()
    {
        var vm = new MainViewModelBuilder().Build();

        var track = new Track
        {
            Name = "AB1",
            Points = new List<Vec3> { new(0, 0, 0), new(0, 100, 0) }
        };

        vm.SelectedTrack = track;
        Assert.That(vm.HasActiveTrack, Is.True);

        vm.SelectedTrack = null;
        Assert.That(vm.HasActiveTrack, Is.False);
    }

    [Test]
    public void SelectedTrack_Setter_SetsIsActiveOnTrack()
    {
        var vm = new MainViewModelBuilder().Build();

        var track = new Track
        {
            Name = "AB1",
            Points = new List<Vec3> { new(0, 0, 0), new(0, 100, 0) }
        };

        vm.SelectedTrack = track;

        Assert.That(track.IsActive, Is.True);
    }

    [Test]
    public void PreviousTrack_IsDeactivatedOnNewSelection()
    {
        var vm = new MainViewModelBuilder().Build();

        var track1 = new Track
        {
            Name = "AB1",
            Points = new List<Vec3> { new(0, 0, 0), new(0, 100, 0) }
        };
        var track2 = new Track
        {
            Name = "AB2",
            Points = new List<Vec3> { new(10, 0, 0), new(10, 100, 0) }
        };

        vm.SelectedTrack = track1;
        vm.SelectedTrack = track2;

        Assert.That(track1.IsActive, Is.False);
        Assert.That(track2.IsActive, Is.True);
    }
}
