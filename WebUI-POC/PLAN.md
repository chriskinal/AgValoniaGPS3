# AgOpenGPS WebUI Hybrid Architecture Plan

## Overview

This document outlines the plan to migrate AgOpenGPS from platform-specific Avalonia UI to a universal WebUI that runs identically on all platforms.

### Rationale

| Approach | Desktop | iOS | Android | Total Effort |
|----------|---------|-----|---------|--------------|
| Avalonia | 100% | ~80% | ~80% | **260%** |
| WebUI    | 100% | 0%  | 0%  | **100%** |

The WebUI approach builds one interface that works everywhere, eliminating platform-specific UI development.

### Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    Native Shell (per platform)               │
│  ┌─────────────────────────────────────────────────────────┐│
│  │                   WebView / Browser                      ││
│  │  ┌─────────────────────────────────────────────────────┐││
│  │  │              HTML5 / CSS / JavaScript                │││
│  │  │  ┌─────────────────────────────────────────────────┐│││
│  │  │  │           WebGL Map Renderer                     ││││
│  │  │  └─────────────────────────────────────────────────┘│││
│  │  └─────────────────────────────────────────────────────┘││
│  └─────────────────────────────────────────────────────────┘│
│                              ↕ WebSocket                     │
│  ┌─────────────────────────────────────────────────────────┐│
│  │              C# Backend Services (existing)              ││
│  │   GPS | UDP | NTRIP | Field | Boundary | Simulator       ││
│  └─────────────────────────────────────────────────────────┘│
│                              ↕ Native APIs                   │
│  ┌─────────────────────────────────────────────────────────┐│
│  │         Platform Bridge (UDP, Serial, GPS, Files)        ││
│  └─────────────────────────────────────────────────────────┘│
└─────────────────────────────────────────────────────────────┘
```

---

## Phase 1: Core Infrastructure

**Goal:** Establish the hybrid architecture with WebSocket communication

### 1.1 Backend WebSocket Server

Add minimal ASP.NET Core WebSocket server to existing services:

```
Shared/
└── AgValoniaGPS.WebHost/           # NEW
    ├── AgValoniaGPS.WebHost.csproj
    ├── Program.cs                   # Minimal API host
    ├── WebSocketHandler.cs          # Real-time GPS streaming
    └── Endpoints/
        ├── GpsEndpoint.cs           # GET /api/gps, WS /ws/gps
        ├── SettingsEndpoint.cs      # GET/POST /api/settings
        ├── SimulatorEndpoint.cs     # POST /api/simulator/*
        ├── FieldEndpoint.cs         # Field/boundary CRUD
        └── NtripEndpoint.cs         # NTRIP configuration
```

**WebSocket Messages (JSON):**
```json
// Server → Client (10Hz)
{
  "type": "gps",
  "data": {
    "latitude": 40.7128,
    "longitude": -74.0060,
    "easting": 123.45,
    "northing": 678.90,
    "heading": 45.0,
    "speed": 5.2,
    "fixQuality": 4,
    "satellites": 12
  }
}

// Client → Server
{
  "type": "simulator",
  "action": "setSpeed",
  "value": 5.0
}
```

### 1.2 Web Project Structure

```
WebUI/
├── index.html              # Single page app entry
├── manifest.json           # PWA manifest
├── sw.js                   # Service worker (offline)
├── src/
│   ├── main.js             # App initialization
│   ├── state.js            # Reactive state management
│   ├── websocket.js        # Backend connection
│   ├── components/
│   │   ├── nav-bar.js      # Left navigation
│   │   ├── status-bar.js   # Top status displays
│   │   ├── toolbar.js      # Bottom controls
│   │   ├── sidebar.js      # Right panel
│   │   └── dialogs/        # Modal dialogs
│   └── map/
│       ├── renderer.js     # WebGL renderer
│       ├── camera.js       # View transforms
│       ├── grid.js         # Grid drawing
│       ├── vehicle.js      # Tractor rendering
│       ├── boundary.js     # Polygon rendering
│       └── shaders/
│           ├── basic.vert
│           └── basic.frag
├── styles/
│   ├── main.css            # Base styles
│   ├── components.css      # Component styles
│   └── themes/
│       ├── dark.css
│       └── light.css
└── assets/
    └── icons/              # UI icons (SVG)
