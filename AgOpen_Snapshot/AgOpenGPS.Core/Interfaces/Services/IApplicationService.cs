using System;

namespace AgOpenGPS.Core.Interfaces.Services
{
    /// <summary>
    /// Main application service that coordinates application lifecycle and state.
    /// </summary>
    public interface IApplicationService
    {
        /// <summary>
        /// Gets the application core instance.
        /// </summary>
        ApplicationCore Core { get; }

        /// <summary>
        /// Event raised when the application state changes.
        /// </summary>
        event EventHandler<ApplicationStateChangedEventArgs> StateChanged;

        /// <summary>
        /// Initializes the application and loads saved state.
        /// </summary>
        void Initialize();

        /// <summary>
        /// Starts the main application loop and GPS processing.
        /// </summary>
        void Start();

        /// <summary>
        /// Pauses GPS processing and guidance.
        /// </summary>
        void Pause();

        /// <summary>
        /// Stops the application and saves state.
        /// </summary>
        void Stop();

        /// <summary>
        /// Gets the current application state.
        /// </summary>
        ApplicationState CurrentState { get; }
    }

    /// <summary>
    /// Application state enumeration.
    /// </summary>
    public enum ApplicationState
    {
        Uninitialized,
        Initialized,
        Running,
        Paused,
        Stopped
    }

    /// <summary>
    /// Event arguments for application state changes.
    /// </summary>
    public class ApplicationStateChangedEventArgs : EventArgs
    {
        public ApplicationState PreviousState { get; set; }
        public ApplicationState NewState { get; set; }
    }
}