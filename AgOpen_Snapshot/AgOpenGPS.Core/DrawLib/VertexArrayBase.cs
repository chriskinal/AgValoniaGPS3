using AgOpenGPS.Core.Models;
using OpenTK.Graphics.OpenGL;
using System;

namespace AgOpenGPS.Core.DrawLib
{
    public abstract class VertexArrayBase : IDisposable
    {
        private int _bufId;
        private bool isDisposed;

        public VertexArrayBase(int nDimensions)
        {
            _bufId = GL.GenBuffer();
            Bind();
            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(0, nDimensions, VertexAttribPointerType.Double, false, 0, 0);
        }

        public int Length { get; protected set; }

        public void Bind()
        {
            GL.BindBuffer(BufferTarget.ArrayBuffer, _bufId);
        }

        private void DeleteBuffer()
        {
            if (0 != _bufId)
            {
                GL.DeleteBuffer(_bufId);
            }
            _bufId = 0;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!isDisposed)
            {
                DeleteBuffer();
                isDisposed = true;
            }
        }

        ~VertexArrayBase()
        {
            Dispose(disposing: false);
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }

}