```

### 1.3 Native Shells

**Desktop (Tauri - Rust):**
- ~200 lines of Rust
- Bundles WebUI + C# backend
- Native UDP/Serial access via Tauri commands
- Builds to ~10MB executable

**iOS (Swift):**
- ~100 lines of Swift
- WKWebView wrapper
- Native CoreLocation for GPS
- UDP via Network.framework

**Android (Kotlin):**
- ~100 lines of Kotlin
- WebView wrapper
- Native LocationManager
- UDP via DatagramSocket

### Deliverables
- [ ] WebSocket server project created
- [ ] GPS streaming endpoint working
- [ ] Settings REST endpoints working
- [ ] WebUI connects and receives data
- [ ] Basic Tauri shell launches WebUI

---

## Phase 2: Map & Vehicle Rendering

**Goal:** Feature parity with SkiaSharp/OpenGL map

### 2.1 WebGL Renderer
- [ ] Grid with configurable spacing (minor/major lines)
- [ ] Colored axes (X=red, Y=green)
- [ ] Smooth pan/zoom/rotate
- [ ] Camera follow mode (lock to vehicle)
- [ ] Free camera mode (manual pan)

### 2.2 Vehicle Rendering
- [ ] Tractor triangle with heading
- [ ] Heading trail (breadcrumb line)
- [ ] Steering angle indicator
- [ ] Vehicle dimensions overlay

### 2.3 Field Elements
- [ ] Boundary polygons (outer=yellow, inner=red)
- [ ] AB lines and curves
- [ ] Tramlines
- [ ] Headland paths
- [ ] Coverage/painted sections

### 2.4 Touch/Mouse Gestures
- [ ] Single finger pan
- [ ] Pinch to zoom
- [ ] Two-finger rotate
- [ ] Double-tap to reset view
- [ ] Mouse wheel zoom
- [ ] Right-click drag rotate

### Deliverables
- [ ] WebGL map matches SkiaSharp quality
- [ ] 60fps on mobile devices
- [ ] All gestures working on touch and mouse

---

## Phase 3: Core UI Panels

**Goal:** Main operational interface

### 3.1 Top Status Bar
- [ ] GPS fix quality indicator (colored dot)
- [ ] Speed display (km/h or mph)
- [ ] Cross-track error (XTE) - large, centered
- [ ] Heading display
- [ ] Satellite count
- [ ] HDOP indicator

### 3.2 Left Navigation (8 buttons)
1. [ ] File Menu - New/Load profile, Language, Simulator, About
2. [ ] View Settings - Grid, 3D mode, Zoom presets
3. [ ] Tools - Special functions
4. [ ] Configuration - Vehicle, Implement, GPS setup
5. [ ] Job Menu - Start/Stop job, Job info
6. [ ] Field Tools - Boundary record/draw, KML import
7. [ ] AutoSteer Config - PID tuning, Steer settings
8. [ ] Data I/O - UDP config, Serial ports, NTRIP

### 3.3 Bottom Toolbar
- [ ] AB Line button (set A, set B, snap)
- [ ] Contour mode toggle
- [ ] U-turn trigger
- [ ] Section master on/off
- [ ] Implement width display
- [ ] Area counter

### 3.4 Right Sidebar
- [ ] AutoSteer engage (large button)
- [ ] Section buttons (1-16 grid)
- [ ] Manual section override
- [ ] Steer angle display

### 3.5 Simulator Panel
- [ ] Enable/disable toggle
- [ ] Steering controls (left/zero/right)
- [ ] Speed controls (accel/stop/decel)
- [ ] Coordinate entry
- [ ] Reset position

### Deliverables
- [ ] All panels functional
- [ ] Responsive layout (tablet vs phone)
- [ ] Touch-friendly button sizes

---

## Phase 4: Dialogs & Settings

**Goal:** Full configuration capability

### 4.1 Vehicle Configuration
- [ ] Vehicle type selection
- [ ] Wheelbase
- [ ] Antenna position (offset from axle)
- [ ] Turn radius
- [ ] Look-ahead distance

### 4.2 Implement Setup
- [ ] Implement type (toolbar, planter, sprayer)
- [ ] Width
- [ ] Number of sections
- [ ] Section widths (individual)
- [ ] Overlap/gap settings
- [ ] Offset from vehicle

### 4.3 GPS/Antenna Settings
- [ ] Antenna height
- [ ] Forward/lateral offset
- [ ] Dual antenna settings
- [ ] Roll compensation

### 4.4 Steering Configuration
- [ ] PID values (P, I, D)
- [ ] Max steer angle
- [ ] Counts per degree
- [ ] Ackerman compensation
- [ ] Steer motor type

### 4.5 NTRIP Configuration
- [ ] Caster address/port
- [ ] Mount point browser
- [ ] Username/password
- [ ] Connection status
- [ ] GGA send interval

### 4.6 Field/Boundary Management
- [ ] Field list view
- [ ] Create new field
- [ ] Load existing field
- [ ] Boundary recording wizard
- [ ] KML/SHP import
- [ ] Boundary offset tool

### Deliverables
- [ ] All settings persist correctly
- [ ] Settings sync to C# backend
- [ ] Import/export working

---

## Phase 5: Platform Integration

**Goal:** Native functionality per platform

### 5.1 Desktop (Tauri)

| Feature | Implementation |
|---------|----------------|
| UDP Sockets | Rust `std::net::UdpSocket` via Tauri command |
| Serial Ports | `serialport` crate via Tauri command |
| File System | Native file dialogs via Tauri API |
| Window Management | Tauri window API |

### 5.2 iOS (Swift)

| Feature | Implementation |
|---------|----------------|
| UDP Sockets | `Network.framework` NWConnection |
| GPS | CoreLocation `CLLocationManager` |
| File System | iOS sandbox + document picker |
| Background | Background modes for GPS |

### 5.3 Android (Kotlin)

| Feature | Implementation |
|---------|----------------|
| UDP Sockets | `java.net.DatagramSocket` |
| GPS | `LocationManager` / Fused Location |
| Serial | USB-OTG via `android-serial-api` |
| File System | Scoped storage + SAF |

### 5.4 JavaScript Bridge

```javascript
// Unified API across platforms
const native = {
  async sendUdp(host, port, data) {
    if (window.__TAURI__) {
      return await invoke('send_udp', { host, port, data });
    } else if (window.webkit?.messageHandlers) {
      return await iOSBridge.sendUdp(host, port, data);
    } else if (window.AndroidBridge) {
      return AndroidBridge.sendUdp(host, port, data);
    }
    throw new Error('No native bridge available');
  }
};
```

### Deliverables
- [ ] Tauri desktop app builds and runs
- [ ] iOS app with WKWebView working
- [ ] Android app with WebView working
- [ ] UDP communication working on all platforms
- [ ] GPS working on mobile platforms

---

## Phase 6: Polish & Testing

**Goal:** Production readiness

### 6.1 Offline Support
- [ ] Service worker caches all assets
- [ ] Works without internet connection
- [ ] Settings persist locally
- [ ] Sync when online

### 6.2 Responsive Design
- [ ] Tablet landscape (primary)
- [ ] Tablet portrait
- [ ] Phone landscape
- [ ] Desktop window resizing

### 6.3 Themes
- [ ] Dark theme (default)
- [ ] Light theme
- [ ] High contrast option
- [ ] Theme persistence

### 6.4 Performance
- [ ] 60fps map rendering
- [ ] <100ms touch response
- [ ] <3s cold start
- [ ] <200MB memory usage

### 6.5 Testing
- [ ] Chrome (Windows, Mac, Linux)
- [ ] Safari (Mac, iOS)
- [ ] Edge (Windows)
- [ ] Firefox (all platforms)
- [ ] iOS Safari (iPhone, iPad)
- [ ] Android Chrome (phones, tablets)

### Deliverables
- [ ] All tests passing
- [ ] Performance targets met
- [ ] App store ready (iOS/Android)

---

## Timeline Estimate

| Phase | Duration | Cumulative |
|-------|----------|------------|
| Phase 1: Infrastructure | 1-2 weeks | 1-2 weeks |
| Phase 2: Map & Vehicle | 1 week | 2-3 weeks |
| Phase 3: Core UI | 2 weeks | 4-5 weeks |
| Phase 4: Dialogs | 1-2 weeks | 5-7 weeks |
| Phase 5: Platform Integration | 1 week | 6-8 weeks |
| Phase 6: Polish | 1 week | 7-9 weeks |

**Total: 7-9 weeks**

---

## What We Keep

All existing C# code in `Shared/` remains:

```
Shared/
├── AgValoniaGPS.Models/      # Unchanged
├── AgValoniaGPS.Services/    # Unchanged
└── AgValoniaGPS.ViewModels/  # Data source for WebSocket
```

---

## Migration Strategy

1. **Parallel Development** - Build WebUI while Avalonia code remains
2. **Feature Parity Testing** - Compare functionality side by side
3. **Gradual Transition** - Switch platforms one at a time
4. **Fallback Option** - Keep Avalonia desktop as backup

---

## Open Questions

1. **Build System** - Vite, Webpack, or vanilla JS?
2. **State Management** - Custom reactive, or use library?
3. **Component Framework** - Vanilla JS, Lit, or Svelte?
4. **Testing Framework** - Playwright, Cypress, or manual?
5. **CI/CD** - GitHub Actions for multi-platform builds?

---

## Next Steps

1. Create `webui-hybrid` branch ✓
2. Set up WebSocket server project
3. Expand POC into structured project
4. Get GPS data flowing to browser
5. Iterate on map rendering
