using OpenTK.Graphics.OpenGL;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using AgOpenGPS.Core.Models.Base;
using GMath = AgOpenGPS.Core.Models.Base.GeometryMath;

namespace AgOpenGPS
{
    /// <summary>
    /// WinForms wrapper for GeometryMath from AgOpenGPS.Core
    /// Pure math functions delegate to Core, OpenGL rendering stays here
    /// </summary>
    public static class glm
    {
        // NOTE: All math functions delegate to AgOpenGPS.Core.Models.Base.GeometryMath
        // This wrapper maintained for WinForms backward compatibility and OpenGL rendering

        #region Delegation to Core - Point and Range Tests

        public static bool InRangeBetweenAB(double start_x, double start_y, double end_x, double end_y,
            double point_x, double point_y)
        {
            return GMath.InRangeBetweenAB(start_x, start_y, end_x, end_y, point_x, point_y);
        }

        public static bool IsPointInPolygon(this List<vec3> polygon, vec3 testPoint)
        {
            // Convert to Core types and delegate
            var corePolygon = polygon.Select(p => (Vec3)p).ToList();
            return GMath.IsPointInPolygon(corePolygon, (Vec3)testPoint);
        }

        public static bool IsPointInPolygon(this List<vec3> polygon, vec2 testPoint)
        {
            var corePolygon = polygon.Select(p => (Vec3)p).ToList();
            return GMath.IsPointInPolygon(corePolygon, (Vec2)testPoint);
        }

        public static bool IsPointInPolygon(this List<vec2> polygon, vec2 testPoint)
        {
            var corePolygon = polygon.Select(p => (Vec2)p).ToList();
            return GMath.IsPointInPolygon(corePolygon, (Vec2)testPoint);
        }

        public static bool IsPointInPolygon(this List<vec2> polygon, vec3 testPoint)
        {
            var corePolygon = polygon.Select(p => (Vec2)p).ToList();
            return GMath.IsPointInPolygon(corePolygon, (Vec3)testPoint);
        }

        #endregion

        #region WinForms-Specific OpenGL Rendering (Not in Core)

        public static void DrawPolygon(this List<vec3> polygon)
        {
            if (polygon.Count > 2)
            {
                GL.Begin(PrimitiveType.LineStrip);
                for (int i = 0; i < polygon.Count; i++)
                {
                    GL.Vertex3(polygon[i].easting, polygon[i].northing, 0);
                }
                GL.End();
            }
        }

        public static void DrawPolygon(this List<vec2> polygon)
        {
            if (polygon.Count > 2)
            {
                GL.Begin(PrimitiveType.LineLoop);
                for (int i = 0; i < polygon.Count; i++)
                {
                    GL.Vertex3(polygon[i].easting, polygon[i].northing, 0);
                }
                GL.End();
            }
        }

        #endregion

        #region Delegation to Core - Catmull-Rom Spline

        public static vec3 Catmull(double t, vec3 p0, vec3 p1, vec3 p2, vec3 p3)
        {
            // Delegate to Core and convert result back
            var result = GMath.Catmull(t, (Vec3)p0, (Vec3)p1, (Vec3)p2, (Vec3)p3);
            return (vec3)result;
        }

        #endregion

        #region Constants - Delegated to Core

        // WinForms-specific regex
        public const string fileRegex = " /^(?!.{256,})(?!(aux|clock\\$|con|nul|prn|com[1-9]|lpt[1-9])(?:$|\\.))[^ ][ \\.\\w-$()+=[\\];#@~,&amp;']+[^\\. ]$/i";

        // All unit conversion constants delegate to Core
        public const double in2m = GMath.in2m;
        public const double m2in = GMath.m2in;
        public const double m2ft = GMath.m2ft;
        public const double ft2m = GMath.ft2m;
        public const double ha2ac = GMath.ha2ac;
        public const double ac2ha = GMath.ac2ha;
        public const double m2ac = GMath.m2ac;
        public const double m2ha = GMath.m2ha;
        public const double galAc2Lha = GMath.galAc2Lha;
        public const double LHa2galAc = GMath.LHa2galAc;
        public const double L2Gal = GMath.L2Gal;
        public const double Gal2L = GMath.Gal2L;
        public const double twoPI = GMath.twoPI;
        public const double PIBy2 = GMath.PIBy2;

        #endregion

        #region Delegation to Core - Angle Conversions

        public static double toDegrees(double radians)
        {
            return GMath.ToDegrees(radians);
        }

        public static double toRadians(double degrees)
        {
            return GMath.ToRadians(degrees);
        }

        public static double AngleDiff(double angle1, double angle2)
        {
            return GMath.AngleDiff(angle1, angle2);
        }

