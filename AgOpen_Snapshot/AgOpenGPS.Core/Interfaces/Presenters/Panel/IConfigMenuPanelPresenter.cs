using AgOpenGPS.Core.ViewModels;

namespace AgOpenGPS.Core.Interfaces
{
    public interface IConfigMenuPanelPresenter
    {
        void ShowConfigMenuDialog(ConfigMenuViewModel viewModel);
        void CloseConfigMenuDialog();

        void ShowConfigDialog(ConfigViewModel viewModel);
        void CloseConfigDialog();
    }
}
