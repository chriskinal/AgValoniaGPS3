using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using AgValoniaGPS.Services.Interfaces;

namespace AgValoniaGPS.ViewModels.Wizards.SteerWizard;

/// <summary>
/// Step for selecting the A/D converter type.
/// Options: Differential (ADS1115) or Single-ended
/// </summary>
public partial class ADConverterStepViewModel : WizardStepViewModel
{
    private readonly IConfigurationService _configService;

    public override string Title => "A/D Converter";

    public override string Description =>
        "Select the type of analog-to-digital converter used for the wheel angle sensor. " +
        "Differential mode (ADS1115) provides better noise rejection. " +
        "Single-ended mode uses a standard ADC input.";

    public override bool CanSkip => true;

    [ObservableProperty]
    private int _adConverter;

    /// <summary>
    /// True when differential ADC is selected.
    /// </summary>
    public bool IsDifferentialSelected => AdConverter == 0;

    /// <summary>
    /// True when single-ended ADC is selected.
    /// </summary>
    public bool IsSingleSelected => AdConverter == 1;

    public ADConverterStepViewModel(IConfigurationService configService)
    {
        _configService = configService;
    }

    protected override void OnEntering()
    {
        AdConverter = _configService.Store.AutoSteer.AdConverter;
    }

    protected override void OnLeaving()
    {
        _configService.Store.AutoSteer.AdConverter = AdConverter;
    }

    partial void OnAdConverterChanged(int value)
    {
        OnPropertyChanged(nameof(IsDifferentialSelected));
        OnPropertyChanged(nameof(IsSingleSelected));
    }

    public void SelectDifferential() => AdConverter = 0;
    public void SelectSingle() => AdConverter = 1;

    public override Task<bool> ValidateAsync()
    {
        ClearValidation();
        return Task.FromResult(true);
    }
}
