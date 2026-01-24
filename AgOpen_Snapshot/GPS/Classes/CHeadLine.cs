using System;
using System.Collections.Generic;
using System.Linq;
using AgOpenGPS.Core.Models.Base;
using AgOpenGPS.Core.Models.Guidance;

namespace AgOpenGPS
{
    /// <summary>
    /// WinForms wrapper for HeadlandLine from AgOpenGPS.Core
    /// Delegates all operations to Core HeadlandLine instance
    /// </summary>
    public class CHeadLine
    {
        private readonly HeadlandLine _core;
        private readonly List<CHeadPath> _trackWrappers;
        private readonly List<vec3> _desListWrapper;

        /// <summary>
        /// WinForms list that wraps Core Tracks
        /// </summary>
        public List<CHeadPath> tracksArr
        {
            get => _trackWrappers;
        }

        /// <summary>
        /// Current track index - delegates to Core
        /// </summary>
        public int idx
        {
            get => _core.CurrentIndex;
            set => _core.CurrentIndex = value;
        }

        /// <summary>
        /// Desired line points - delegates to Core with vec3 conversion
        /// </summary>
        public List<vec3> desList
        {
            get => _desListWrapper;
        }

        public CHeadLine()
        {
            _core = new HeadlandLine();
            _trackWrappers = new TracksListWrapper(_core.Tracks);
            _desListWrapper = new DesiredPointsListWrapper(_core.DesiredPoints);
        }

        /// <summary>
        /// Get the underlying Core HeadlandLine instance
        /// </summary>
        public HeadlandLine CoreHeadlandLine => _core;

        /// <summary>
        /// Wrapper list that keeps CHeadPath wrappers synchronized with Core Tracks
        /// </summary>
        private class TracksListWrapper : List<CHeadPath>
        {
            private readonly List<HeadlandPath> _coreTracks;

            public TracksListWrapper(List<HeadlandPath> coreTracks)
            {
                _coreTracks = coreTracks;
                SyncFromCore();
            }

            private void SyncFromCore()
            {
                Clear();
                base.AddRange(_coreTracks.Select(t => new CHeadPath(t)));
            }

            public new void Add(CHeadPath item)
            {
                _coreTracks.Add(item.CorePath);
                base.Add(item);
            }

            public new void AddRange(IEnumerable<CHeadPath> collection)
            {
                foreach (var item in collection)
                {
                    Add(item);
                }
            }

            public new void Clear()
            {
                _coreTracks.Clear();
                base.Clear();
            }

            public new void RemoveAt(int index)
            {
                _coreTracks.RemoveAt(index);
                base.RemoveAt(index);
            }

            public new bool Remove(CHeadPath item)
            {
                var coreItem = item.CorePath;
                var removed = _coreTracks.Remove(coreItem);
                if (removed) base.Remove(item);
                return removed;
            }
        }

        /// <summary>
        /// Wrapper list that keeps vec3 points synchronized with Core DesiredPoints
        /// </summary>
        private class DesiredPointsListWrapper : List<vec3>
        {
            private readonly List<Vec3> _corePoints;

            public DesiredPointsListWrapper(List<Vec3> corePoints)
            {
                _corePoints = corePoints;
                SyncFromCore();
            }

            private void SyncFromCore()
            {
                Clear();
                base.AddRange(_corePoints.Select(v => (vec3)v));
            }

            public new void Add(vec3 item)
            {
                _corePoints.Add((Vec3)item);
                base.Add(item);
            }

            public new void AddRange(IEnumerable<vec3> collection)
            {
                foreach (var item in collection)
                {
                    Add(item);
                }
            }

            public new void Clear()
            {
                _corePoints.Clear();
                base.Clear();
            }

            public new void RemoveAt(int index)
            {
                _corePoints.RemoveAt(index);
                base.RemoveAt(index);
            }

            public new bool Remove(vec3 item)
            {
                var coreItem = (Vec3)item;
                var removed = _corePoints.Remove(coreItem);
                if (removed) base.Remove(item);
                return removed;
            }

            public new vec3 this[int index]
            {
                get => base[index];
                set
                {
                    _corePoints[index] = (Vec3)value;
                    base[index] = value;
                }
            }
        }
    }

    /// <summary>
    /// WinForms wrapper for HeadlandPath from AgOpenGPS.Core
    /// Delegates all operations to Core HeadlandPath instance
    /// </summary>
    public class CHeadPath
    {
        private readonly HeadlandPath _corePath;
        private readonly List<vec3> _trackPtsWrapper;

        /// <summary>
        /// Track points - delegates to Core with vec3 conversion
        /// </summary>
        public List<vec3> trackPts
        {
            get => _trackPtsWrapper;
        }

        /// <summary>
        /// Name - delegates to Core
        /// </summary>
        public string name
        {
            get => _corePath.Name;
            set => _corePath.Name = value;
        }

        /// <summary>
        /// Move distance - delegates to Core
        /// </summary>
        public double moveDistance
        {
            get => _corePath.MoveDistance;
            set => _corePath.MoveDistance = value;
        }

        /// <summary>
        /// Mode - delegates to Core
        /// </summary>
        public int mode
        {
            get => _corePath.Mode;
            set => _corePath.Mode = value;
        }

        /// <summary>
        /// A-point index - delegates to Core
        /// </summary>
        public int a_point
        {
            get => _corePath.APointIndex;
            set => _corePath.APointIndex = value;
        }

        /// <summary>
        /// Create wrapper around new Core HeadlandPath
        /// </summary>
        public CHeadPath()
        {
            _corePath = new HeadlandPath();
            _trackPtsWrapper = new TrackPointsListWrapper(_corePath.TrackPoints);
        }

        /// <summary>
        /// Create wrapper around existing Core HeadlandPath
        /// </summary>
        internal CHeadPath(HeadlandPath corePath)
        {
            _corePath = corePath;
            _trackPtsWrapper = new TrackPointsListWrapper(_corePath.TrackPoints);
        }

        /// <summary>
        /// Get the underlying Core HeadlandPath instance
        /// </summary>
        internal HeadlandPath CorePath => _corePath;

        /// <summary>
        /// Wrapper list that keeps vec3 track points synchronized with Core TrackPoints
        /// </summary>
        private class TrackPointsListWrapper : List<vec3>
        {
            private readonly List<Vec3> _corePoints;

            public TrackPointsListWrapper(List<Vec3> corePoints)
            {
                _corePoints = corePoints;
                SyncFromCore();
            }

            private void SyncFromCore()
            {
                Clear();
                base.AddRange(_corePoints.Select(v => (vec3)v));
            }

            public new void Add(vec3 item)
            {
                _corePoints.Add((Vec3)item);
                base.Add(item);
            }

            public new void AddRange(IEnumerable<vec3> collection)
            {
                foreach (var item in collection)
                {
                    Add(item);
                }
            }

            public new void Clear()
            {
                _corePoints.Clear();
                base.Clear();
            }

            public new void RemoveAt(int index)
            {
                _corePoints.RemoveAt(index);
                base.RemoveAt(index);
            }

            public new bool Remove(vec3 item)
            {
                var coreItem = (Vec3)item;
                var removed = _corePoints.Remove(coreItem);
                if (removed) base.Remove(item);
                return removed;
            }

            public new vec3 this[int index]
            {
                get => base[index];
                set
                {
                    _corePoints[index] = (Vec3)value;
                    base[index] = value;
                }
            }
        }
    }
}