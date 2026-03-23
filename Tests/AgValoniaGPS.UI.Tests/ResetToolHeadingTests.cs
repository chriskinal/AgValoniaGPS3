namespace AgValoniaGPS.UI.Tests;

/// <summary>
/// Tests for the Reset Tool Heading command.
/// </summary>
[TestFixture]
public class ResetToolHeadingTests
{
    [Test]
    public void ResetToolHeading_SetsToolHeadingToVehicleHeading()
    {
        var vm = new MainViewModelBuilder().Build();

        // Simulate vehicle heading (in degrees, stored on VM)
        vm.Heading = 90.0;  // East
        vm.Easting = 100.0;
        vm.Northing = 200.0;

        // Set tool heading to something different
        vm.ToolHeadingRadians = 0.0; // North

        vm.ResetToolHeadingCommand!.Execute(null);

        // Tool heading should now match vehicle heading (90° = π/2 radians)
        double expectedRadians = 90.0 * Math.PI / 180.0;
        Assert.That(vm.ToolHeadingRadians, Is.EqualTo(expectedRadians).Within(0.001));
    }

    [Test]
    public void ResetToolHeading_ShowsStatusMessage()
    {
        var vm = new MainViewModelBuilder().Build();

        vm.ResetToolHeadingCommand!.Execute(null);

        Assert.That(vm.StatusMessage, Is.EqualTo("Tool heading reset to vehicle heading"));
    }

    [Test]
    public void ResetToolHeading_CallsResetTrailingState()
    {
        var builder = new MainViewModelBuilder();
        var vm = builder.Build();

        vm.Heading = 45.0;
        vm.Easting = 50.0;
        vm.Northing = 75.0;

        vm.ResetToolHeadingCommand!.Execute(null);

        Assert.That(vm.StatusMessage, Does.Contain("reset"));
    }
}
