namespace AgValoniaGPS.ViewModels.Tests;

[TestFixture]
public class GpsHandlingTests
{
    [Test]
    public void GpsProperties_AreSettable()
    {
        var vm = new MainViewModelBuilder().Build();

        vm.Latitude = 40.1234;
        vm.Longitude = -89.5678;
        vm.Heading = 270.0;
        vm.Speed = 2.78; // m/s ~= 10 km/h

        Assert.That(vm.Latitude, Is.EqualTo(40.1234));
        Assert.That(vm.Longitude, Is.EqualTo(-89.5678));
        Assert.That(vm.Heading, Is.EqualTo(270.0));
        Assert.That(vm.Speed, Is.EqualTo(2.78));
    }

    [Test]
    public void SpeedKmh_ComputedFromSpeed()
    {
        var vm = new MainViewModelBuilder().Build();

        vm.Speed = 2.78; // m/s
        // SpeedKmh = abs(speed) * 3.6
        Assert.That(vm.SpeedKmh, Is.EqualTo(2.78 * 3.6).Within(0.01));
    }

    [Test]
    public void StatusMessage_CanBeSet()
    {
        var vm = new MainViewModelBuilder().Build();

        vm.StatusMessage = "Test status";

        Assert.That(vm.StatusMessage, Is.EqualTo("Test status"));
    }

    [Test]
    public void EastingNorthing_AreSettable()
    {
        var vm = new MainViewModelBuilder().Build();

        vm.Easting = 500.0;
        vm.Northing = 1000.0;

        Assert.That(vm.Easting, Is.EqualTo(500.0));
        Assert.That(vm.Northing, Is.EqualTo(1000.0));
    }
}
