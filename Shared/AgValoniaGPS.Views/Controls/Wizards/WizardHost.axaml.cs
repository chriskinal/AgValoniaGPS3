using Avalonia.Controls;
using AgValoniaGPS.ViewModels.Wizards;
using AgValoniaGPS.ViewModels.Wizards.SteerWizard;
using AgValoniaGPS.Views.Controls.Wizards.SteerWizard;

namespace AgValoniaGPS.Views.Controls.Wizards;

public partial class WizardHost : UserControl
{
    private ContentControl? _stepContentControl;
    private WizardViewModel? _subscribedWizard;

    public WizardHost()
    {
        InitializeComponent();
    }

    protected override void OnLoaded(Avalonia.Interactivity.RoutedEventArgs e)
    {
        base.OnLoaded(e);

        _stepContentControl = this.FindControl<ContentControl>("StepContentControl");

        // Subscribe to current wizard
        SubscribeToWizard(DataContext as WizardViewModel);

        this.DataContextChanged += WizardHost_DataContextChanged;
    }

    protected override void OnUnloaded(Avalonia.Interactivity.RoutedEventArgs e)
    {
        // Clean up subscriptions to prevent memory leaks
        UnsubscribeFromWizard();
        this.DataContextChanged -= WizardHost_DataContextChanged;

        base.OnUnloaded(e);
    }

    private void WizardHost_DataContextChanged(object? sender, EventArgs e)
    {
        // Unsubscribe from old wizard, subscribe to new one
        SubscribeToWizard(DataContext as WizardViewModel);
    }

    private void SubscribeToWizard(WizardViewModel? wizard)
    {
        // Unsubscribe from old wizard first
        UnsubscribeFromWizard();

        if (wizard != null)
        {
            _subscribedWizard = wizard;
            wizard.PropertyChanged += Wizard_PropertyChanged;
            UpdateStepView(wizard.CurrentStep);
        }
    }

    private void UnsubscribeFromWizard()
    {
        if (_subscribedWizard != null)
        {
            _subscribedWizard.PropertyChanged -= Wizard_PropertyChanged;
            _subscribedWizard = null;
        }
    }

    private void Wizard_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(WizardViewModel.CurrentStep) && sender is WizardViewModel wizard)
        {
            UpdateStepView(wizard.CurrentStep);
        }
    }

    private void UpdateStepView(WizardStepViewModel? step)
    {
        if (_stepContentControl == null || step == null)
            return;

        // Manually create the correct view based on the ViewModel type
        // This bypasses DataTemplate resolution which had caching issues
        Control? view = step switch
        {
            // Group A: Introduction
            WelcomeStepViewModel => new WelcomeStepView(),

            // Group B: Vehicle Dimensions
            WheelbaseStepViewModel => new WheelbaseStepView(),
            TrackWidthStepViewModel => new TrackWidthStepView(),
            AntennaPivotStepViewModel => new AntennaPivotStepView(),
            AntennaHeightStepViewModel => new AntennaHeightStepView(),
            AntennaOffsetStepViewModel => new AntennaOffsetStepView(),

            // Group C: Hardware Configuration
            SteerEnableStepViewModel => new SteerEnableStepView(),
            MotorDriverStepViewModel => new MotorDriverStepView(),
            ADConverterStepViewModel => new ADConverterStepView(),
            InvertSettingsStepViewModel => new InvertSettingsStepView(),
            DanfossStepViewModel => new DanfossStepView(),

            // Group G: Completion
            FinishStepViewModel => new FinishStepView(),
            _ => null
        };

        if (view != null)
        {
            view.DataContext = step;
            _stepContentControl.Content = view;
        }
        else
        {
            _stepContentControl.Content = new TextBlock { Text = $"Unknown step: {step.GetType().Name}" };
        }
    }
}
