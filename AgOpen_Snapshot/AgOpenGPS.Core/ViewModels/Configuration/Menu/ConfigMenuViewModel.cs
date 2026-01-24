using AgOpenGPS.Core.Interfaces;
using System.Windows.Input;

namespace AgOpenGPS.Core.ViewModels
{
    public class ConfigMenuViewModel : DayNightAndUnitsViewModel
    {
        private readonly ApplicationModel _appModel;
        private readonly IConfigMenuPanelPresenter _configMenuPanelPresenter;
        private ConfigViewModel _configViewModel;

        public ConfigMenuViewModel(ApplicationModel appModel, IConfigMenuPanelPresenter configMenuPanelPresenter)
        {
            _appModel = appModel;
            _configMenuPanelPresenter = configMenuPanelPresenter;
            ShowConfigurationDialogCommand = new RelayCommand(ShowConfigurationDialog);
        }

        public ICommand ShowConfigurationDialogCommand { get; }

        private ConfigViewModel ConfigViewModel
        {
            get
            {
                if (_configViewModel == null)
                {
                    _configViewModel =
                        new ConfigViewModel(_appModel);
                }
                return _configViewModel;
            }
        }
        private void ShowConfigurationDialog()
        {
            _configMenuPanelPresenter.CloseConfigMenuDialog();
            ConfigViewModel.UpdateFromSettings();
            _configMenuPanelPresenter.ShowConfigDialog(_configViewModel);
        }
    }
}
