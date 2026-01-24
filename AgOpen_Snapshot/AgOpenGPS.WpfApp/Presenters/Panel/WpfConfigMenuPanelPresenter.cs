using AgOpenGPS.Core.Interfaces;
using AgOpenGPS.Core.ViewModels;
using AgOpenGPS.WpfApp.Configuration;

namespace AgOpenGPS.WpfApp
{
    public class WpfConfigMenuPanelPresenter : IConfigMenuPanelPresenter
    {
        private ConfigMenuDialog _configMenuDialog;
        private ConfigDialog _configDialog;

        void IConfigMenuPanelPresenter.ShowConfigMenuDialog(ConfigMenuViewModel viewModel)
        {
            _configMenuDialog = new ConfigMenuDialog
            {
                DataContext = viewModel
            };
            _configMenuDialog.ShowDialog();
        }

        void IConfigMenuPanelPresenter.CloseConfigMenuDialog()
        {
            _configMenuDialog?.Close();
            _configMenuDialog = null;
        }

        void IConfigMenuPanelPresenter.ShowConfigDialog(ConfigViewModel viewModel)
        {
            _configDialog = new ConfigDialog
            {
                DataContext = viewModel
            };
            _configDialog.ShowDialog();
        }

        void IConfigMenuPanelPresenter.CloseConfigDialog()
        {
            _configDialog?.Close();
            _configDialog = null;
        }

    }
}

