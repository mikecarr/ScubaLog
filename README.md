# ScubaLog

**ScubaLog** is a modern, cross-platform scuba diving logbook application built with **.NET 9** and **Avalonia UI**. It lets you view your dive history, analyze profiles, and track gas/gauge overlays across Desktop, Mobile, and Web.

## Features

* Dive log browsing with grid + profile graph (depth/temperature/pressure overlays, hover detail panel)
* UDDF import (MacDive-export-compatible), with unit-aware display and per-sample gas/PPO2 from mix switches
* Dive detail window (double-click a dive) with summary + tanks tab
* Stubbed “Import from dive computer…” dialog (Shearwater/Oceanic/Hollis) ready for libdivecomputer wiring
* Cross-platform targets: Desktop (Windows/macOS/Linux), Android, iOS, Browser (Wasm)

## Technologies

*   **Framework**: [.NET 9](https://dotnet.microsoft.com/)
*   **UI Framework**: [Avalonia UI 11](https://avaloniaui.net/)
*   **MVVM Pattern**: [CommunityToolkit.Mvvm](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/)
*   **Language**: C# 13

## Project Structure

The solution follows a standard cross-platform architecture:

*   **`ScubaLog`**: The shared library containing UI logic, Views, and ViewModels.
*   **`ScubaLog.Core`**: The business logic layer, containing Models (`Dive`, `DiveSample`, `GasMix`) and Services (`MacDiveImporter`).
*   **`ScubaLog.Desktop`**: The desktop entry point (Windows/macOS/Linux).
*   **`ScubaLog.Android`**: The Android application project.
*   **`ScubaLog.iOS`**: The iOS application project.
*   **`ScubaLog.Browser`**: The WebAssembly (Wasm) project.

## Getting Started

### Prerequisites
*   [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
*   Any IDE with .NET support (JetBrains Rider, Visual Studio, VS Code)

### Build and Run

1.  **Clone the repository**:
    ```bash
    git clone https://github.com/yourusername/ScubaLog.git
    cd ScubaLog
    ```

2.  **Run the Desktop app** (recommended to suppress Avalonia telemetry write issues):
    ```bash
    AVALONIA_NO_ANALYTICS=1 dotnet run --project ScubaLog.Desktop
    ```

3.  **Native lib (macOS arm64)**: if you are testing the libdivecomputer resolver, ensure `ScubaLog/runtimes/osx-arm64/native/libdivecomputer.dylib` exists (already placed for local testing). Other RIDs can be added under `runtimes/<rid>/native/`.

## Importing
* **UDDF**: Settings → “Import UDDF…”, pick a `.uddf`/`.xml`. Gas/PPO2 are derived from `<switchmix>` mixes; temperatures default to Kelvin→°C/°F; pressure defaults to Pa→bar/psi when unitless.
* **Dive computer (stub)**: Settings → “Import from dive computer…”. UI is wired; importer currently returns no dives until libdivecomputer bindings are implemented.

## Project Structure (quick)
* `ScubaLog`: Avalonia UI, views/viewmodels.
* `ScubaLog.Core`: models, services, importers (UDDF, MacDive, dive computer stub).
* Platform heads: `ScubaLog.Desktop`, `ScubaLog.Android`, `ScubaLog.iOS`, `ScubaLog.Browser`.

## 📄 License
[MIT](LICENSE)
