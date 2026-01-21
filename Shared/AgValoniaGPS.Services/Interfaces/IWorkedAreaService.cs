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

using AgValoniaGPS.Models.Base;

namespace AgValoniaGPS.Services.Interfaces
{
    /// <summary>
    /// Service for worked area calculations.
    /// </summary>
    public interface IWorkedAreaService
    {
        /// <summary>
        /// Calculate the area of two triangles in a triangle strip.
        /// </summary>
        /// <param name="points">Array of points (must have at least startIndex + 4 elements)</param>
        /// <param name="startIndex">Starting index in the points array</param>
        /// <returns>Total area of the two triangles in square meters</returns>
        double CalculateTriangleStripArea(Vec3[] points, int startIndex);

        /// <summary>
        /// Calculate the area of a single triangle using three points.
        /// </summary>
        /// <param name="p1">First point</param>
        /// <param name="p2">Second point</param>
        /// <param name="p3">Third point</param>
        /// <returns>Area of the triangle in square meters</returns>
        double CalculateTriangleArea(Vec3 p1, Vec3 p2, Vec3 p3);
    }
}
