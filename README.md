# ScubaLog

**ScubaLog** is a modern, cross-platform scuba diving logbook application built with **.NET 9** and **Avalonia UI**. It allows divers to view their dive history, analyze dive profiles, and track gas consumption statistics across Desktop, Mobile, and Web platforms.

## Features

*   **Digital Dive Log**: Browse and view your complete history of recorded dives.
*   **Advanced Visualization**: Interactive charts for dive profiles, including:
    *   Depth
    *   Temperature
    *   SAC/RMV (Surface Air Consumption / Respiratory Minute Volume) rates
    *   ppO2 (Partial Pressure of Oxygen)
    *   Air consumption
*   **Data Import**: Built-in support for importing dive data from **MacDive**. Plan is to add Dive Computer integration.
*   **Cross-Platform**: Runs natively on:
    *   Windows, macOS, Linux (Desktop)
    *   Android & iOS (Mobile)
    *   Web Assembly (Browser)

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

2.  **Run the Desktop app**:
    ```bash
    dotnet run --project ScubaLog.Desktop
    ```

## 📄 License
[MIT](LICENSE)