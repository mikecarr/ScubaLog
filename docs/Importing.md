# Importing Data

## UDDF
- Settings → “Import UDDF…” and select a `.uddf`/`.xml`.
- Parsing highlights:
  - Depth defaults to meters when unitless.
  - Temperature defaults to Kelvin when unitless and >150, converted to °C/°F for display.
  - Pressure defaults to Pascals when unitless; bar/psi are handled explicitly.
  - Gas/PPO2 per sample are resolved from `<switchmix ref="...">` and mix FO2; PPO2 is derived from FO2 + depth.
  - If no RMV/SAC/ND(T)/TTS fields exist in the UDDF (common), those remain `--`.
- Tanks: `<tankvolume>` is parsed into a `TankUsage` entry (assumed cubic meters when ≤5, otherwise liters).

## Dive Computer (libdivecomputer stub)
- UI: Settings → “Import from dive computer…”, choose manufacturer/model/scope.
- Current status: importer is stubbed; returns no dives until native bindings are wired.
- Native lib:
  - macOS arm64 test build lives at `runtimes/osx-arm64/native/libdivecomputer.dylib`.
  - Add other RIDs under `runtimes/<rid>/native/` (osx-x64, win-x64, linux-x64, etc.).
  - Resolver: `LibDiveComputerInterop` loads the RID-specific binary; actual P/Invoke calls still need to be implemented.
- To add real device sync:
  1) Build per-RID libdivecomputer and place in `runtimes/<rid>/native/`.
  2) Add P/Invoke signatures and wire `LibDiveComputerImporter.ImportAsync`.
  3) Track last-import timestamps/serials for “new only” scopes.

## MacDive
- The desktop app checks for a MacDive SQLite file (see `App.axaml.cs`) and imports dives if present, falling back to demo data otherwise.
