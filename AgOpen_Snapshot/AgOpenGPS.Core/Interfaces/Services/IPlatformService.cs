using System.Collections.Generic;

namespace AgOpenGPS.Core.Interfaces.Services
{
    /// <summary>
    /// Platform-specific services abstraction for cross-platform support.
    /// Implementations handle Windows, Linux, and macOS specific functionality.
    /// </summary>
    public interface IPlatformService
    {
        /// <summary>
        /// Gets the path where application configuration should be stored.
        /// </summary>
        string GetConfigurationPath();

        /// <summary>
        /// Gets the path where application data should be stored.
        /// </summary>
        string GetDataPath();

        /// <summary>
        /// Brings a window to the foreground (platform-specific implementation).
        /// </summary>
        /// <param name="processName">The name of the process to bring forward</param>
        /// <returns>True if successful</returns>
        bool BringWindowToFront(string processName);

        /// <summary>
        /// Gets available serial ports on the system.
        /// </summary>
        IEnumerable<string> GetSerialPorts();

        /// <summary>
        /// Opens a file dialog and returns the selected file path.
        /// </summary>
        /// <param name="title">Dialog title</param>
        /// <param name="filter">File type filter</param>
        /// <param name="initialDirectory">Starting directory</param>
        /// <returns>Selected file path, or null if cancelled</returns>
        string ShowOpenFileDialog(string title, string filter, string initialDirectory);

        /// <summary>
        /// Opens a save file dialog and returns the selected file path.
        /// </summary>
        /// <param name="title">Dialog title</param>
        /// <param name="filter">File type filter</param>
        /// <param name="initialDirectory">Starting directory</param>
        /// <returns>Selected file path, or null if cancelled</returns>
        string ShowSaveFileDialog(string title, string filter, string initialDirectory);
    }
}