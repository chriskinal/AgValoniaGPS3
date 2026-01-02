namespace AgValoniaGPS.ViewModels.Wizards.SteerWizard;

/// <summary>
/// Welcome/Introduction step for the Steer Wizard.
/// </summary>
public class WelcomeStepViewModel : WizardStepViewModel
{
    public override string Title => "Welcome to AutoSteer Setup";

    public override string Description =>
        "This wizard will guide you through configuring your AutoSteer system. " +
        "We'll set up your vehicle dimensions, antenna position, and steering parameters.\n\n" +
        "Make sure your vehicle is on level ground and the GPS receiver is powered on before continuing.";

    public override bool CanGoBack => false; // First step
}
