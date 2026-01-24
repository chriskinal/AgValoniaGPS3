using AgOpenGPS.Core.Interfaces;
using AgOpenGPS.Core.ViewModels;
using AgOpenGPS.WpfApp.Field;
using System;
using System.Windows;

namespace AgOpenGPS.WpfApp
{
    public class WpfSelectFieldPanelPresenter : ISelectFieldPanelPresenter
    {
        private SelectFieldMenuDialog _selectFieldMenuDialog;
        private SelectNearFieldDialog _selectNearFieldDialog;
        private CreateFromExistingFieldDialog _createFromExistingFieldDialog;
        private SelectFieldDialog _selectFieldDialog;

        void ISelectFieldPanelPresenter.ShowSelectFieldMenuDialog(SelectFieldMenuViewModel viewModel)
        {
            _selectFieldMenuDialog = new SelectFieldMenuDialog
            {
                DataContext = viewModel
            };
            _selectFieldMenuDialog.ShowDialog();
        }

        void ISelectFieldPanelPresenter.CloseSelectFieldMenuDialog()
        {
            _selectFieldMenuDialog?.Close();
            _selectFieldMenuDialog = null;
        }

        void ISelectFieldPanelPresenter.ShowSelectNearFieldDialog(SelectNearFieldViewModel viewModel)
        {
            viewModel.UpdateFields();
            _selectNearFieldDialog = new SelectNearFieldDialog
            {
                DataContext = viewModel
            };
            _selectNearFieldDialog.ShowDialog();
        }

        void ISelectFieldPanelPresenter.CloseSelectNearFieldDialog()
        {
            _selectNearFieldDialog?.Close();
            _selectFieldDialog = null;
        }

        void ISelectFieldPanelPresenter.ShowCreateFromExistingFieldDialog(
            CreateFromExistingFieldViewModel viewModel)
        {
            viewModel.UpdateFields();
            _createFromExistingFieldDialog = new CreateFromExistingFieldDialog
            {
                DataContext = viewModel
            };
            _createFromExistingFieldDialog.ShowDialog();
        }

        void ISelectFieldPanelPresenter.CloseCreateFromExistingFieldDialog()
        {
            _createFromExistingFieldDialog?.Close();
            _createFromExistingFieldDialog = null;
        }

        void ISelectFieldPanelPresenter.ShowSelectFieldDialog(SelectFieldViewModel viewModel)
        {
            viewModel.UpdateFields();
            _selectFieldDialog = new SelectFieldDialog
            {
                DataContext = viewModel
            };
            _selectFieldDialog.ShowDialog();
        }

        void ISelectFieldPanelPresenter.CloseSelectFieldDialog()
        {
            _selectFieldDialog.Close();
            _selectFieldDialog = null;
        }

        bool ISelectFieldPanelPresenter.ShowConfirmDeleteMessageBox(string fieldName)
        {
            MessageBoxResult result = MessageBox.Show(
                "Are you sure you want to delete field " + fieldName + "?",
                "Delete field",
                MessageBoxButton.OKCancel);
            return result == MessageBoxResult.OK;
        }

    }
}
