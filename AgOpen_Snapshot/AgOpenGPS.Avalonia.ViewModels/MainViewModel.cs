using AgOpenGPS.Core.Interfaces.Services;
using ReactiveUI;
using System;

namespace AgOpenGPS.Avalonia.ViewModels
{
    /// <summary>
    /// Main view model for the AgOpenGPS Avalonia application.
    /// Coordinates between the UI and the service layer.
    /// </summary>
    public class MainViewModel : ReactiveObject
    {
        private readonly IApplicationService _applicationService;
        private readonly IRenderService _renderService;
        private string _title = "AgOpenGPS - Avalonia (Phase 2)";
        private string _statusText = "Initializing...";
        private string _fieldName = "No Field";
        private bool _isRunning;

        public MainViewModel(IApplicationService applicationService, IRenderService renderService)
        {
            _applicationService = applicationService ?? throw new ArgumentNullException(nameof(applicationService));
            _renderService = renderService ?? throw new ArgumentNullException(nameof(renderService));

            // Subscribe to application state changes
            _applicationService.StateChanged += OnApplicationStateChanged;

            // Initialize the application
            _applicationService.Initialize();
            UpdateStatus("Application initialized");
        }

        /// <summary>
        /// Gets or sets the main window title.
        /// </summary>
        public string Title
        {
            get => _title;
            set => this.RaiseAndSetIfChanged(ref _title, value);
        }

        /// <summary>
        /// Gets or sets the status bar text.
        /// </summary>
        public string StatusText
        {
            get => _statusText;
            set => this.RaiseAndSetIfChanged(ref _statusText, value);
        }

        /// <summary>
        /// Gets or sets the current field name.
        /// </summary>
        public string FieldName
        {
            get => _fieldName;
            set => this.RaiseAndSetIfChanged(ref _fieldName, value);
        }

        /// <summary>
        /// Gets or sets whether the application is running.
        /// </summary>
        public bool IsRunning
        {
            get => _isRunning;
            set => this.RaiseAndSetIfChanged(ref _isRunning, value);
        }

        /// <summary>
        /// Gets the application core from the service.
        /// </summary>
        public AgOpenGPS.Core.ApplicationCore AppCore => _applicationService.Core;

        /// <summary>
        /// Updates the status text with current information.
        /// </summary>
        /// <param name="status">The status message to display</param>
        public void UpdateStatus(string status)
        {
            StatusText = $"{DateTime.Now:HH:mm:ss} - {status}";
        }

        /// <summary>
        /// Starts the application.
        /// </summary>
        public void Start()
        {
            _applicationService.Start();
        }

        /// <summary>
        /// Pauses the application.
        /// </summary>
        public void Pause()
        {
            _applicationService.Pause();
        }

        /// <summary>
        /// Stops the application.
        /// </summary>
        public void Stop()
        {
            _applicationService.Stop();
        }

        private void OnApplicationStateChanged(object? sender, ApplicationStateChangedEventArgs e)
        {
            IsRunning = e.NewState == ApplicationState.Running;
            UpdateStatus($"Application state: {e.NewState}");

            // Update field name if available
            if (AppCore?.ActiveField != null)
            {
                FieldName = AppCore.ActiveField.Name;
            }
        }
    }
}