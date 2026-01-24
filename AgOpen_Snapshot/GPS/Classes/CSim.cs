using AgOpenGPS.Core.Interfaces.Services;
using AgOpenGPS.Core.Models;
using AgOpenGPS.Core.Services;
using System;

namespace AgOpenGPS
{
    /// <summary>
    /// WinForms wrapper for GPS simulation.
    /// Delegates all simulation logic to Core GpsSimulationService and updates FormGPS.
    /// </summary>
    public class CSim
    {
        private readonly FormGPS mf;
        private readonly IGpsSimulationService _coreSimulation;

        #region properties sim (delegated to Core)

        public Wgs84 CurrentLatLon
        {
            get => _coreSimulation.CurrentPosition;
            set => _coreSimulation.Initialize(value);
        }

        public double headingTrue
        {
            get => _coreSimulation.HeadingRadians;
            set => _coreSimulation.SetHeading(value);
        }

        public double stepDistance
        {
            get => _coreSimulation.StepDistance;
            set => _coreSimulation.StepDistance = value;
        }

        public double steerAngle
        {
            get => _coreSimulation.SteerAngle;
            set => _coreSimulation.SteerAngle = value;
        }

        public double steerangleAve => _coreSimulation.SteerAngleAverage;

        public double steerAngleScrollBar { get; set; }

        public bool isAccelForward
        {
            get => _coreSimulation.IsAcceleratingForward;
            set => _coreSimulation.IsAcceleratingForward = value;
        }

        public bool isAccelBack
        {
            get => _coreSimulation.IsAcceleratingBackward;
            set => _coreSimulation.IsAcceleratingBackward = value;
        }

        #endregion properties sim

        public CSim(FormGPS _f)
        {
            mf = _f;

            // Create Core simulation service (no LocalPlane dependency)
            _coreSimulation = new GpsSimulationService();

            // Initialize with settings
            var startPosition = new Wgs84(
                Properties.Settings.Default.setGPS_SimLatitude,
                Properties.Settings.Default.setGPS_SimLongitude);
            _coreSimulation.Initialize(startPosition);

            // Subscribe to simulation updates
            _coreSimulation.GpsDataUpdated += OnGpsDataUpdated;
        }

        /// <summary>
        /// Process simulation tick - delegates to Core service
        /// </summary>
        public void DoSimTick(double _st)
        {
            // Core service handles all simulation logic and raises event
            _coreSimulation.Tick(_st);
        }

        /// <summary>
        /// Handle GPS data updates from Core simulation service
        /// </summary>
        private void OnGpsDataUpdated(object sender, GpsSimulationEventArgs e)
        {
            var data = e.Data;

            // Update FormGPS with simulated data
            mf.mc.actualSteerAngleDegrees = data.SteerAngleDegrees;

            mf.pn.vtgSpeed = data.SpeedKmh;
            mf.pn.AverageTheSpeed();

            // Convert WGS84 to local coordinates using FormGPS's LocalPlane
            // (original code did this conversion here, not in CSim)
            GeoCoord fixCoord = mf.AppModel.LocalPlane.ConvertWgs84ToGeoCoord(data.Position);
            mf.pn.fix.northing = fixCoord.Northing;
            mf.pn.fix.easting = fixCoord.Easting;

            mf.pn.headingTrue = data.HeadingDegrees;
            mf.pn.headingTrueDual = data.HeadingDegrees;

            mf.ahrs.imuHeading = data.HeadingDegrees;
            if (mf.ahrs.imuHeading >= 360)
                mf.ahrs.imuHeading -= 360;

            mf.AppModel.CurrentLatLon = data.Position;

            mf.pn.hdop = data.Hdop;
            mf.pn.altitude = data.Altitude;
            mf.pn.satellitesTracked = data.SatellitesTracked;

            mf.sentenceCounter = 0;

            mf.UpdateFixPosition();
        }
    }
}
