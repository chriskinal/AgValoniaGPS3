# Linux Environment Setup

Guide for setting up the AgValoniaGPS3 development environment on Linux.

## Prerequisites

- **OS**: Debian 11+, Ubuntu 20.04+, Fedora 38+, or similar
- **Architecture**: x64 (ARM64 also supported)
- **Disk space**: ~1 GB for .NET SDK + NuGet packages

## 1. Install .NET 10.0 SDK

### Option A: Microsoft install script (recommended, no root required)

```bash
curl -sSL https://dot.net/v1/dotnet-install.sh -o dotnet-install.sh
chmod +x dotnet-install.sh
./dotnet-install.sh --channel 10.0
```

Add to your shell profile (`~/.bashrc`, `~/.zshrc`, etc.):

```bash
export DOTNET_ROOT="$HOME/.dotnet"
export PATH="$DOTNET_ROOT:$PATH"
```

Then reload:

```bash
source ~/.bashrc  # or source ~/.zshrc
```

### Option B: Package manager

**Debian/Ubuntu:**

```bash
# Add Microsoft package repository
wget https://packages.microsoft.com/config/debian/11/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
rm packages-microsoft-prod.deb

sudo apt-get update
sudo apt-get install -y dotnet-sdk-10.0
```

**Fedora:**

```bash
sudo dnf install dotnet-sdk-10.0
```

### Verify installation

```bash
dotnet --version
# Should output 10.0.x
```

## 2. Install system dependencies

Avalonia UI requires some native libraries for rendering.

**Debian/Ubuntu:**

```bash
sudo apt-get install -y \
  libx11-dev \
  libice-dev \
  libsm-dev \
  libfontconfig1-dev \
  libgbm-dev \
  libdrm-dev
```

**Fedora:**

```bash
sudo dnf install -y \
  libX11-devel \
  libICE-devel \
  libSM-devel \
  fontconfig-devel \
  mesa-libgbm-devel \
  libdrm-devel
```

## 3. Clone and build

```bash
git clone <repository-url> AgValoniaGPS3
cd AgValoniaGPS3

# Restore NuGet packages and build
dotnet build AgValoniaGPS.sln
```

## 4. Run the application

```bash
dotnet run --project Platforms/AgValoniaGPS.Desktop/AgValoniaGPS.Desktop.csproj
```

## 5. Run tests

```bash
dotnet run --project TestRunner/TestRunner.csproj
```

## Troubleshooting

### `dotnet: command not found` after install script

The install script places the SDK in `~/.dotnet`. Ensure your PATH is updated:

```bash
export PATH="$HOME/.dotnet:$PATH"
```

Add this to your shell profile to make it permanent.

### Avalonia rendering issues on headless/SSH sessions

If running without a display (CI, SSH), set:

```bash
export DISPLAY=:0       # if X11 is available
# or for headless testing:
export AVALONIA_SCREEN_SCALE_FACTORS="1"
```

### NuGet restore failures

If behind a corporate proxy:

```bash
dotnet nuget add source https://api.nuget.org/v3/index.json --name nuget.org
```

### ICU / globalization errors

If you see `System.Globalization.CultureNotFoundException`:

```bash
sudo apt-get install -y libicu-dev   # Debian/Ubuntu
sudo dnf install -y libicu-devel     # Fedora
```

Or use invariant globalization:

```bash
export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1
```
