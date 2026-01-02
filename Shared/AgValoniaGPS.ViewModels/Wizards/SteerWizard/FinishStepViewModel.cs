namespace AgValoniaGPS.ViewModels.Wizards.SteerWizard;

/// <summary>
/// Final step - summary and completion.
/// </summary>
public class FinishStepViewModel : WizardStepViewModel
{
    public override string Title => "Setup Complete";

    public override string Description =>
        "Your basic AutoSteer configuration is complete!\n\n" +
        "Click Finish to save your settings. You can always modify these values " +
        "later in the Configuration panel.\n\n" +
        "For advanced steering calibration (WAS, CPD, Ackermann), run this wizard again " +
        "or use the Tools menu.";

    public override bool CanSkip => false;
}
