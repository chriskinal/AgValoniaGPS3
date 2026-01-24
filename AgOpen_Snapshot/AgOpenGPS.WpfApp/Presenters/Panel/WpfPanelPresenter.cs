using AgOpenGPS.Core.Interfaces;

namespace AgOpenGPS.WpfApp.Presenters
{
    public class WpfPanelPresenter : IPanelPresenter
    {
        public WpfPanelPresenter()
        {
            ConfigMenuPanelPresenter = new WpfConfigMenuPanelPresenter();
            SelectFieldPanelPresenter = new WpfSelectFieldPanelPresenter();
        }

        public IConfigMenuPanelPresenter ConfigMenuPanelPresenter { get; }
        public ISelectFieldPanelPresenter SelectFieldPanelPresenter { get; }
    }

}
