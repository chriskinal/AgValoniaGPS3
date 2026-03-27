# AgValoniaGPS

Cross-platform rewrite of [AgOpenGPS](https://github.com/farmerbriantee/AgOpenGPS) using Avalonia, .NET 10, and C#. Uses the MVVM pattern to separate backend services from the UI.

## Platforms

- Windows (x64)
- macOS (x64, ARM64)
- Linux (x64)
- Android
- iOS

## Tech Stack

- **UI:** Avalonia 11, ReactiveUI
- **Runtime:** .NET 10
- **Architecture:** MVVM with dependency injection
- **Testing:** NUnit, Avalonia.Headless

## Building

Prerequisites: .NET 10 SDK

```bash
# Desktop (Windows/macOS/Linux)
dotnet build Platforms/AgValoniaGPS.Desktop

# Run
dotnet run --project Platforms/AgValoniaGPS.Desktop

# Android
dotnet build Platforms/AgValoniaGPS.Android

# Run tests
dotnet test
```

See [BUILD.md](BUILD.md) for platform-specific setup and publishing instructions.

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for available features, architecture overview, and how to get started.

## License

[GNU General Public License v3.0](LICENSE.md)
