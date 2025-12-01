using System.Collections.Generic;
using Avalonia;
using AgValoniaGPS.Models;
using AgValoniaGPS.Views.Controls;

namespace AgValoniaGPS.Desktop.Controls;

/// <summary>
/// Interface for map rendering controls - allows OpenGL, SkiaSharp, and DrawingContext implementations.
/// Extends ISharedMapControl from the shared Views project to enable cross-platform code sharing.
/// </summary>
public interface IMapControl : ISharedMapControl
{
    // All members are inherited from ISharedMapControl
    // This interface exists for backwards compatibility with existing Desktop code
}
