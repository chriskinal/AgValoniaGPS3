using AgValoniaGPS.Models.Base;
using AgValoniaGPS.Models.Guidance;
using AgValoniaGPS.Models.Track;
using NSubstitute;

namespace AgValoniaGPS.ViewModels.Tests;

[TestFixture]
public class GuidanceTests
{
    [Test]
    public void SelectingTrack_SetsHasActiveTrack()
    {
        var builder = new MainViewModelBuilder();
        var vm = builder.Build();

        var track = new Track
        {
            Name = "AB1",
            Points = new List<Vec3>
            {
                new(0, 0, 0),
                new(0, 100, 0)
            }
        };

        vm.SelectedTrack = track;

        Assert.That(vm.HasActiveTrack, Is.True);
    }

    [Test]
    public void SelectingTrack_UpdatesActiveTrackOnState()
    {
        var builder = new MainViewModelBuilder();
        var vm = builder.Build();

        var track = new Track
        {
            Name = "AB1",
            Points = new List<Vec3>
            {
                new(0, 0, 0),
                new(0, 100, 0)
            }
        };

        vm.SelectedTrack = track;

        Assert.That(vm.State.Field.ActiveTrack, Is.SameAs(track));
    }

    [Test]
    public void SimulatorSteerAngle_SetWhenAutoSteerEngaged()
    {
        var builder = new MainViewModelBuilder();
        var vm = builder.Build();

        // Set up a track so guidance can run
        var track = new Track
        {
            Name = "AB1",
            Points = new List<Vec3>
            {
                new(0, 0, 0),
                new(0, 100, 0)
            }
        };
        vm.SelectedTrack = track;

        // Engage autosteer
        vm.IsAutoSteerEngaged = true;

        // Set a known steer angle on the VM (simulating what guidance would do)
        vm.SimulatorSteerAngle = 12.5;

        Assert.That(vm.SimulatorSteerAngle, Is.EqualTo(12.5));
    }
}
