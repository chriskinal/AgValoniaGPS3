using System.Drawing;

namespace AgOpenGPS.Core.Models
{
    public class BingMap
    {
        public BingMap(
            GeoBoundingBox geoBoundingBox,
            Bitmap bitmap)
        {
            GeoBoundingBox = geoBoundingBox;
            Bitmap = bitmap;
        }

        public GeoBoundingBox GeoBoundingBox { get; }
        public Bitmap Bitmap { get; }

    }
}
