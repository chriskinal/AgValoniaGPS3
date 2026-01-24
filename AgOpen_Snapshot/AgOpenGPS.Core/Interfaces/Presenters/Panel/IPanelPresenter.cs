using AgOpenGPS.Core.ViewModels;

namespace AgOpenGPS.Core.Interfaces
{
    public interface IPanelPresenter
    {
        ISelectFieldPanelPresenter SelectFieldPanelPresenter { get; }
        IConfigMenuPanelPresenter ConfigMenuPanelPresenter { get; }
    }
}
