using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using Avalonia.Threading;
using Silk.NET.OpenGL;
using StbImageSharp;
using AgValoniaGPS.Models;

namespace AgValoniaGPS.Desktop.Controls;

public class OpenGLMapControl : OpenGlControlBase, IMapControl
{
    // Avalonia property for grid visibility
    public static readonly StyledProperty<bool> IsGridVisibleProperty =
        AvaloniaProperty.Register<OpenGLMapControl, bool>(
            nameof(IsGridVisible),
            defaultValue: false,
            coerce: (control, value) =>
            {
                if (control is OpenGLMapControl mapControl)
                {
                    mapControl.RequestNextFrameRendering(); // Trigger re-render when value changes
                }
                return value;
            });

    public bool IsGridVisible
    {
        get => GetValue(IsGridVisibleProperty);
        set => SetValue(IsGridVisibleProperty, value);
    }

    private GL? _gl;
    private uint _gridVao;
    private uint _gridVbo;
    private uint _vehicleVao;
    private uint _vehicleVbo;
    private uint _boundaryVao;
    private uint _boundaryVbo;
    private uint _recordingVao;
    private uint _recordingVbo;
    private uint _recordingPointsVao;
    private uint _recordingPointsVbo;
    private uint _shaderProgram;
    private uint _textureShaderProgram;
    private uint _vehicleTexture;
    private int _gridVertexCount;
    private int _recordingLineVertexCount;
    private int _recordingPointVertexCount;
    private List<(int offset, int count)> _boundarySegments = new(); // Track separate boundary loops
    private Boundary? _pendingBoundary;
    private bool _hasPendingBoundaryUpdate;
    private List<(double Easting, double Northing)>? _pendingRecordingPoints;
    private bool _hasPendingRecordingUpdate;

    // Background satellite image
    private uint _backgroundTexture;
    private uint _backgroundVao;
    private uint _backgroundVbo;
    private bool _hasBackgroundImage;
    private double _backgroundMinX; // Easting min (west edge)
    private double _backgroundMaxX; // Easting max (east edge)
    private double _backgroundMinY; // Northing min (south edge)
    private double _backgroundMaxY; // Northing max (north edge)
    private string? _pendingBackgroundImagePath;
    private double _pendingBgMinX, _pendingBgMaxX, _pendingBgMinY, _pendingBgMaxY;
    private bool _hasPendingBackgroundUpdate;

    // Boundary offset indicator (shows where point will be dropped)
    private double _boundaryOffsetMeters = 0.0; // Offset in meters (perpendicular to heading)
    private bool _showBoundaryOffsetIndicator = false;

    // Runtime OpenGL ES detection (ANGLE on Windows uses OpenGL ES)
    private bool _isOpenGLES = false;

    // Camera/viewport properties
    private double _cameraX = 0.0;
    private double _cameraY = 0.0;
    private double _zoom = 1.0;
    private double _rotation = 0.0; // Radians (yaw)
    private double _cameraPitch = 0.0; // Radians (0 = top-down, positive = tilted up)
    private double _cameraDistance = 100.0; // Distance from target in 3D mode
    private bool _is3DMode = false;

    // GPS/Vehicle position
    private double _vehicleX = 0.0;      // Meters (world coordinates)
    private double _vehicleY = 0.0;      // Meters (world coordinates)
    private double _vehicleHeading = 0.0; // Radians

    // Mouse interaction state
    private bool _isPanning = false;
    private bool _isRotating = false;
    private Point _lastMousePosition;

