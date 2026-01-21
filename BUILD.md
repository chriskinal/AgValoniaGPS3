<!--
AgValoniaGPS
Copyright (C) 2024-2025 AgValoniaGPS Contributors

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program. If not, see <https://www.gnu.org/licenses/>.
-->

# AgValoniaGPS3 Build Guide

This guide covers building AgValoniaGPS3 on Windows, macOS (Intel and Apple Silicon), and Linux.

## Table of Contents

- [Prerequisites](#prerequisites)
  - [All Platforms](#all-platforms)
  - [Windows](#windows)
  - [macOS](#macos)
  - [Linux](#linux)
- [Building Desktop Application](#building-desktop-application)
  - [Windows](#windows-1)
  - [macOS (Intel)](#macos-intel)
  - [macOS (Apple Silicon)](#macos-apple-silicon)
  - [Linux](#linux-1)
- [Building iOS Application](#building-ios-application)
- [Publishing](#publishing)
- [Troubleshooting](#troubleshooting)

---

## Prerequisites

### All Platforms

1. **.NET 10 SDK**

   Download from: https://dotnet.microsoft.com/download/dotnet/10.0

   Verify installation:
   ```bash
   dotnet --version
   ```
   Expected output: `10.0.x` or higher

2. **Git** (for cloning the repository)
   ```bash
   git clone https://github.com/your-repo/AgValoniaGPS3.git
   cd AgValoniaGPS3
   ```

3. **Restore NuGet packages** (automatic on first build, or manual):
   ```bash
   dotnet restore AgValoniaGPS.sln
   ```

### Windows

**Required:**
- .NET 10 SDK
- Windows 10/11 (x64 recommended)

**Optional:**
- Visual Studio 2022 (17.0+) with ".NET Desktop Development" workload
- JetBrains Rider

### macOS

**Required:**
- .NET 10 SDK
- macOS 12.0 (Monterey) or later

**For iOS Development:**
- Xcode 15+ (from Mac App Store)
- Xcode Command Line Tools:
  ```bash
  xcode-select --install
  ```
- iOS Simulator (installed via Xcode)

**Optional:**
- Visual Studio for Mac
- JetBrains Rider

### Linux

**Required:**
- .NET 10 SDK

**Dependencies (Debian/Ubuntu):**
```bash
sudo apt update
sudo apt install -y libx11-dev libxext-dev libxrandr-dev libxcursor-dev \
  libxi-dev libgl1-mesa-dev libasound2-dev libfontconfig1-dev
```

**Dependencies (Fedora/RHEL):**
```bash
sudo dnf install libX11-devel libXext-devel libXrandr-devel libXcursor-devel \
  libXi-devel mesa-libGL-devel alsa-lib-devel fontconfig-devel
```

**Dependencies (Arch Linux):**
```bash
sudo pacman -S libx11 libxext libxrandr libxcursor libxi mesa alsa-lib fontconfig
```

---

## Building Desktop Application

### Windows

**Build (Debug):**
```cmd
dotnet build Platforms\AgValoniaGPS.Desktop\AgValoniaGPS.Desktop.csproj
```

**Build (Release):**
```cmd
dotnet build Platforms\AgValoniaGPS.Desktop\AgValoniaGPS.Desktop.csproj -c Release
```

**Run:**
```cmd
dotnet run --project Platforms\AgValoniaGPS.Desktop\AgValoniaGPS.Desktop.csproj
```

**Build entire solution:**
```cmd
dotnet build AgValoniaGPS.sln
```

### macOS (Intel)

**Build (Debug):**
```bash
dotnet build Platforms/AgValoniaGPS.Desktop/AgValoniaGPS.Desktop.csproj
```

**Build (Release):**
```bash
dotnet build Platforms/AgValoniaGPS.Desktop/AgValoniaGPS.Desktop.csproj -c Release
```

**Run:**
```bash
dotnet run --project Platforms/AgValoniaGPS.Desktop/AgValoniaGPS.Desktop.csproj
```

**Build for x64 specifically:**
```bash
dotnet build Platforms/AgValoniaGPS.Desktop/AgValoniaGPS.Desktop.csproj -r osx-x64
```

### macOS (Apple Silicon)

**Build (Debug):**
```bash
dotnet build Platforms/AgValoniaGPS.Desktop/AgValoniaGPS.Desktop.csproj
```

**Build (Release):**
```bash
dotnet build Platforms/AgValoniaGPS.Desktop/AgValoniaGPS.Desktop.csproj -c Release
```

**Run:**
```bash
dotnet run --project Platforms/AgValoniaGPS.Desktop/AgValoniaGPS.Desktop.csproj
```

**Build for ARM64 specifically:**
```bash
dotnet build Platforms/AgValoniaGPS.Desktop/AgValoniaGPS.Desktop.csproj -r osx-arm64
```

### Linux

**Build (Debug):**
```bash
dotnet build Platforms/AgValoniaGPS.Desktop/AgValoniaGPS.Desktop.csproj
```

**Build (Release):**
```bash
dotnet build Platforms/AgValoniaGPS.Desktop/AgValoniaGPS.Desktop.csproj -c Release
```

**Run:**
```bash
dotnet run --project Platforms/AgValoniaGPS.Desktop/AgValoniaGPS.Desktop.csproj
```

---

## Building iOS Application

> **Note:** iOS builds require macOS with Xcode installed. iOS development is not supported on Windows or Linux.

### Prerequisites Check

1. Verify Xcode installation:
   ```bash
   xcode-select -p
   ```

2. Accept Xcode license (if not already):
   ```bash
   sudo xcodebuild -license accept
   ```

3. List available simulators:
   ```bash
   xcrun simctl list devices available
   ```

### Building for iOS Simulator

**Apple Silicon Mac (ARM64 simulator):**
```bash
dotnet build Platforms/AgValoniaGPS.iOS/AgValoniaGPS.iOS.csproj \
  -c Debug \
  -f net10.0-ios \
  -r iossimulator-arm64
```

**Intel Mac (x64 simulator):**
```bash
dotnet build Platforms/AgValoniaGPS.iOS/AgValoniaGPS.iOS.csproj \
  -c Debug \
  -f net10.0-ios \
  -r iossimulator-x64
```

### Running on iOS Simulator

**Method 1: Using dotnet build with Run target**
```bash
# Apple Silicon
dotnet build Platforms/AgValoniaGPS.iOS/AgValoniaGPS.iOS.csproj \
  -c Debug \
  -f net10.0-ios \
  -r iossimulator-arm64 \
  -t:Run

# Intel
dotnet build Platforms/AgValoniaGPS.iOS/AgValoniaGPS.iOS.csproj \
  -c Debug \
  -f net10.0-ios \
  -r iossimulator-x64 \
  -t:Run
```

**Method 2: Manual deployment with xcrun**

If the `-t:Run` target doesn't work, use this approach:

1. Boot a simulator:
   ```bash
   # List available simulators
   xcrun simctl list devices available

   # Boot a specific device (e.g., iPhone 15 Pro)
   xcrun simctl boot "iPhone 15 Pro"

   # Or boot any available device
   open -a Simulator
   ```

2. Install the app:
   ```bash
   # Apple Silicon
   xcrun simctl install booted \
     Platforms/AgValoniaGPS.iOS/bin/Debug/net10.0-ios/iossimulator-arm64/AgValoniaGPS.iOS.app

   # Intel
   xcrun simctl install booted \
     Platforms/AgValoniaGPS.iOS/bin/Debug/net10.0-ios/iossimulator-x64/AgValoniaGPS.iOS.app
   ```

3. Launch the app:
   ```bash
   xcrun simctl launch booted com.agvaloniaagps.ios
   ```

### Building for Physical iOS Device

> **Note:** Requires Apple Developer account and provisioning profiles.

```bash
dotnet build Platforms/AgValoniaGPS.iOS/AgValoniaGPS.iOS.csproj \
  -c Release \
  -f net10.0-ios \
  -r ios-arm64
```

---

## Publishing

### Windows Self-Contained Executable

```cmd
dotnet publish Platforms\AgValoniaGPS.Desktop\AgValoniaGPS.Desktop.csproj ^
  -c Release ^
  -r win-x64 ^
  --self-contained true ^
  -o publish\win-x64
```

### macOS App Bundle (Intel)

```bash
dotnet publish Platforms/AgValoniaGPS.Desktop/AgValoniaGPS.Desktop.csproj \
  -c Release \
  -r osx-x64 \
  --self-contained true \
  -o publish/osx-x64
```

### macOS App Bundle (Apple Silicon)

```bash
dotnet publish Platforms/AgValoniaGPS.Desktop/AgValoniaGPS.Desktop.csproj \
  -c Release \
  -r osx-arm64 \
  --self-contained true \
  -o publish/osx-arm64
```

### Linux

```bash
dotnet publish Platforms/AgValoniaGPS.Desktop/AgValoniaGPS.Desktop.csproj \
  -c Release \
  -r linux-x64 \
  --self-contained true \
  -o publish/linux-x64
```

### iOS App Store (IPA)

```bash
dotnet publish Platforms/AgValoniaGPS.iOS/AgValoniaGPS.iOS.csproj \
  -c Release \
  -f net10.0-ios \
  -r ios-arm64
```

---

## Running Tests

Run the guidance algorithm test suite:

```bash
dotnet run --project TestRunner/TestRunner.csproj
```

Expected output:
```
=== TrackGuidanceService Tests ===

Test 1 - AB Line Pure Pursuit: PASS
Test 2 - Curve Pure Pursuit: PASS
Test 3 - AB Line Stanley: PASS
Test 4 - Vehicle On Line: PASS
Test 5 - Track Conversion: PASS

=== Overall: ALL TESTS PASSED ===
```

Tests verify:
- Cross-track error (XTE) calculations
- Steering angle calculations for Pure Pursuit and Stanley algorithms
- Track model conversion between legacy ABLine and unified Track

---

## Troubleshooting

### Common Issues

**"SDK not found" errors**
- Ensure .NET 10 SDK is installed: `dotnet --list-sdks`
- Check PATH environment variable includes dotnet

**NuGet restore failures**
```bash
dotnet nuget locals all --clear
dotnet restore AgValoniaGPS.sln
```

**iOS simulator not found**
```bash
# Reset simulators
xcrun simctl shutdown all
xcrun simctl erase all
```

**iOS build fails with "No valid iOS code signing keys found"**
- For simulator builds, this can be ignored
- For device builds, configure signing in Info.plist or via command line

**Performance issues on Intel Mac with iOS Simulator**
- The iOS Simulator runs ARM64 code via Rosetta 2 translation
- Reduce frame rate in `DrawingContextMapControl.cs`:
  ```csharp
  Interval = TimeSpan.FromMilliseconds(100)  // 10 FPS for better performance
  ```

**Linux: "Unable to load shared library 'libSkiaSharp'"**
```bash
# Install additional dependencies
sudo apt install libfontconfig1
```

### Build Output Locations

| Platform | Configuration | Output Path |
|----------|---------------|-------------|
| Windows | Debug | `Platforms/AgValoniaGPS.Desktop/bin/Debug/net10.0/` |
| Windows | Release | `Platforms/AgValoniaGPS.Desktop/bin/Release/net10.0/` |
| macOS | Debug | `Platforms/AgValoniaGPS.Desktop/bin/Debug/net10.0/` |
| iOS Simulator | Debug | `Platforms/AgValoniaGPS.iOS/bin/Debug/net10.0-ios/iossimulator-arm64/` |

### Getting Help

- Avalonia Documentation: https://docs.avaloniaui.net/
- .NET Documentation: https://docs.microsoft.com/dotnet/
- Report issues: https://github.com/your-repo/AgValoniaGPS3/issues

---

## Project Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| Avalonia | 11.3.9 | Cross-platform UI framework |
| ReactiveUI | 20.1.1 | MVVM framework |
| Microsoft.Extensions.DependencyInjection | 9.0.0 | Dependency injection |
| Newtonsoft.Json | 13.0.3 | JSON serialization |
| System.IO.Ports | 9.0.0 | Serial port communication |
| SkiaSharp | 3.119.1 | Graphics rendering (iOS) |
