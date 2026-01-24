using Avalonia.OpenGL;
using System;
using static Avalonia.OpenGL.GlConsts;

namespace AgOpenGPS.Avalonia.OpenGL
{
    /// <summary>
    /// Simple test renderer to validate OpenGL integration.
    /// Renders a colored background with a simple pattern.
    /// </summary>
    public class TestRenderer
    {
        private float _hue = 0f;

        public void Initialize(GlInterface gl)
        {
            // OpenGL initialization - context is already set up by Avalonia
            // Future: Initialize shaders, VBOs, etc. here
        }

        public void Render(GlInterface gl, int width, int height)
        {
            // Animate the background color
            _hue += 0.001f;
            if (_hue >= 1.0f)
                _hue -= 1.0f;

            // Convert HSV to RGB for a nice color transition
            var rgb = HsvToRgb(_hue, 0.5f, 0.3f);

            // Clear the screen with animated color
            gl.ClearColor(rgb.r, rgb.g, rgb.b, 1.0f);
            gl.Clear(GL_COLOR_BUFFER_BIT | GL_DEPTH_BUFFER_BIT);

            // Set up viewport
            gl.Viewport(0, 0, width, height);

            // Enable depth testing
            gl.Enable(GL_DEPTH_TEST);

            // Check for OpenGL errors
            var error = gl.GetError();
            if (error != GL_NO_ERROR)
            {
                Console.WriteLine($"OpenGL Error: {error}");
            }
        }

        private (float r, float g, float b) HsvToRgb(float h, float s, float v)
        {
            int i = (int)(h * 6);
            float f = h * 6 - i;
            float p = v * (1 - s);
            float q = v * (1 - f * s);
            float t = v * (1 - (1 - f) * s);

            return (i % 6) switch
            {
                0 => (v, t, p),
                1 => (q, v, p),
                2 => (p, v, t),
                3 => (p, q, v),
                4 => (t, p, v),
                _ => (v, p, q)
            };
        }
    }
}