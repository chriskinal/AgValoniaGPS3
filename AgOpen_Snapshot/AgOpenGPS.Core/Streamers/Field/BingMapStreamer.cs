using AgLibrary.Logging;
using AgOpenGPS.Core.Interfaces;
using AgOpenGPS.Core.Models;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace AgOpenGPS.Core.Streamers
{
    public class BingMapStreamer : FieldAspectStreamer
    {
        private readonly BingMapBitmapStreamer _bitmapStreamer;
        public BingMapStreamer(
            IFieldStreamerPresenter presenter
        ) :
            base("BackPic.txt", presenter)
        {
            _bitmapStreamer = new BingMapBitmapStreamer(presenter);
        }

        public BingMap TryRead(DirectoryInfo fieldDirectory)
        {
            BingMap bingMap = null;
            FileInfo fileInfo = GetFileInfo(fieldDirectory);
            if (fileInfo.Exists)
            {
                try
                {
                    bingMap = Read(fieldDirectory);
                }
                catch (Exception)
                {
                }
            }
            return bingMap;
        }

        public void TryWrite(BingMap bingMap, DirectoryInfo fieldDirectory)
        {
            try
            {
                Write(bingMap, fieldDirectory);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message + "\n Cannot write to file.");
                Log.EventWriter("Saving BingMap" + e.ToString());
            }
        }

        private BingMap Read(DirectoryInfo fieldDirectory)
        {
            BingMap bingMap = null;
            FileInfo fileInfo = GetFileInfo(fieldDirectory);
            using (GeoStreamReader reader = new GeoStreamReader(fileInfo))
            {
                string line = reader.ReadLine(); // skip header
                bool hasBingMap = reader.ReadBool();
                if (hasBingMap)
                {
                    GeoBoundingBox geoBb = reader.ReadGeoBoundingBox();
                    Bitmap bitmap = _bitmapStreamer.Read(fieldDirectory);
                    if (bitmap != null)
                    {
                        bingMap = new BingMap(geoBb, bitmap);
                    }
                }
            }
            return bingMap;
        }

        private void Write(BingMap bingMap, DirectoryInfo fieldDirectory)
        {
            if (bingMap != null)
            {
                FileInfo boundingBoxFileInfo = GetFileInfo(fieldDirectory);
                using (GeoStreamWriter writer = new GeoStreamWriter(boundingBoxFileInfo))
                {
                    writer.WriteLine("$BackPic");
                    writer.WriteBool(true);
                    writer.WriteGeoBoundingBox(bingMap.GeoBoundingBox);
                }
                _bitmapStreamer.Write(bingMap.Bitmap, fieldDirectory);
            }
            else
            {
                DeleteFile(fieldDirectory);
                _bitmapStreamer.DeleteFile(fieldDirectory);
            }
        }

        public void CreateFile(DirectoryInfo fieldDirectory)
        {
            fieldDirectory.Create();
            using (StreamWriter writer = new StreamWriter(GetFileInfo(fieldDirectory).Name))
            {
            }
        }

        private class BingMapBitmapStreamer : FieldAspectStreamer
        {
            public BingMapBitmapStreamer(
                IFieldStreamerPresenter presenter
            ) :
                base("BackPic.png", presenter)
            {
            }

            public Bitmap Read(DirectoryInfo fieldDirectory)
            {
                Bitmap bitmap = null;
                FileInfo fileInfo = GetFileInfo(fieldDirectory);
                if (fileInfo.Exists)
                {
                    bitmap = new Bitmap(Image.FromFile(fileInfo.FullName));
                }
                return bitmap;
            }

            public void Write(Bitmap bitmap, DirectoryInfo fieldDirectory)
            {
                FileInfo fileInfo = GetFileInfo(fieldDirectory);
                if (fileInfo.Exists)
                {
                    fileInfo.Delete();
                }
                bitmap?.Save(fileInfo.FullName, ImageFormat.Png);
            }
        }
    }
}
