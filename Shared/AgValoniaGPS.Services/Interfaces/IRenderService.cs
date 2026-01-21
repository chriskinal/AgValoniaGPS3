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

namespace AgValoniaGPS.Services.Interfaces
{
    /// <summary>
    /// Service for managing the OpenGL rendering pipeline.
    /// Abstracts rendering operations from platform-specific OpenGL implementations.
    /// </summary>
    public interface IRenderService
    {
        /// <summary>
        /// Initializes the rendering context and resources.
        /// </summary>
        void Initialize();

        /// <summary>
        /// Renders a single frame.
        /// </summary>
        /// <param name="deltaTime">Time since last frame in seconds</param>
        void Render(double deltaTime);

        /// <summary>
        /// Updates viewport dimensions when window is resized.
        /// </summary>
        /// <param name="width">New width in pixels</param>
        /// <param name="height">New height in pixels</param>
        void UpdateViewport(int width, int height);

        /// <summary>
        /// Cleans up rendering resources.
        /// </summary>
        void Dispose();

        /// <summary>
        /// Requests the next frame to be rendered.
        /// </summary>
        void RequestFrame();
    }
}