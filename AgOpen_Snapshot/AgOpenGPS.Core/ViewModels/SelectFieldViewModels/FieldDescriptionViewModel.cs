using AgOpenGPS.Core.Models;
using System.IO;

namespace AgOpenGPS.Core.ViewModels
{
    public class FieldDescriptionViewModel : DayNightAndUnitsViewModel
    {
        public FieldDescriptionViewModel(
            FieldDescription fieldDescription,
            Wgs84 currentLatLon)
        {
            DirectoryInfo = fieldDescription.FieldDirectory;

            if (fieldDescription.Wgs84Start.HasValue)
            {
                double distance = currentLatLon.DistanceInKiloMeters(fieldDescription.Wgs84Start.Value);
                DistanceViewModel = new LongDistanceViewModel(distance);
                AddChild(DistanceViewModel);
            }
            if (fieldDescription.Area.HasValue)
            {
                AreaViewModel = new AreaViewModel(fieldDescription.Area.Value);
                AddChild(AreaViewModel);
            }
        }

        public DirectoryInfo DirectoryInfo { get; }
        public string FieldName => DirectoryInfo.Name;
        public LongDistanceViewModel DistanceViewModel { get; }
        public AreaViewModel AreaViewModel { get; }

    }

}