    public OpenGLMapControl()
    {
        // Make control focusable and set to accept all pointer events
        Focusable = true;
        IsHitTestVisible = true;
        ClipToBounds = false;

        // Start render loop
        var timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16) // ~60 FPS
        };
        timer.Tick += (s, e) => RequestNextFrameRendering();
        timer.Start();

        // Wire up mouse events for camera control
        PointerPressed += OnPointerPressed;
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
        PointerWheelChanged += OnPointerWheelChanged;
    }

    protected override void OnOpenGlInit(GlInterface gl)
    {
        base.OnOpenGlInit(gl);

        // Initialize Silk.NET OpenGL context
        _gl = GL.GetApi(gl.GetProcAddress);

        string versionString = _gl.GetStringS(StringName.Version) ?? "";
        Console.WriteLine($"OpenGL Version: {versionString}");
        Console.WriteLine($"OpenGL Vendor: {_gl.GetStringS(StringName.Vendor)}");
        Console.WriteLine($"OpenGL Renderer: {_gl.GetStringS(StringName.Renderer)}");

        // Detect OpenGL ES at runtime (ANGLE on Windows reports "OpenGL ES 3.0" in version string)
        _isOpenGLES = versionString.Contains("OpenGL ES", StringComparison.OrdinalIgnoreCase)
                      || OperatingSystem.IsAndroid()
                      || OperatingSystem.IsIOS();
        Console.WriteLine($"Using OpenGL ES shaders: {_isOpenGLES}");

        // Set clear color (dark background for grid visibility)
        _gl.ClearColor(0.1f, 0.1f, 0.1f, 1.0f);

        // Enable blending for transparency
        _gl.Enable(EnableCap.Blend);
        _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

        // Initialize basic rendering resources
        try
        {
            InitializeShaders();
            InitializeGrid();
            InitializeVehicle();
            InitializeTextureShaders();
            LoadVehicleTexture();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR during initialization: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }

    private string GetColorVertexShaderSource()
    {
        if (_isOpenGLES)
        {
            // OpenGL ES 3.0 for Android/iOS
            return @"#version 300 es
precision mediump float;
layout (location = 0) in vec2 aPosition;
layout (location = 1) in vec4 aColor;

uniform mat4 uMVP;

out vec4 vColor;

void main()
{
    gl_Position = uMVP * vec4(aPosition, 0.0, 1.0);
    vColor = aColor;
}";
        }
        else
        {
            // OpenGL 3.3 for Windows/macOS/Linux
            return @"#version 330
layout (location = 0) in vec2 aPosition;
layout (location = 1) in vec4 aColor;

uniform mat4 uMVP;

out vec4 vColor;

void main()
{
    gl_Position = uMVP * vec4(aPosition, 0.0, 1.0);
    vColor = aColor;
}";
        }
    }

    private string GetColorFragmentShaderSource()
    {
        if (_isOpenGLES)
        {
            // OpenGL ES 3.0 for Android/iOS
            return @"#version 300 es
precision mediump float;
in vec4 vColor;
out vec4 FragColor;

void main()
{
    FragColor = vColor;
}";
        }
        else
        {
            // OpenGL 3.3 for Windows/macOS/Linux
            return @"#version 330
in vec4 vColor;
out vec4 FragColor;

void main()
{
    FragColor = vColor;
}";
        }
    }

    private void InitializeShaders()
    {
        if (_gl == null) return;

        string vertexShaderSource = GetColorVertexShaderSource();
        string fragmentShaderSource = GetColorFragmentShaderSource();

        // Compile vertex shader
        uint vertexShader = _gl.CreateShader(ShaderType.VertexShader);
        _gl.ShaderSource(vertexShader, vertexShaderSource);
        _gl.CompileShader(vertexShader);

        _gl.GetShader(vertexShader, ShaderParameterName.CompileStatus, out int vStatus);
        if (vStatus != (int)GLEnum.True)
        {
            string log = _gl.GetShaderInfoLog(vertexShader);
            throw new Exception($"Vertex shader compilation failed: {log}");
        }

        // Compile fragment shader
        uint fragmentShader = _gl.CreateShader(ShaderType.FragmentShader);
        _gl.ShaderSource(fragmentShader, fragmentShaderSource);
        _gl.CompileShader(fragmentShader);

        _gl.GetShader(fragmentShader, ShaderParameterName.CompileStatus, out int fStatus);
        if (fStatus != (int)GLEnum.True)
        {
            string log = _gl.GetShaderInfoLog(fragmentShader);
            throw new Exception($"Fragment shader compilation failed: {log}");
        }

        // Link shader program
        _shaderProgram = _gl.CreateProgram();
        _gl.AttachShader(_shaderProgram, vertexShader);
        _gl.AttachShader(_shaderProgram, fragmentShader);
        _gl.LinkProgram(_shaderProgram);

        _gl.GetProgram(_shaderProgram, ProgramPropertyARB.LinkStatus, out int pStatus);
        if (pStatus != (int)GLEnum.True)
        {
            string log = _gl.GetProgramInfoLog(_shaderProgram);
            throw new Exception($"Shader program linking failed: {log}");
        }

        // Clean up shaders (no longer needed after linking)
        _gl.DeleteShader(vertexShader);
        _gl.DeleteShader(fragmentShader);
    }

    private string GetTextureVertexShaderSource()
    {
        if (_isOpenGLES)
        {
            // OpenGL ES 3.0 for Android/iOS
            return @"#version 300 es
precision mediump float;
layout (location = 0) in vec2 aPosition;
layout (location = 1) in vec2 aTexCoord;

out vec2 TexCoord;

uniform mat4 uTransform;

void main()
{
    gl_Position = uTransform * vec4(aPosition, 0.0, 1.0);
    TexCoord = aTexCoord;
}";
        }
        else
        {
            // OpenGL 3.3 for Windows/macOS/Linux
            return @"#version 330
layout (location = 0) in vec2 aPosition;
layout (location = 1) in vec2 aTexCoord;

out vec2 TexCoord;

uniform mat4 uTransform;

void main()
{
    gl_Position = uTransform * vec4(aPosition, 0.0, 1.0);
    TexCoord = aTexCoord;
}";
        }
    }

    private string GetTextureFragmentShaderSource()
    {
        if (_isOpenGLES)
        {
            // OpenGL ES 3.0 for Android/iOS
            return @"#version 300 es
precision mediump float;
in vec2 TexCoord;
out vec4 FragColor;

uniform sampler2D uTexture;

void main()
{
    FragColor = texture(uTexture, TexCoord);
}";
        }
        else
        {
            // OpenGL 3.3 for Windows/macOS/Linux
            return @"#version 330
in vec2 TexCoord;
out vec4 FragColor;

uniform sampler2D uTexture;

void main()
{
    FragColor = texture(uTexture, TexCoord);
}";
        }
    }

    private void InitializeTextureShaders()
    {
        if (_gl == null) return;

        string textureVertexShaderSource = GetTextureVertexShaderSource();
        string textureFragmentShaderSource = GetTextureFragmentShaderSource();

        uint vertexShader = _gl.CreateShader(ShaderType.VertexShader);
        _gl.ShaderSource(vertexShader, textureVertexShaderSource);
        _gl.CompileShader(vertexShader);

        _gl.GetShader(vertexShader, ShaderParameterName.CompileStatus, out int vStatus);
        if (vStatus != (int)GLEnum.True)
        {
            string log = _gl.GetShaderInfoLog(vertexShader);
            throw new Exception($"Texture vertex shader compilation failed: {log}");
        }

        uint fragmentShader = _gl.CreateShader(ShaderType.FragmentShader);
        _gl.ShaderSource(fragmentShader, textureFragmentShaderSource);
        _gl.CompileShader(fragmentShader);

        _gl.GetShader(fragmentShader, ShaderParameterName.CompileStatus, out int fStatus);
        if (fStatus != (int)GLEnum.True)
        {
            string log = _gl.GetShaderInfoLog(fragmentShader);
            throw new Exception($"Texture fragment shader compilation failed: {log}");
        }

        _textureShaderProgram = _gl.CreateProgram();
        _gl.AttachShader(_textureShaderProgram, vertexShader);
        _gl.AttachShader(_textureShaderProgram, fragmentShader);
        _gl.LinkProgram(_textureShaderProgram);

        _gl.GetProgram(_textureShaderProgram, ProgramPropertyARB.LinkStatus, out int pStatus);
        if (pStatus != (int)GLEnum.True)
        {
            string log = _gl.GetProgramInfoLog(_textureShaderProgram);
            throw new Exception($"Texture shader program linking failed: {log}");
        }

        _gl.DeleteShader(vertexShader);
        _gl.DeleteShader(fragmentShader);
    }

    private void LoadVehicleTexture()
    {
        if (_gl == null) return;

        string texturePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Images", "TractorAoG.png");

        if (!File.Exists(texturePath))
        {
            throw new FileNotFoundException($"Texture file not found: {texturePath}");
        }

        // Load image using StbImageSharp
        StbImage.stbi_set_flip_vertically_on_load(1);
        using var stream = File.OpenRead(texturePath);
        ImageResult image = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);

        // Create OpenGL texture
        _vehicleTexture = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, _vehicleTexture);

        unsafe
        {
            fixed (byte* ptr = image.Data)
            {
                _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba,
                    (uint)image.Width, (uint)image.Height, 0,
                    PixelFormat.Rgba, PixelType.UnsignedByte, ptr);
            }
        }

        // Set texture parameters
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);

        _gl.BindTexture(TextureTarget.Texture2D, 0);
    }

    private void InitializeGrid()
    {
        if (_gl == null) return;

        // Create grid lines (10m spacing, 100m x 100m grid)
        var gridVertices = new System.Collections.Generic.List<float>();
        float gridSize = 500.0f; // 500m x 500m grid
        float spacing = 10.0f;   // 10m spacing
        float alpha = 0.3f;      // Semi-transparent

        // Vertical lines
        for (float x = -gridSize; x <= gridSize; x += spacing)
        {
            // Brighter lines every 50m
            float lineAlpha = (Math.Abs(x % 50.0f) < 0.1f) ? 0.5f : alpha;

            gridVertices.Add(x); gridVertices.Add(-gridSize); // Position
            gridVertices.Add(0.3f); gridVertices.Add(0.3f); gridVertices.Add(0.3f); gridVertices.Add(lineAlpha); // Gray color

            gridVertices.Add(x); gridVertices.Add(gridSize);
            gridVertices.Add(0.3f); gridVertices.Add(0.3f); gridVertices.Add(0.3f); gridVertices.Add(lineAlpha);
        }

        // Horizontal lines
        for (float y = -gridSize; y <= gridSize; y += spacing)
        {
            float lineAlpha = (Math.Abs(y % 50.0f) < 0.1f) ? 0.5f : alpha;

            gridVertices.Add(-gridSize); gridVertices.Add(y);
            gridVertices.Add(0.3f); gridVertices.Add(0.3f); gridVertices.Add(0.3f); gridVertices.Add(lineAlpha);

            gridVertices.Add(gridSize); gridVertices.Add(y);
            gridVertices.Add(0.3f); gridVertices.Add(0.3f); gridVertices.Add(0.3f); gridVertices.Add(lineAlpha);
        }

        // Axis lines (brighter)
        // X-axis (red)
        gridVertices.Add(-gridSize); gridVertices.Add(0.0f);
        gridVertices.Add(0.8f); gridVertices.Add(0.2f); gridVertices.Add(0.2f); gridVertices.Add(0.8f);
        gridVertices.Add(gridSize); gridVertices.Add(0.0f);
        gridVertices.Add(0.8f); gridVertices.Add(0.2f); gridVertices.Add(0.2f); gridVertices.Add(0.8f);

        // Y-axis (green)
        gridVertices.Add(0.0f); gridVertices.Add(-gridSize);
        gridVertices.Add(0.2f); gridVertices.Add(0.8f); gridVertices.Add(0.2f); gridVertices.Add(0.8f);
        gridVertices.Add(0.0f); gridVertices.Add(gridSize);
        gridVertices.Add(0.2f); gridVertices.Add(0.8f); gridVertices.Add(0.2f); gridVertices.Add(0.8f);

        _gridVertexCount = gridVertices.Count / 6; // 6 floats per vertex (x, y, r, g, b, a)

        // Create grid VAO/VBO
        _gridVao = _gl.GenVertexArray();
        _gl.BindVertexArray(_gridVao);

        _gridVbo = _gl.GenBuffer();
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _gridVbo);

        float[] gridArray = gridVertices.ToArray();
        unsafe
        {
            fixed (float* v = gridArray)
            {
                _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(gridArray.Length * sizeof(float)), v, BufferUsageARB.StaticDraw);
            }
        }

        // Position attribute
        unsafe
        {
            _gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 6 * sizeof(float), (void*)0);
        }
        _gl.EnableVertexAttribArray(0);

        // Color attribute
        unsafe
        {
            _gl.VertexAttribPointer(1, 4, VertexAttribPointerType.Float, false, 6 * sizeof(float), (void*)(2 * sizeof(float)));
        }
        _gl.EnableVertexAttribArray(1);

        _gl.BindVertexArray(0);
    }

    private void InitializeVehicle()
    {
        if (_gl == null) return;

        // Create a textured quad to represent the vehicle
        // Scale: about 5 meters (typical tractor size)
        float size = 5.0f;

        float[] vehicleVertices = new float[]
        {
            // Position (x, y), TexCoord (u, v)
            // Quad for tractor image
            -size * 0.5f, -size * 0.5f,  0.0f, 0.0f,  // Bottom-left
             size * 0.5f, -size * 0.5f,  1.0f, 0.0f,  // Bottom-right
             size * 0.5f,  size * 0.5f,  1.0f, 1.0f,  // Top-right
            -size * 0.5f,  size * 0.5f,  0.0f, 1.0f   // Top-left
        };

        // Create VAO and VBO
        _vehicleVao = _gl.GenVertexArray();
        _gl.BindVertexArray(_vehicleVao);

        _vehicleVbo = _gl.GenBuffer();
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vehicleVbo);

        unsafe
        {
            fixed (float* v = vehicleVertices)
            {
                _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(vehicleVertices.Length * sizeof(float)), v, BufferUsageARB.StaticDraw);
            }
        }

        // Position attribute
        unsafe
        {
            _gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), (void*)0);
        }
        _gl.EnableVertexAttribArray(0);

        // Texture coordinate attribute
        unsafe
        {
            _gl.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), (void*)(2 * sizeof(float)));
        }
        _gl.EnableVertexAttribArray(1);

        _gl.BindVertexArray(0);
    }

    protected override void OnOpenGlRender(GlInterface gl, int fb)
    {
        if (_gl == null) return;

        // Process pending boundary on render thread
        if (_hasPendingBoundaryUpdate)
        {
            _hasPendingBoundaryUpdate = false;
            if (_pendingBoundary != null)
            {
                InitializeBoundary(_pendingBoundary);
            }
            else
            {
                ClearBoundary();
            }
            _pendingBoundary = null;
        }

        // Process pending recording points update on render thread
        if (_hasPendingRecordingUpdate)
        {
            _hasPendingRecordingUpdate = false;
            if (_pendingRecordingPoints != null && _pendingRecordingPoints.Count > 0)
            {
                UpdateRecordingBuffers(_pendingRecordingPoints);
            }
            else
            {
                ClearRecordingBuffers();
            }
            _pendingRecordingPoints = null;
        }

        // Process pending background image update on render thread
        if (_hasPendingBackgroundUpdate)
        {
            _hasPendingBackgroundUpdate = false;
            if (!string.IsNullOrEmpty(_pendingBackgroundImagePath))
            {
                LoadBackgroundTexture(_pendingBackgroundImagePath, _pendingBgMinX, _pendingBgMaxX, _pendingBgMinY, _pendingBgMaxY);
            }
            else
            {
                ClearBackgroundImage();
            }
            _pendingBackgroundImagePath = null;
        }

        // Set viewport
        _gl.Viewport(0, 0, (uint)Bounds.Width, (uint)Bounds.Height);

        // Clear the screen and depth buffer
        _gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

        // Enable depth testing for 3D mode
        if (_is3DMode)
        {
            _gl.Enable(EnableCap.DepthTest);
        }
        else
        {
            _gl.Disable(EnableCap.DepthTest);
        }

        // Use shader program
        _gl.UseProgram(_shaderProgram);

        float aspect = (float)Bounds.Width / (float)Bounds.Height;
        float[] view;
        float[] projection;
        float[] mvp;

        if (_is3DMode)
        {
            // 3D perspective mode
            // Camera follows vehicle from behind and above
            float targetX = (float)_vehicleX;
            float targetY = (float)_vehicleY;
            float targetZ = 0.0f;

            // Calculate camera position based on distance, pitch, and vehicle heading
            float camX = targetX - (float)(_cameraDistance * Math.Cos(_cameraPitch) * Math.Sin(_vehicleHeading));
            float camY = targetY - (float)(_cameraDistance * Math.Cos(_cameraPitch) * Math.Cos(_vehicleHeading));
            float camZ = (float)(_cameraDistance * Math.Sin(_cameraPitch));

            view = CreateLookAtMatrix(camX, camY, camZ, targetX, targetY, targetZ);
            projection = CreatePerspectiveMatrix(45.0f * (float)Math.PI / 180.0f, aspect, 1.0f, 10000.0f);
        }
        else
        {
            // 2D orthographic mode
            float viewWidth = 200.0f * aspect / (float)_zoom;
            float viewHeight = 200.0f / (float)_zoom;

            view = CreateViewMatrix((float)_cameraX, (float)_cameraY, (float)_rotation);
            projection = CreateOrthographicMatrix(
                -viewWidth / 2,
                viewWidth / 2,
                -viewHeight / 2,
                viewHeight / 2
            );
        }

        // Combine projection * view
        mvp = MultiplyMatrices(projection, view);

        // Set MVP uniform
        int mvpLocation = _gl.GetUniformLocation(_shaderProgram, "uMVP");
        unsafe
        {
            fixed (float* m = mvp)
            {
                _gl.UniformMatrix4(_gl.GetUniformLocation(_shaderProgram, "uMVP"), 1, false, m);
            }
        }

        // Draw background satellite image first (behind everything)
        if (_hasBackgroundImage && _backgroundTexture != 0)
        {
            _gl.UseProgram(_textureShaderProgram);
            _gl.BindTexture(TextureTarget.Texture2D, _backgroundTexture);
            _gl.BindVertexArray(_backgroundVao);

            int bgTransformLoc = _gl.GetUniformLocation(_textureShaderProgram, "uTransform");
            unsafe
            {
                fixed (float* m = mvp)
                {
                    _gl.UniformMatrix4(bgTransformLoc, 1, false, m);
                }
            }

            _gl.DrawArrays(PrimitiveType.TriangleFan, 0, 4);
            _gl.BindVertexArray(0);
            _gl.BindTexture(TextureTarget.Texture2D, 0);
        }

        // Use color shader for grid and other elements
        _gl.UseProgram(_shaderProgram);
        unsafe
        {
            fixed (float* m = mvp)
            {
                _gl.UniformMatrix4(_gl.GetUniformLocation(_shaderProgram, "uMVP"), 1, false, m);
            }
        }

        // Draw grid (if visible)
        if (IsGridVisible)
        {
            _gl.BindVertexArray(_gridVao);
            _gl.DrawArrays(PrimitiveType.Lines, 0, (uint)_gridVertexCount);
            _gl.BindVertexArray(0);
        }

        // Draw boundary (if loaded)
        if (_boundaryVao != 0 && _boundarySegments.Count > 0)
        {
            _gl.BindVertexArray(_boundaryVao);
            _gl.LineWidth(5.0f); // Thicker lines for better visibility

            // Draw each boundary segment separately (outer and inner boundaries)
            foreach (var (offset, count) in _boundarySegments)
            {
                _gl.DrawArrays(GLEnum.LineLoop, offset, (uint)count);
            }

            _gl.BindVertexArray(0);
            _gl.LineWidth(1.0f);
        }

        // Draw recording boundary lines (cyan color, LINE_STRIP)
        if (_recordingVao != 0 && _recordingLineVertexCount > 1)
        {
            _gl.BindVertexArray(_recordingVao);
            _gl.LineWidth(3.0f);
            _gl.DrawArrays(GLEnum.LineStrip, 0, (uint)_recordingLineVertexCount);
            _gl.BindVertexArray(0);
            _gl.LineWidth(1.0f);
        }

        // Draw recording boundary points (red markers)
        if (_recordingPointsVao != 0 && _recordingPointVertexCount > 0)
        {
            _gl.BindVertexArray(_recordingPointsVao);
            _gl.PointSize(4.0f); // Small markers
            _gl.DrawArrays(GLEnum.Points, 0, (uint)_recordingPointVertexCount);
            _gl.BindVertexArray(0);
            _gl.PointSize(1.0f);
        }

        // Draw vehicle with position and heading
        float[] vehicleModel;
        if (_is3DMode)
        {
            // In 3D mode, create a billboard that faces the camera
            vehicleModel = CreateBillboardMatrix((float)_vehicleX, (float)_vehicleY, 2.0f, view);
        }
        else
        {
            // In 2D mode, rotate vehicle based on heading
            // Negate heading because AgOpenGPS uses compass convention (clockwise, 0=North)
            // while OpenGL rotation is counterclockwise (math convention)
            vehicleModel = CreateModelMatrix((float)_vehicleX, (float)_vehicleY, -(float)_vehicleHeading);
        }
        float[] vehicleMvp = MultiplyMatrices(mvp, vehicleModel);

        unsafe
        {
            fixed (float* m = vehicleMvp)
            {
                _gl.UniformMatrix4(_gl.GetUniformLocation(_shaderProgram, "uMVP"), 1, false, m);
            }
        }

        // Draw vehicle (textured quad)
        _gl.UseProgram(_textureShaderProgram);
        _gl.BindTexture(TextureTarget.Texture2D, _vehicleTexture);
        _gl.BindVertexArray(_vehicleVao);

        int textureTransformLoc = _gl.GetUniformLocation(_textureShaderProgram, "uTransform");
        unsafe
        {
            fixed (float* m = vehicleMvp)
            {
                _gl.UniformMatrix4(textureTransformLoc, 1, false, m);
            }
        }

        _gl.DrawArrays(PrimitiveType.TriangleFan, 0, 4);
        _gl.BindVertexArray(0);

        // Draw boundary offset indicator on top of vehicle (reference point and arrow showing offset direction)
        if (_showBoundaryOffsetIndicator)
        {
            DrawBoundaryOffsetIndicator(mvp);
        }
    }

    private float[] CreateOrthographicMatrix(float left, float right, float bottom, float top)
    {
        // Simple orthographic projection matrix
        float[] matrix = new float[16];
        matrix[0] = 2.0f / (right - left);
        matrix[5] = 2.0f / (top - bottom);
        matrix[10] = -1.0f;
        matrix[12] = -(right + left) / (right - left);
        matrix[13] = -(top + bottom) / (top - bottom);
        matrix[15] = 1.0f;
        return matrix;
    }

    private float[] CreateViewMatrix(float x, float y, float rotation)
    {
        // Create view matrix with translation and rotation
        // View matrix = inverse of camera transform
        float cos = (float)Math.Cos(-rotation);
        float sin = (float)Math.Sin(-rotation);

        float[] matrix = new float[16];
        // Rotation around Z axis
        matrix[0] = cos;
        matrix[1] = sin;
        matrix[4] = -sin;
        matrix[5] = cos;
        matrix[10] = 1.0f;
        matrix[15] = 1.0f;

        // Translation (camera position negated for view matrix)
        matrix[12] = -x * cos - y * (-sin);
        matrix[13] = -x * sin + y * cos;

        return matrix;
    }

    private float[] CreateModelMatrix(float x, float y, float rotation)
    {
        // Create model matrix for an object (translation + rotation)
        float cos = (float)Math.Cos(rotation);
        float sin = (float)Math.Sin(rotation);

        float[] matrix = new float[16];
        // Rotation around Z axis
        matrix[0] = cos;
        matrix[1] = sin;
        matrix[4] = -sin;
        matrix[5] = cos;
        matrix[10] = 1.0f;
        matrix[15] = 1.0f;

        // Translation
        matrix[12] = x;
        matrix[13] = y;

        return matrix;
    }

    private float[] CreateBillboardMatrix(float x, float y, float z, float[] viewMatrix)
    {
        // Create a billboard matrix (sprite that always faces camera)
        // Extract right, up vectors from inverse view matrix
        float[] matrix = new float[16];

        // Copy the inverse rotation from view matrix (transpose of rotation part)
        matrix[0] = viewMatrix[0];
        matrix[1] = viewMatrix[4];
        matrix[2] = viewMatrix[8];
        matrix[3] = 0;

        matrix[4] = viewMatrix[1];
        matrix[5] = viewMatrix[5];
        matrix[6] = viewMatrix[9];
        matrix[7] = 0;

        matrix[8] = viewMatrix[2];
        matrix[9] = viewMatrix[6];
        matrix[10] = viewMatrix[10];
        matrix[11] = 0;

        // Position
        matrix[12] = x;
        matrix[13] = y;
        matrix[14] = z;
        matrix[15] = 1.0f;

        return matrix;
    }

    private float[] CreatePerspectiveMatrix(float fovY, float aspect, float near, float far)
    {
        // Perspective projection matrix
        float f = 1.0f / (float)Math.Tan(fovY / 2.0f);
        float[] matrix = new float[16];

        matrix[0] = f / aspect;
        matrix[5] = f;
        matrix[10] = (far + near) / (near - far);
        matrix[11] = -1.0f;
        matrix[14] = (2.0f * far * near) / (near - far);

        return matrix;
    }

    private float[] CreateLookAtMatrix(float eyeX, float eyeY, float eyeZ, float targetX, float targetY, float targetZ)
    {
        // Look-at matrix (view matrix)
        float upX = 0.0f, upY = 0.0f, upZ = 1.0f;

        // Forward vector (from eye to target)
        float fX = targetX - eyeX;
        float fY = targetY - eyeY;
        float fZ = targetZ - eyeZ;
        float fLen = (float)Math.Sqrt(fX * fX + fY * fY + fZ * fZ);
        fX /= fLen; fY /= fLen; fZ /= fLen;

        // Right vector (cross product of forward and up)
        float rX = fY * upZ - fZ * upY;
        float rY = fZ * upX - fX * upZ;
        float rZ = fX * upY - fY * upX;
        float rLen = (float)Math.Sqrt(rX * rX + rY * rY + rZ * rZ);
        rX /= rLen; rY /= rLen; rZ /= rLen;

        // Up vector (cross product of right and forward)
        float uX = rY * fZ - rZ * fY;
        float uY = rZ * fX - rX * fZ;
        float uZ = rX * fY - rY * fX;

        float[] matrix = new float[16];
        matrix[0] = rX;
        matrix[4] = rY;
        matrix[8] = rZ;
        matrix[12] = -(rX * eyeX + rY * eyeY + rZ * eyeZ);

        matrix[1] = uX;
        matrix[5] = uY;
        matrix[9] = uZ;
        matrix[13] = -(uX * eyeX + uY * eyeY + uZ * eyeZ);

        matrix[2] = -fX;
        matrix[6] = -fY;
        matrix[10] = -fZ;
        matrix[14] = (fX * eyeX + fY * eyeY + fZ * eyeZ);

        matrix[15] = 1.0f;

        return matrix;
    }

    private float[] MultiplyMatrices(float[] a, float[] b)
    {
        // Multiply two 4x4 matrices (column-major order)
        float[] result = new float[16];

        for (int row = 0; row < 4; row++)
        {
            for (int col = 0; col < 4; col++)
            {
                result[col * 4 + row] =
                    a[0 * 4 + row] * b[col * 4 + 0] +
                    a[1 * 4 + row] * b[col * 4 + 1] +
                    a[2 * 4 + row] * b[col * 4 + 2] +
                    a[3 * 4 + row] * b[col * 4 + 3];
            }
        }

        return result;
    }

    protected override void OnOpenGlDeinit(GlInterface gl)
    {
        if (_gl != null)
        {
            if (_vehicleTexture != 0)
            {
                _gl.DeleteTexture(_vehicleTexture);
            }
            if (_backgroundTexture != 0)
            {
                _gl.DeleteTexture(_backgroundTexture);
            }
            if (_backgroundVao != 0)
            {
                _gl.DeleteVertexArray(_backgroundVao);
                _gl.DeleteBuffer(_backgroundVbo);
            }
            if (_textureShaderProgram != 0)
            {
                _gl.DeleteProgram(_textureShaderProgram);
            }
            _gl.DeleteBuffer(_gridVbo);
            _gl.DeleteVertexArray(_gridVao);
            _gl.DeleteBuffer(_vehicleVbo);
            _gl.DeleteVertexArray(_vehicleVao);
            _gl.DeleteProgram(_shaderProgram);
        }

        base.OnOpenGlDeinit(gl);
    }

    // Public methods for camera control (to be called from UI)
    public void SetCamera(double x, double y, double zoom, double rotation)
    {
        _cameraX = x;
        _cameraY = y;
        _zoom = zoom;
        _rotation = rotation;
        RequestNextFrameRendering();
    }

    public void Pan(double deltaX, double deltaY)
    {
        _cameraX += deltaX;
        _cameraY += deltaY;
        RequestNextFrameRendering();
    }

    public void Zoom(double delta)
    {
        if (_is3DMode)
        {
            // In 3D mode, adjust camera distance
            _cameraDistance *= (1.0 / delta); // Inverse - larger zoom factor = closer camera
            _cameraDistance = Math.Clamp(_cameraDistance, 10.0, 500.0);
        }
        else
        {
            // In 2D mode, adjust zoom level
            _zoom *= delta;
            _zoom = Math.Clamp(_zoom, 0.1, 100.0);
        }
        RequestNextFrameRendering();
    }

    public void Rotate(double deltaRadians)
    {
        _rotation += deltaRadians;
        RequestNextFrameRendering();
    }

    public double GetZoom() => _zoom;

    public void SetGridVisible(bool visible)
    {
        IsGridVisible = visible;
    }

    // Public methods for external mouse control
    public void StartPan(Point position)
    {
        _isPanning = true;
        _lastMousePosition = position;
    }

    public void StartRotate(Point position)
    {
        _isRotating = true;
        _lastMousePosition = position;
    }

    public void UpdateMouse(Point position)
    {
        if (_isPanning)
        {
            if (_is3DMode)
            {
                // In 3D mode, left-click drag adjusts pitch (camera tilt)
                double deltaY = position.Y - _lastMousePosition.Y;
                double pitchDelta = -deltaY * 0.005; // 0.005 radians per pixel
                SetPitch(pitchDelta);
            }
            else
            {
                // In 2D mode, left-click drag pans the camera
                double deltaX = position.X - _lastMousePosition.X;
                double deltaY = position.Y - _lastMousePosition.Y;

                // Convert screen space delta to world space (accounting for zoom and aspect ratio)
                float aspect = (float)Bounds.Width / (float)Bounds.Height;
                double worldDeltaX = -deltaX * (200.0 * aspect / _zoom) / Bounds.Width;
                double worldDeltaY = -deltaY * (200.0 / _zoom) / Bounds.Height;

                Pan(worldDeltaX, worldDeltaY);
            }
            _lastMousePosition = position;
        }
        else if (_isRotating)
        {
            // Calculate rotation based on horizontal mouse movement
            double deltaX = position.X - _lastMousePosition.X;
            double rotationDelta = deltaX * 0.01; // 0.01 radians per pixel

            Rotate(rotationDelta);
            _lastMousePosition = position;
        }
    }

    public void EndPanRotate()
    {
        _isPanning = false;
        _isRotating = false;
    }

    public void Toggle3DMode()
    {
        _is3DMode = !_is3DMode;
        if (_is3DMode)
        {
            // Set initial 3D camera parameters
            _cameraPitch = Math.PI / 6.0; // 30 degrees
            _cameraDistance = 150.0;
        }
        else
        {
            // Reset to 2D
            _cameraPitch = 0.0;
        }
        RequestNextFrameRendering();
    }

    public void Set3DMode(bool is3D)
    {
        if (_is3DMode != is3D)
        {
            Toggle3DMode();
        }
    }

    public bool Is3DMode => _is3DMode;

    public void PanTo(double x, double y)
    {
        _cameraX = x;
        _cameraY = y;
        RequestNextFrameRendering();
    }

    public void SetPitch(double deltaRadians)
    {
        _cameraPitch += deltaRadians;
        _cameraPitch = Math.Clamp(_cameraPitch, 0.0, Math.PI / 2.5); // Limit pitch
        RequestNextFrameRendering();
    }

    public void SetPitchAbsolute(double pitchRadians)
    {
        _cameraPitch = Math.Clamp(pitchRadians, 0.0, Math.PI / 2.5);
        RequestNextFrameRendering();
    }

    public void SetVehiclePosition(double x, double y, double heading)
    {
        _vehicleX = x;
        _vehicleY = y;
        _vehicleHeading = heading;
        RequestNextFrameRendering();
    }

    // Mouse event handlers for camera control
    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        Console.WriteLine("OnPointerPressed called!");
        var point = e.GetCurrentPoint(this);

        if (point.Properties.IsLeftButtonPressed)
        {
            Console.WriteLine("Left button pressed - starting pan");
            _isPanning = true;
            _lastMousePosition = point.Position;
            e.Pointer.Capture(this);
            e.Handled = true;
        }
        else if (point.Properties.IsRightButtonPressed)
        {
            Console.WriteLine("Right button pressed - starting rotation");
            _isRotating = true;
            _lastMousePosition = point.Position;
            e.Pointer.Capture(this);
            e.Handled = true;
        }
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        var point = e.GetCurrentPoint(this);
        var currentPos = point.Position;

        if (_isPanning)
        {
            if (_is3DMode)
            {
                // In 3D mode, left-click drag adjusts pitch (camera tilt)
                double deltaY = currentPos.Y - _lastMousePosition.Y;
                double pitchDelta = -deltaY * 0.005; // 0.005 radians per pixel
                SetPitch(pitchDelta);
            }
            else
            {
                // In 2D mode, left-click drag pans the camera
                double deltaX = currentPos.X - _lastMousePosition.X;
                double deltaY = currentPos.Y - _lastMousePosition.Y;

                // Convert screen space delta to world space (accounting for zoom and aspect ratio)
                float aspect = (float)Bounds.Width / (float)Bounds.Height;
                double worldDeltaX = -deltaX * (200.0 * aspect / _zoom) / Bounds.Width;
                double worldDeltaY = deltaY * (200.0 / _zoom) / Bounds.Height;

                Pan(worldDeltaX, worldDeltaY);
            }
            _lastMousePosition = currentPos;
            e.Handled = true;
        }
        else if (_isRotating)
        {
            // Calculate rotation based on horizontal mouse movement
            double deltaX = currentPos.X - _lastMousePosition.X;
            double rotationDelta = deltaX * 0.01; // 0.01 radians per pixel

            Rotate(rotationDelta);
            _lastMousePosition = currentPos;
            e.Handled = true;
        }
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_isPanning || _isRotating)
        {
            _isPanning = false;
            _isRotating = false;
            e.Pointer.Capture(null);
            e.Handled = true;
        }
    }

    private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (_is3DMode)
        {
            // In 3D mode, adjust camera distance (zoom in/out from vehicle)
            double distanceFactor = e.Delta.Y > 0 ? 0.9 : 1.1; // Inverse - scroll up = closer
            _cameraDistance *= distanceFactor;
            _cameraDistance = Math.Clamp(_cameraDistance, 10.0, 500.0); // Limit distance
            RequestNextFrameRendering();
        }
        else
        {
            // In 2D mode, zoom in/out
            double zoomFactor = e.Delta.Y > 0 ? 1.1 : 0.9;
            Zoom(zoomFactor);
        }
        e.Handled = true;
    }

    /// <summary>
    /// Set the field boundary to render (deferred to render thread)
    /// </summary>
    public void SetBoundary(Boundary? boundary)
    {
        _pendingBoundary = boundary;
        _hasPendingBoundaryUpdate = true;
    }

    private void ClearBoundary()
    {
        if (_gl == null) return;

        // Delete boundary VAO and VBO
        if (_boundaryVao != 0)
        {
            _gl.DeleteVertexArray(_boundaryVao);
            _gl.DeleteBuffer(_boundaryVbo);
            _boundaryVao = 0;
            _boundaryVbo = 0;
        }
        _boundarySegments.Clear();
    }

    private void InitializeBoundary(Boundary boundary)
    {
        if (_gl == null) return;

        // Clear existing boundary data
        ClearBoundary();

        // Build vertex data for boundary (position + color: x, y, r, g, b, a)
        List<float> vertices = new List<float>();
        int currentOffset = 0;

        // Add outer boundary as LINE_LOOP (yellow color)
        if (boundary.OuterBoundary != null && boundary.OuterBoundary.IsValid)
        {
            int pointCount = boundary.OuterBoundary.Points.Count;
            foreach (var point in boundary.OuterBoundary.Points)
            {
                vertices.Add((float)point.Easting);   // x
                vertices.Add((float)point.Northing);  // y
                vertices.Add(1.0f);                    // r (yellow)
                vertices.Add(1.0f);                    // g
                vertices.Add(0.0f);                    // b
                vertices.Add(1.0f);                    // a
            }
            _boundarySegments.Add((currentOffset, pointCount));
            currentOffset += pointCount;
        }

        // Add inner boundaries as LINE_LOOPs (red color for holes)
        foreach (var innerBoundary in boundary.InnerBoundaries)
        {
            if (innerBoundary.IsValid)
            {
                int pointCount = innerBoundary.Points.Count;
                foreach (var point in innerBoundary.Points)
                {
                    vertices.Add((float)point.Easting);   // x
                    vertices.Add((float)point.Northing);  // y
                    vertices.Add(1.0f);                    // r (red)
                    vertices.Add(0.0f);                    // g
                    vertices.Add(0.0f);                    // b
                    vertices.Add(1.0f);                    // a
                }
                _boundarySegments.Add((currentOffset, pointCount));
                currentOffset += pointCount;
            }
        }

        if (vertices.Count == 0)
            return;

        // Create VAO and VBO for boundary
        _boundaryVao = _gl.GenVertexArray();
        _gl.BindVertexArray(_boundaryVao);

        _boundaryVbo = _gl.GenBuffer();
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _boundaryVbo);

        unsafe
        {
            fixed (float* v = vertices.ToArray())
            {
                _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(vertices.Count * sizeof(float)), v, BufferUsageARB.StaticDraw);
            }
        }

        // Position attribute (location 0): x, y
        // Color attribute (location 1): r, g, b, a
        // Stride: 6 floats (2 position + 4 color)
        int stride = 6 * sizeof(float);

        unsafe
        {
            // Position attribute (location 0)
            _gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, (uint)stride, (void*)0);
            _gl.EnableVertexAttribArray(0);

            // Color attribute (location 1)
            _gl.VertexAttribPointer(1, 4, VertexAttribPointerType.Float, false, (uint)stride, (void*)(2 * sizeof(float)));
            _gl.EnableVertexAttribArray(1);
        }

        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);
        _gl.BindVertexArray(0);
    }

    /// <summary>
    /// Set the recording boundary points to display (deferred to render thread)
    /// </summary>
    public void SetRecordingPoints(IReadOnlyList<(double Easting, double Northing)> points)
    {
        _pendingRecordingPoints = points.ToList();
        _hasPendingRecordingUpdate = true;
    }

    /// <summary>
    /// Clear the recording boundary display
    /// </summary>
    public void ClearRecordingPoints()
    {
        _pendingRecordingPoints = null;
        _hasPendingRecordingUpdate = true;
    }

    /// <summary>
    /// Set the boundary offset indicator visibility and offset value
    /// </summary>
    /// <param name="show">Whether to show the indicator</param>
    /// <param name="offsetMeters">Offset in meters (positive = right of vehicle heading, negative = left)</param>
    public void SetBoundaryOffsetIndicator(bool show, double offsetMeters = 0.0)
    {
        _showBoundaryOffsetIndicator = show;
        _boundaryOffsetMeters = offsetMeters;
    }

    /// <summary>
    /// Draw the boundary offset indicator (teal reference point + yellow arrow showing offset)
    /// </summary>
    private void DrawBoundaryOffsetIndicator(float[] mvp)
    {
        if (_gl == null) return;

        // Calculate reference point (vehicle position) and offset point
        // Offset is perpendicular to heading (positive = right)
        float refX = (float)_vehicleX;
        float refY = (float)_vehicleY;

        // Calculate perpendicular direction (90 degrees clockwise from heading for "right")
        // heading is in radians, 0 = North, clockwise positive
        float perpAngle = (float)_vehicleHeading + (float)(Math.PI / 2.0); // 90 degrees right
        float offsetX = refX + (float)(_boundaryOffsetMeters * Math.Sin(perpAngle));
        float offsetY = refY + (float)(_boundaryOffsetMeters * Math.Cos(perpAngle));

        // Build vertex data for the indicator
        // Format: x, y, r, g, b, a (6 floats per vertex)
        List<float> vertices = new List<float>();

        // Teal reference square (4 vertices for a small filled quad at vehicle position)
        float squareSize = 0.3f; // meters
        // Cyan/teal color (0, 0.8, 0.8)
        float tR = 0.0f, tG = 0.8f, tB = 0.8f, tA = 1.0f;

        // Square vertices (triangle fan: center + 4 corners)
        vertices.AddRange(new float[] { refX, refY, tR, tG, tB, tA }); // center
        vertices.AddRange(new float[] { refX - squareSize, refY - squareSize, tR, tG, tB, tA });
        vertices.AddRange(new float[] { refX + squareSize, refY - squareSize, tR, tG, tB, tA });
        vertices.AddRange(new float[] { refX + squareSize, refY + squareSize, tR, tG, tB, tA });
        vertices.AddRange(new float[] { refX - squareSize, refY + squareSize, tR, tG, tB, tA });
        vertices.AddRange(new float[] { refX - squareSize, refY - squareSize, tR, tG, tB, tA }); // close

        int squareVertexCount = 6;

        // Yellow arrow line (from ref point to offset point) - only if offset is non-zero
        float yR = 1.0f, yG = 0.9f, yB = 0.0f, yA = 1.0f; // Yellow
        int arrowLineVertexCount = 0;
        int arrowHeadVertexCount = 0;

        if (Math.Abs(_boundaryOffsetMeters) > 0.01)
        {
            // Arrow line
            vertices.AddRange(new float[] { refX, refY, yR, yG, yB, yA });
            vertices.AddRange(new float[] { offsetX, offsetY, yR, yG, yB, yA });
            arrowLineVertexCount = 2;

            // Arrowhead at offset point (triangle pointing in offset direction)
            float arrowSize = 0.4f;
            // Direction from ref to offset
            float dx = offsetX - refX;
            float dy = offsetY - refY;
            float len = (float)Math.Sqrt(dx * dx + dy * dy);
            if (len > 0.001f)
            {
                dx /= len;
                dy /= len;
                // Perpendicular to arrow direction
                float px = -dy;
                float py = dx;

                // Arrow tip is at offset point, base is behind it
                float tipX = offsetX;
                float tipY = offsetY;
                float baseX = offsetX - dx * arrowSize;
                float baseY = offsetY - dy * arrowSize;

                // Triangle vertices
                vertices.AddRange(new float[] { tipX, tipY, yR, yG, yB, yA });
                vertices.AddRange(new float[] { baseX + px * arrowSize * 0.5f, baseY + py * arrowSize * 0.5f, yR, yG, yB, yA });
                vertices.AddRange(new float[] { baseX - px * arrowSize * 0.5f, baseY - py * arrowSize * 0.5f, yR, yG, yB, yA });
                arrowHeadVertexCount = 3;
            }
        }

        // Create temporary VAO/VBO for this frame
        uint vao = _gl.GenVertexArray();
        uint vbo = _gl.GenBuffer();

        _gl.BindVertexArray(vao);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, vbo);

        float[] vertexArray = vertices.ToArray();
        unsafe
        {
            fixed (float* v = vertexArray)
            {
                _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(vertexArray.Length * sizeof(float)), v, BufferUsageARB.StreamDraw);
            }
        }

        int stride = 6 * sizeof(float);
        unsafe
        {
            _gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, (uint)stride, (void*)0);
            _gl.EnableVertexAttribArray(0);
            _gl.VertexAttribPointer(1, 4, VertexAttribPointerType.Float, false, (uint)stride, (void*)(2 * sizeof(float)));
            _gl.EnableVertexAttribArray(1);
        }

        // Use color shader with identity MVP (world coordinates already in vertices)
        _gl.UseProgram(_shaderProgram);
        unsafe
        {
            fixed (float* m = mvp)
            {
                _gl.UniformMatrix4(_gl.GetUniformLocation(_shaderProgram, "uMVP"), 1, false, m);
            }
        }

        // Draw reference square (triangle fan)
        _gl.DrawArrays(PrimitiveType.TriangleFan, 0, (uint)squareVertexCount);

        // Draw arrow line
        if (arrowLineVertexCount > 0)
        {
            _gl.LineWidth(3.0f);
            _gl.DrawArrays(PrimitiveType.Lines, (int)squareVertexCount, (uint)arrowLineVertexCount);
            _gl.LineWidth(1.0f);
        }

        // Draw arrowhead
        if (arrowHeadVertexCount > 0)
        {
            _gl.DrawArrays(PrimitiveType.Triangles, (int)(squareVertexCount + arrowLineVertexCount), (uint)arrowHeadVertexCount);
        }

        // Cleanup
        _gl.BindVertexArray(0);
        _gl.DeleteVertexArray(vao);
        _gl.DeleteBuffer(vbo);
    }

    private void ClearRecordingBuffers()
    {
        if (_gl == null) return;

        if (_recordingVao != 0)
        {
            _gl.DeleteVertexArray(_recordingVao);
            _gl.DeleteBuffer(_recordingVbo);
            _recordingVao = 0;
            _recordingVbo = 0;
        }

        if (_recordingPointsVao != 0)
        {
            _gl.DeleteVertexArray(_recordingPointsVao);
            _gl.DeleteBuffer(_recordingPointsVbo);
            _recordingPointsVao = 0;
            _recordingPointsVbo = 0;
        }

        _recordingLineVertexCount = 0;
        _recordingPointVertexCount = 0;
    }

    private void UpdateRecordingBuffers(List<(double Easting, double Northing)> points)
    {
        if (_gl == null || points.Count == 0) return;

        // Clear existing buffers
        ClearRecordingBuffers();

        // Build vertex data for lines (cyan color: x, y, r, g, b, a)
        List<float> lineVertices = new List<float>();
        foreach (var point in points)
        {
            lineVertices.Add((float)point.Easting);   // x
            lineVertices.Add((float)point.Northing);  // y
            lineVertices.Add(0.0f);                    // r (cyan)
            lineVertices.Add(1.0f);                    // g
            lineVertices.Add(1.0f);                    // b
            lineVertices.Add(1.0f);                    // a
        }

        _recordingLineVertexCount = points.Count;

        // Create VAO and VBO for lines
        _recordingVao = _gl.GenVertexArray();
        _gl.BindVertexArray(_recordingVao);

        _recordingVbo = _gl.GenBuffer();
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _recordingVbo);

        unsafe
        {
            fixed (float* v = lineVertices.ToArray())
            {
                _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(lineVertices.Count * sizeof(float)), v, BufferUsageARB.DynamicDraw);
            }
        }

        int stride = 6 * sizeof(float);
        unsafe
        {
            _gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, (uint)stride, (void*)0);
            _gl.EnableVertexAttribArray(0);
            _gl.VertexAttribPointer(1, 4, VertexAttribPointerType.Float, false, (uint)stride, (void*)(2 * sizeof(float)));
            _gl.EnableVertexAttribArray(1);
        }

        _gl.BindVertexArray(0);

        // Build vertex data for points (red/orange markers)
        List<float> pointVertices = new List<float>();
        foreach (var point in points)
        {
            pointVertices.Add((float)point.Easting);   // x
            pointVertices.Add((float)point.Northing);  // y
            pointVertices.Add(1.0f);                    // r (red/orange)
            pointVertices.Add(0.5f);                    // g
            pointVertices.Add(0.0f);                    // b
            pointVertices.Add(1.0f);                    // a
        }

        _recordingPointVertexCount = points.Count;

        // Create VAO and VBO for points
        _recordingPointsVao = _gl.GenVertexArray();
        _gl.BindVertexArray(_recordingPointsVao);

        _recordingPointsVbo = _gl.GenBuffer();
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _recordingPointsVbo);

        unsafe
        {
            fixed (float* v = pointVertices.ToArray())
            {
                _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(pointVertices.Count * sizeof(float)), v, BufferUsageARB.DynamicDraw);
            }
        }

        unsafe
        {
            _gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, (uint)stride, (void*)0);
            _gl.EnableVertexAttribArray(0);
            _gl.VertexAttribPointer(1, 4, VertexAttribPointerType.Float, false, (uint)stride, (void*)(2 * sizeof(float)));
            _gl.EnableVertexAttribArray(1);
        }

        _gl.BindVertexArray(0);
    }

    /// <summary>
    /// Set the background satellite image to display (deferred to render thread)
    /// </summary>
    /// <param name="imagePath">Path to the PNG image file</param>
    /// <param name="minX">West edge easting (meters)</param>
    /// <param name="maxY">North edge northing (meters)</param>
    /// <param name="maxX">East edge easting (meters)</param>
    /// <param name="minY">South edge northing (meters)</param>
    public void SetBackgroundImage(string imagePath, double minX, double maxY, double maxX, double minY)
    {
        _pendingBackgroundImagePath = imagePath;
        _pendingBgMinX = minX;
        _pendingBgMaxX = maxX;
        _pendingBgMinY = minY;
        _pendingBgMaxY = maxY;
        _hasPendingBackgroundUpdate = true;
        Console.WriteLine($"SetBackgroundImage: {imagePath}");
        Console.WriteLine($"  Bounds: minX={minX:F2}, maxX={maxX:F2}, minY={minY:F2}, maxY={maxY:F2}");
    }

    /// <summary>
    /// Clear the background satellite image
    /// </summary>
    public void ClearBackground()
    {
        _pendingBackgroundImagePath = null;
        _hasPendingBackgroundUpdate = true;
    }

    private void ClearBackgroundImage()
    {
        if (_gl == null) return;

        if (_backgroundTexture != 0)
        {
            _gl.DeleteTexture(_backgroundTexture);
            _backgroundTexture = 0;
        }

        if (_backgroundVao != 0)
        {
            _gl.DeleteVertexArray(_backgroundVao);
            _gl.DeleteBuffer(_backgroundVbo);
            _backgroundVao = 0;
            _backgroundVbo = 0;
        }

        _hasBackgroundImage = false;
    }

    private void LoadBackgroundTexture(string imagePath, double minX, double maxX, double minY, double maxY)
    {
        if (_gl == null) return;

        // Clear any existing background
        ClearBackgroundImage();

        if (!File.Exists(imagePath))
        {
            Console.WriteLine($"Background image not found: {imagePath}");
            return;
        }

        try
        {
            // Load image using StbImageSharp
            StbImage.stbi_set_flip_vertically_on_load(1);
            using var stream = File.OpenRead(imagePath);
            ImageResult image = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);

            Console.WriteLine($"Loaded background image: {image.Width}x{image.Height}");

            // Create OpenGL texture
            _backgroundTexture = _gl.GenTexture();
            _gl.BindTexture(TextureTarget.Texture2D, _backgroundTexture);

            unsafe
            {
                fixed (byte* ptr = image.Data)
                {
                    _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba,
                        (uint)image.Width, (uint)image.Height, 0,
                        PixelFormat.Rgba, PixelType.UnsignedByte, ptr);
                }
            }

            // Set texture parameters
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Linear);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);

            _gl.BindTexture(TextureTarget.Texture2D, 0);

            // Store geo-reference bounds
            _backgroundMinX = minX;
            _backgroundMaxX = maxX;
            _backgroundMinY = minY;
            _backgroundMaxY = maxY;

            // Create VAO/VBO for background quad
            // Vertices: position (x, y), texcoord (u, v)
            // The quad spans from (minX, minY) to (maxX, maxY) in world coordinates
            float[] vertices = new float[]
            {
                // Position (x, y), TexCoord (u, v)
                (float)minX, (float)minY,  0.0f, 0.0f,  // Bottom-left (SW)
                (float)maxX, (float)minY,  1.0f, 0.0f,  // Bottom-right (SE)
                (float)maxX, (float)maxY,  1.0f, 1.0f,  // Top-right (NE)
                (float)minX, (float)maxY,  0.0f, 1.0f   // Top-left (NW)
            };

            _backgroundVao = _gl.GenVertexArray();
            _gl.BindVertexArray(_backgroundVao);

            _backgroundVbo = _gl.GenBuffer();
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _backgroundVbo);

            unsafe
            {
                fixed (float* v = vertices)
                {
                    _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(vertices.Length * sizeof(float)), v, BufferUsageARB.StaticDraw);
                }
            }

            // Position attribute (location 0)
            unsafe
            {
                _gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), (void*)0);
            }
            _gl.EnableVertexAttribArray(0);

            // Texture coordinate attribute (location 1)
            unsafe
            {
                _gl.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), (void*)(2 * sizeof(float)));
            }
            _gl.EnableVertexAttribArray(1);

            _gl.BindVertexArray(0);

            _hasBackgroundImage = true;
            Console.WriteLine("Background image loaded successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading background image: {ex.Message}");
            ClearBackgroundImage();
        }
    }
}
