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

﻿using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace AgValoniaGPS.Models
{
    public class WorkedArea
    {
        private readonly List<QuadStrip> _unsavedWork = new List<QuadStrip>();

        public WorkedArea()
        {
            Area = new GeoArea();
        }

        public ReadOnlyCollection<QuadStrip> UnsavedWork => _unsavedWork.AsReadOnly();

        public GeoArea Area { get; private set; }

        public void AddStrip(QuadStrip strip)
        {
            Area += strip.Area;
            _unsavedWork.Add(strip);
        }

        public void ResetUnsavedWork()
        {
            _unsavedWork.Clear();
        }
    }
}
