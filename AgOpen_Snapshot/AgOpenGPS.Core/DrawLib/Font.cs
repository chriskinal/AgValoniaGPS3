using AgOpenGPS.Core;
using OpenTK.Graphics.OpenGL;
using System;

namespace AgOpenGPS.Core.DrawLib
{
    public class Font
    {
        private readonly Camera _camera;
        private readonly Texture2D _fontTexture;

        public const int GlyphsPerLine = 16;
        public const int GlyphWidth = 16;
        public const int GlyphHeight = 32;
        public const int CharXSpacing = 16;

        public const int textureWidth = 256;

        public const int textureHeight = 256;

        public Font(Camera camera, Texture2D fontTexture)
        {
            _camera = camera;
            _fontTexture = fontTexture;
        }

        public void DrawText3D(
            double x1, double y1, string text, double camHeadingHint, double size = 1.0)
        {
            double x = 0, y = 0;

            GL.PushMatrix();

            GL.Translate(x1, y1, 0);

            if (_camera.PitchInDegrees < -45)
            {
                GL.Rotate(90, 1, 0, 0);
                if (_camera.FollowDirectionHint) GL.Rotate(-camHeadingHint, 0, 1, 0);
                size = -_camera.camSetDistance;
                size = Math.Pow(size, 0.8);
                size /= 800;
            }
            else
            {
                if (_camera.FollowDirectionHint) GL.Rotate(-camHeadingHint, 0, 0, 1);
                size = -_camera.camSetDistance;
                size = Math.Pow(size, 0.85);
                size /= 1000;
            }
            double yTop = y + GlyphHeight * size;
            double yBottom = y;
            DrawText(x, yBottom, yTop, text, size);

            GL.PopMatrix();
        }

        public void DrawText(double x, double y, string text, double size = 1.0)
        {
            double yBottom = y + GlyphHeight * size;
            double yTop = y;
            DrawText(x, yBottom, yTop, text, size);
        }

        private void DrawText(double x, double yBottom, double yTop, string text, double size)
        {
            _fontTexture.Bind();
            GL.Enable(EnableCap.Texture2D);
            GL.Begin(PrimitiveType.Quads);

            double u_step = GlyphWidth / (double)textureWidth;
            double v_step = GlyphHeight / (double)textureHeight;

            for (int n = 0; n < text.Length; n++)
            {
                char idx = text[n];
                double u = idx % GlyphsPerLine * u_step;
                double v = idx / GlyphsPerLine * v_step;

                GL.TexCoord2(u, v);
                GL.Vertex2(x, yTop);
                GL.TexCoord2(u + u_step, v);
                GL.Vertex2(x + GlyphWidth * size, yTop);
                GL.TexCoord2(u + u_step, v + v_step);
                GL.Vertex2(x + GlyphWidth * size, yBottom);
                GL.TexCoord2(u, v + v_step);
                GL.Vertex2(x, yBottom);

                x += CharXSpacing * size;
            }
            GL.End();
            GL.Disable(EnableCap.Texture2D);
        }
    }
}