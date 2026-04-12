namespace AgValoniaGPS.ViewModels.Tests;

[TestFixture]
public class SectionControlTests
{
    [Test]
    public void ToggleSectionMasterCommand_TogglesSectionMasterOn()
    {
        var vm = new MainViewModelBuilder().Build();

        bool initialState = vm.IsSectionMasterOn;

        vm.ToggleSectionMasterCommand!.Execute(null);

        Assert.That(vm.IsSectionMasterOn, Is.Not.EqualTo(initialState));
    }

    [Test]
    public void ToggleSectionCommand_WithValidIndex_DoesNotThrow()
    {
        var builder = new MainViewModelBuilder();
        var vm = builder.Build();

        // Execute with section index 0 (string param, matching real usage)
        Assert.DoesNotThrow(() => vm.ToggleSectionCommand!.Execute("0"));
    }

    [Test]
    public void SectionActiveProperties_AreAccessible()
    {
        var vm = new MainViewModelBuilder().Build();

        // All section active properties should be false by default
        Assert.That(vm.Section1Active, Is.False);
        Assert.That(vm.Section2Active, Is.False);
        Assert.That(vm.Section3Active, Is.False);
        Assert.That(vm.Section4Active, Is.False);

        // Can set them
        vm.Section1Active = true;
        Assert.That(vm.Section1Active, Is.True);
    }
}
