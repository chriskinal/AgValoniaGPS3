// AgValoniaGPS
// Copyright (C) 2024-2025 AgValoniaGPS Contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program. If not, see <https://www.gnu.org/licenses/>.

using System.Collections.Generic;

namespace AgValoniaGPS.Services.Interfaces
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