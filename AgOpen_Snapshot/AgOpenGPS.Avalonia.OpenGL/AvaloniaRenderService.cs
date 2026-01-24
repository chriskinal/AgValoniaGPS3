using AgOpenGPS.Core.Interfaces.Services;
using Avalonia.OpenGL;
using System;

namespace AgOpenGPS.Avalonia.OpenGL
{
    /// <summary>
    /// Avalonia-specific implementation of the rendering service.
    /// Manages the OpenGL rendering pipeline for the Avalonia application.
    /// </summary>
    public class AvaloniaRenderService : IRenderService
    {
        private readonly AgOpenGLControl _glControl;
        private readonly TestRenderer _testRenderer;
        private bool _isInitialized;
        private int _width;
        private int _height;

        public AvaloniaRenderService(AgOpenGLControl glControl)
        {
            _glControl = glControl ?? throw new ArgumentNullException(nameof(glControl));
            _testRenderer = new TestRenderer();

            // Wire up events
            _glControl.GlInitialized += OnGlInitialized;
            _glControl.GlRender += OnGlRender;
        }

        public void Initialize()
        {
            // OpenGL initialization will happen when GlInitialized event fires
            // Nothing to do here for now
        }

        public void Render(double deltaTime)
        {
            // Rendering happens automatically via the OpenGL control's event system
            // This method is here for future use when we need manual render triggers
        }

        public void UpdateViewport(int width, int height)
        {
            _width = width;
            _height = height;
        }

        public void Dispose()
        {
            _glControl.GlInitialized -= OnGlInitialized;
            _glControl.GlRender -= OnGlRender;
        }

        public void RequestFrame()
        {
            _glControl.RequestRender();
        }

        private void OnGlInitialized(object? sender, GlInterfaceEventArgs e)
        {
            _testRenderer.Initialize(e.GlInterface);
            _isInitialized = true;
        }

        private void OnGlRender(object? sender, GlInterfaceEventArgs e)
        {
            if (!_isInitialized)
                return;

            var width = (int)_glControl.Bounds.Width;
            var height = (int)_glControl.Bounds.Height;

            if (width > 0 && height > 0)
            {
                _testRenderer.Render(e.GlInterface, width, height);
            }
        }
    }
}