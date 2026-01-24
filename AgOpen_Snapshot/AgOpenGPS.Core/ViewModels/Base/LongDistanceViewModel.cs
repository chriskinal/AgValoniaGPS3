using AgOpenGPS.Core.Models;

namespace AgOpenGPS.Core.ViewModels
{
    // Dedicated ViewModel to display a distance in kilometers or miles
    public class LongDistanceViewModel : DayNightAndUnitsViewModel
    {
        private readonly Distance _distance;
        public LongDistanceViewModel(Distance distance)
        {
            _distance = distance;
        }

        public LongDistanceViewModel(
            double distanceInMeters
        ) :
            this(new Distance(distanceInMeters))
        {
        }

        public double DistanceInMeters => _distance.InMeters;

        public double DistanceInKm => _distance.InKilometers;
        public double DistanceInMiles => _distance.InMiles;

        public double DisplayedDistance => IsMetric ? DistanceInKm : DistanceInMiles;

    }
}