        #endregion

        #region Delegation to Core - Distance Calculations

        public static double Distance(double east1, double north1, double east2, double north2)
        {
            return GMath.Distance(east1, north1, east2, north2);
        }

        public static double Distance(vec2 first, vec2 second)
        {
            return GMath.Distance((Vec2)first, (Vec2)second);
        }

        public static double Distance(vec2 first, vec3 second)
        {
            return GMath.Distance((Vec2)first, (Vec3)second);
        }

        public static double Distance(vec3 first, vec2 second)
        {
            return GMath.Distance((Vec3)first, (Vec2)second);
        }

        public static double Distance(vec3 first, vec3 second)
        {
            return GMath.Distance((Vec3)first, (Vec3)second);
        }

        public static double Distance(vec2 first, double east, double north)
        {
            return GMath.Distance((Vec2)first, east, north);
        }

        public static double Distance(vec3 first, double east, double north)
        {
            return GMath.Distance((Vec3)first, east, north);
        }

        public static double Distance(vecFix2Fix first, vec2 second)
        {
            return GMath.Distance((VecFix2Fix)first, (Vec2)second);
        }

        public static double Distance(vecFix2Fix first, vecFix2Fix second)
        {
            return GMath.Distance((VecFix2Fix)first, (VecFix2Fix)second);
        }

        #endregion

        #region Delegation to Core - Distance Squared (Optimized)

        public static double DistanceSquared(double northing1, double easting1, double northing2, double easting2)
        {
            return GMath.DistanceSquared(northing1, easting1, northing2, easting2);
        }

        public static double DistanceSquared(vec3 first, vec2 second)
        {
            return GMath.DistanceSquared((Vec3)first, (Vec2)second);
        }

        public static double DistanceSquared(vec2 first, vec3 second)
        {
            return GMath.DistanceSquared((Vec2)first, (Vec3)second);
        }

        public static double DistanceSquared(vec3 first, vec3 second)
        {
            return GMath.DistanceSquared((Vec3)first, (Vec3)second);
        }

        public static double DistanceSquared(vec2 first, vec2 second)
        {
            return GMath.DistanceSquared((Vec2)first, (Vec2)second);
        }

        public static double DistanceSquared(vecFix2Fix first, vec2 second)
        {
            return GMath.DistanceSquared((VecFix2Fix)first, (Vec2)second);
        }

        #endregion

        #region WinForms-Specific Image Processing (Not in Core)

        public static Bitmap MakeGrayscale3(Bitmap original)
        {
            //create a blank bitmap the same size as original
            Bitmap newBitmap = new Bitmap(original.Width, original.Height);
            //get a graphics object from the new image
            Graphics g = Graphics.FromImage(newBitmap);
            //create the grayscale ColorMatrix
            ColorMatrix colorMatrix = new ColorMatrix(
               new float[][]
              {
                 new float[] {.3f, .3f, .3f, 0, 0},
                 new float[] {.59f, .59f, .59f, 0, 0},
                 new float[] {.11f, .11f, .11f, 0, 0},
                 new float[] {0, 0, 0, 1, 0},
                 new float[] {0, 0, 0, 0, 1}
              });
            //create some image attributes
            ImageAttributes attributes = new ImageAttributes();
            //set the color matrix attribute
            attributes.SetColorMatrix(colorMatrix);
            //draw the original image on the new image
            //using the grayscale color matrix
            g.DrawImage(original, new Rectangle(0, 0, original.Width, original.Height),
               0, 0, original.Width, original.Height, GraphicsUnit.Pixel, attributes);
            //dispose the Graphics object
            g.Dispose();
            return newBitmap;
        }

        #endregion

        #region Delegation to Core - Raycasting

        public static vec2? RaycastToPolygon(vec3 origin, List<vec3> polygon)
        {
            // Convert to Core types and delegate
            var corePolygon = polygon.Select(p => (Vec3)p).ToList();
            var result = GMath.RaycastToPolygon((Vec3)origin, corePolygon);

            // Convert result back to WinForms vec2
            return result.HasValue ? (vec2?)((vec2)result.Value) : null;
        }

        public static bool TryRaySegmentIntersection(vec2 rayOrigin, vec2 rayDir, vec2 segA, vec2 segB, out vec2 intersection)
        {
            // Delegate to Core
            bool result = GMath.TryRaySegmentIntersection(
                (Vec2)rayOrigin, (Vec2)rayDir, (Vec2)segA, (Vec2)segB, out Vec2 coreIntersection);

            // Convert result back to WinForms vec2
            intersection = result ? (vec2)coreIntersection : new vec2();
            return result;
        }

        #endregion
    }
}