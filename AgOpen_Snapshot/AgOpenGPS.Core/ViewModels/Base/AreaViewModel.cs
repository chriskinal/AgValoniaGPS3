using AgOpenGPS.Core.Models;

namespace AgOpenGPS.Core.ViewModels
{
    public class AreaViewModel : DayNightAndUnitsViewModel
    {
        private readonly Area _area;
        public AreaViewModel(Area area)
        {
            _area = area;
        }

        public AreaViewModel(double areaInSquareMeters) : this(new Area(areaInSquareMeters))
        {
        }

        public double AreaInSquareMeters => _area.InSquareMeters;
        public double AreaInHectares => _area.InHectares;
        public double AreaInAcres => _area.InAcres;

        public double DisplayedArea => IsMetric ? AreaInHectares : AreaInAcres;
    }
}
