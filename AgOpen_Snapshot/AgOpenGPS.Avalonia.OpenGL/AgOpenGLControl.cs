using Avalonia;
using Avalonia.Controls;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using Avalonia.Threading;
using System;

namespace AgOpenGPS.Avalonia.OpenGL
{
    /// <summary>
    /// Custom OpenGL control for AgOpenGPS that provides a rendering surface
    /// for field visualization, GPS guidance, and section control displays.
    /// </summary>
    public class AgOpenGLControl : OpenGlControlBase
    {
        private bool _isInitialized;

        /// <summary>
        /// Event raised when OpenGL context is initialized and ready for rendering setup.
        /// Use this to initialize shaders, buffers, and other OpenGL resources.
        /// </summary>
        public event EventHandler<GlInterfaceEventArgs>? GlInitialized;

        /// <summary>
        /// Event raised when the control should render a frame.
        /// All OpenGL drawing commands should be issued in response to this event.
        /// </summary>
        public event EventHandler<GlInterfaceEventArgs>? GlRender;

        /// <summary>
        /// Gets the OpenGL interface for making GL calls.
        /// Only available after initialization.
        /// </summary>
        public GlInterface? GlInterface { get; private set; }

        protected override void OnOpenGlInit(GlInterface gl)
        {
            base.OnOpenGlInit(gl);

            GlInterface = gl;
            _isInitialized = true;

            // Raise initialization event for subscribers to set up their OpenGL resources
            GlInitialized?.Invoke(this, new GlInterfaceEventArgs(gl));
        }

        protected override void OnOpenGlRender(GlInterface gl, int fb)
        {
            if (!_isInitialized)
                return;

            // Raise render event for subscribers to draw their content
            GlRender?.Invoke(this, new GlInterfaceEventArgs(gl));
        }

        protected override void OnOpenGlDeinit(GlInterface gl)
        {
            _isInitialized = false;
            GlInterface = null;

            base.OnOpenGlDeinit(gl);
        }

        /// <summary>
        /// Request a redraw of the OpenGL surface.
        /// Call this when your scene data changes and needs to be re-rendered.
        /// </summary>
        public void RequestRender()
        {
            if (_isInitialized)
            {
                Dispatcher.UIThread.Post(RequestNextFrameRendering, DispatcherPriority.Render);
            }
        }
    }

    /// <summary>
    /// Event arguments for OpenGL events, providing access to the GL interface.
    /// </summary>
    public class GlInterfaceEventArgs : EventArgs
    {
        public GlInterface GlInterface { get; }

        public GlInterfaceEventArgs(GlInterface glInterface)
        {
            GlInterface = glInterface;
        }
    }
}