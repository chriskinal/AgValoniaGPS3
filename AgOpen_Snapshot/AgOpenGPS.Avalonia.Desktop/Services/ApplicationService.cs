using AgOpenGPS.Core;
using AgOpenGPS.Core.Interfaces;
using AgOpenGPS.Core.Interfaces.Services;
using System;
using System.IO;

namespace AgOpenGPS.Avalonia.Desktop.Services
{
    /// <summary>
    /// Main application service implementation for Avalonia.
    /// Manages the ApplicationCore and coordinates application lifecycle.
    /// </summary>
    public class ApplicationService : IApplicationService
    {
        private readonly IPlatformService _platformService;
        private ApplicationState _currentState;

        public ApplicationService(IPlatformService platformService)
        {
            _platformService = platformService;
            _currentState = ApplicationState.Uninitialized;
        }

        public ApplicationCore Core { get; private set; } = null!;

        public event EventHandler<ApplicationStateChangedEventArgs>? StateChanged;

        public ApplicationState CurrentState
        {
            get => _currentState;
            private set
            {
                if (_currentState != value)
                {
                    var args = new ApplicationStateChangedEventArgs
                    {
                        PreviousState = _currentState,
                        NewState = value
                    };
                    _currentState = value;
                    StateChanged?.Invoke(this, args);
                }
            }
        }

        public void Initialize()
        {
            if (CurrentState != ApplicationState.Uninitialized)
                return;

            // Get the base directory from platform service
            var dataPath = _platformService.GetDataPath();
            var baseDirectory = new DirectoryInfo(dataPath);

            // Create presenters (these will be implemented in Phase 3)
            var panelPresenter = new NullPanelPresenter();
            var errorPresenter = new NullErrorPresenter();

            // Initialize ApplicationCore
            Core = new ApplicationCore(baseDirectory, panelPresenter, errorPresenter);

            CurrentState = ApplicationState.Initialized;
        }

        public void Start()
        {
            if (CurrentState != ApplicationState.Initialized && CurrentState != ApplicationState.Paused)
                return;

            // TODO: Start GPS processing, timers, etc.
            CurrentState = ApplicationState.Running;
        }

        public void Pause()
        {
            if (CurrentState != ApplicationState.Running)
                return;

            // TODO: Pause GPS processing
            CurrentState = ApplicationState.Paused;
        }

        public void Stop()
        {
            if (CurrentState == ApplicationState.Stopped || CurrentState == ApplicationState.Uninitialized)
                return;

            // TODO: Save application state, stop timers, etc.
            CurrentState = ApplicationState.Stopped;
        }
    }

    /// <summary>
    /// Null implementation of IPanelPresenter for Phase 2.
    /// Will be replaced with actual implementation in Phase 3.
    /// </summary>
    internal class NullPanelPresenter : IPanelPresenter
    {
        public ISelectFieldPanelPresenter SelectFieldPanelPresenter => new NullSelectFieldPanelPresenter();
        public IConfigMenuPanelPresenter ConfigMenuPanelPresenter => new NullConfigMenuPanelPresenter();
    }

    internal class NullSelectFieldPanelPresenter : ISelectFieldPanelPresenter
    {
        public void ShowSelectFieldMenuDialog(Core.ViewModels.SelectFieldMenuViewModel viewModel) { }
        public void CloseSelectFieldMenuDialog() { }
        public void ShowSelectNearFieldDialog(Core.ViewModels.SelectNearFieldViewModel viewModel) { }
        public void CloseSelectNearFieldDialog() { }
        public void ShowSelectFieldDialog(Core.ViewModels.SelectFieldViewModel viewModel) { }
        public void CloseSelectFieldDialog() { }
        public void ShowCreateFromExistingFieldDialog(Core.ViewModels.CreateFromExistingFieldViewModel viewModel) { }
        public void CloseCreateFromExistingFieldDialog() { }
        public bool ShowConfirmDeleteMessageBox(string fieldName) => false;
    }

    internal class NullConfigMenuPanelPresenter : IConfigMenuPanelPresenter
    {
        public void ShowConfigMenuDialog(Core.ViewModels.ConfigMenuViewModel viewModel) { }
        public void CloseConfigMenuDialog() { }
        public void ShowConfigDialog(Core.ViewModels.ConfigViewModel viewModel) { }
        public void CloseConfigDialog() { }
    }

    /// <summary>
    /// Null implementation of IErrorPresenter for Phase 2.
    /// Will be replaced with actual implementation in Phase 3.
    /// </summary>
    internal class NullErrorPresenter : IErrorPresenter
    {
        public void PresentTimedMessage(TimeSpan timeSpan, string titleString, string messageString) { }
    }
}