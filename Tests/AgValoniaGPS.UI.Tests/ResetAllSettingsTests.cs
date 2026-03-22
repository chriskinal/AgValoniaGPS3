using AgValoniaGPS.Models.Configuration;
using AgValoniaGPS.Models.State;
using NSubstitute;

namespace AgValoniaGPS.UI.Tests;

/// <summary>
/// Tests for the Reset All Settings command.
/// </summary>
[TestFixture]
public class ResetAllSettingsTests
{
    [Test]
    public void ResetCommand_ShowsConfirmationDialog()
    {
        var vm = new MainViewModelBuilder().Build();

        vm.ResetAllSettingsCommand!.Execute(null);

        Assert.That(vm.State.UI.ActiveDialog, Is.EqualTo(DialogType.Confirmation));
    }

    [Test]
    public void ResetCommand_WhenConfirmed_ResetsSettingsService()
    {
        var builder = new MainViewModelBuilder();
        var vm = builder.Build();

        // Open and confirm
        vm.ResetAllSettingsCommand!.Execute(null);
        vm.ConfirmConfirmationDialogCommand!.Execute(null);

        builder.SettingsService.Received(1).ResetToDefaults();
        builder.SettingsService.Received(1).Save();
    }

    [Test]
    public void ResetCommand_WhenConfirmed_ResetsConfigurationStore()
    {
        var original = ConfigurationStore.Instance;
        var vm = new MainViewModelBuilder().Build();

        vm.ResetAllSettingsCommand!.Execute(null);
        vm.ConfirmConfirmationDialogCommand!.Execute(null);

        // ConfigurationStore.Instance should be a fresh instance
        Assert.That(ConfigurationStore.Instance, Is.Not.SameAs(original));
    }

    [Test]
    public void ResetCommand_WhenCancelled_DoesNotReset()
    {
        var builder = new MainViewModelBuilder();
        var vm = builder.Build();

        vm.ResetAllSettingsCommand!.Execute(null);
        vm.CancelConfirmationDialogCommand!.Execute(null);

        builder.SettingsService.DidNotReceive().ResetToDefaults();
        builder.SettingsService.DidNotReceive().Save();
    }

    [Test]
    public void ResetCommand_WhenCancelled_ClosesDialog()
    {
        var vm = new MainViewModelBuilder().Build();

        vm.ResetAllSettingsCommand!.Execute(null);
        vm.CancelConfirmationDialogCommand!.Execute(null);

        Assert.That(vm.State.UI.ActiveDialog, Is.EqualTo(DialogType.None));
    }
}
