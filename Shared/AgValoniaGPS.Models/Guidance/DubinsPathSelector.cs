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

﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace AgValoniaGPS.Models
{
    public class DubinsPathSelector
    {
        private readonly DubinsPathConstraints _constraints;
        private List<DubinsPath> _paths = new List<DubinsPath>();

        public DubinsPathSelector(DubinsPathConstraints constraints)
        {
            _constraints = constraints;
            GeoCircle startRightCircle = DubinsPath.ComputeCircle(constraints.StartConstraint, constraints.RadiusConstraint, TurnType.Right);
            GeoCircle startLeftCircle = DubinsPath.ComputeCircle(constraints.StartConstraint, constraints.RadiusConstraint, TurnType.Left);
            GeoCircle goalRightCircle = DubinsPath.ComputeCircle(constraints.GoalConstraint, constraints.RadiusConstraint, TurnType.Right);
            GeoCircle goalLeftCircle = DubinsPath.ComputeCircle(constraints.GoalConstraint, constraints.RadiusConstraint, TurnType.Left);

            // 2 x OuterDubinsPath
            if (RsrDubinsPath.PathIsPossible(startRightCircle, goalRightCircle))
            {
                _paths.Add(new RsrDubinsPath(_constraints));
            }
            if (LslDubinsPath.PathIsPossible(startLeftCircle, goalLeftCircle))
            {
                _paths.Add(new LslDubinsPath(_constraints));
            }
            // 2 x CurvedDubinsPath
            if (LrlDubinsPath.PathIsPossible(startLeftCircle, goalLeftCircle))
            {
                _paths.Add(new LrlDubinsPath(_constraints));
            }
            if (RlrDubinsPath.PathIsPossible(startRightCircle, goalRightCircle))
            {
                _paths.Add(new RlrDubinsPath(_constraints));
            }
            // 2 x InnerDubinsPath
            if (RslDubinsPath.PathIsPossible(startRightCircle, goalLeftCircle))
            {
                _paths.Add(new RslDubinsPath(_constraints));
            }
            if (LsrDubinsPath.PathIsPossible(startLeftCircle, goalRightCircle))
            {
                _paths.Add(new LsrDubinsPath(_constraints));
            }
            _paths.Sort((x, y) => x.TotalLength.CompareTo(y.TotalLength));
        }

        public ReadOnlyCollection<DubinsPath> Paths => _paths.AsReadOnly();

    }

}
