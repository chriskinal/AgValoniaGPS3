using AgOpenGPS.Core.Models;

namespace AgOpenGPS.Core.ViewModels
{
    // Dedicated ViewModel to display a distance in meters or feet
    public class MediumDistanceViewModel : DayNightAndUnitsViewModel
    {
        private readonly Distance _distance;
        public MediumDistanceViewModel(Distance distance)
        {
            _distance = distance;
        }

        public MediumDistanceViewModel(
            double distanceInMeters
        ) :
            this(new Distance(distanceInMeters))
        {
        }

        public double DistanceInMeters => _distance.InMeters;

        public double DistanceInFeet => _distance.InFeet;

        public double DisplayedDistance => IsMetric ? DistanceInMeters : DistanceInFeet;

    }
}
