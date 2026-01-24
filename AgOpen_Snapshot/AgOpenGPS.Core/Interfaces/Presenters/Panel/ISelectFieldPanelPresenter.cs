using AgOpenGPS.Core.ViewModels;

namespace AgOpenGPS.Core.Interfaces
{
    public interface ISelectFieldPanelPresenter
    {
        void ShowSelectFieldMenuDialog(SelectFieldMenuViewModel viewModel);
        void CloseSelectFieldMenuDialog();
        void ShowSelectNearFieldDialog(SelectNearFieldViewModel viewModel);
        void CloseSelectNearFieldDialog();
        void ShowSelectFieldDialog(SelectFieldViewModel viewModel);
        void CloseSelectFieldDialog();
        void ShowCreateFromExistingFieldDialog(CreateFromExistingFieldViewModel viewModel);
        void CloseCreateFromExistingFieldDialog();

        bool ShowConfirmDeleteMessageBox(string fieldName);
    }
}
