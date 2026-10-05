# Handoff — live state

Read [PLAN.md](PLAN.md) for the design. This file only tracks where things stand.

## 2026-10-05 — session 1
- Project created, plan written.
- **Slicer risk retired**: Bambu Studio 2.08.02.61 slices headless. It needs flattened
  presets; see PLAN.md §2 and M1 below. Fixtures in `samples/`:
  - `cube20.stl`: 20 mm cube
  - `cube20_X1C_PLA.gcode`: the cube sliced on X1C / PLA Basic / 0.20mm Standard
  - `cube20_X1C_PLA.result.json`: the slicer's summary
- Stack confirmed by Martin: C# / .NET 10 / WPF / HelixToolkit.Wpf.SharpDX.
- Martin's answers are recorded in PLAN.md §15:
  - interfaces = mount and contact faces
  - printer and process fixed, only the filament changes
  - print temperatures only
  - bead-resolution cells (line width × line width × layer height)
  - AMGCL for the linear solve
  - octree multi-resolution: coarse pass at 8³ cells per element by default (user picks
    4³–32³), then refine only at hotspots (PLAN.md §7.2)
- VS Build Tools 2026 (18.10.2) installed: MSVC 19.51, CMake 4.3.1, OpenMP 2.0.
  - Smoke test passed: CMake + NMake built an OpenMP DLL, and .NET 10 called it via
    P/Invoke (8 threads, exact result).
  - To build native code, call
    `"C:\Program Files (x86)\Microsoft Visual Studio\18\BuildTools\VC\Auxiliary\Build\vcvars64.bat"`
    first. Its "vswhere not recognized" message is harmless.

### M0 scaffold: done
- `GcodeFem.slnx` with Core / App / Cli / Tests on .NET 10. `Directory.Build.props` sets
  nullable, implicit usings and the latest C#.
- Core: `Geometry/` holds `TriangleMesh` (welded, with `Transformed` for reorientation),
  `Box3`, and `Stl` (ASCII + binary read, binary write).
- Tests: 4 green (`dotnet test GcodeFem.slnx`), covering read, outward normals, binary
  round trip and rotation.
- CLI: `dotnet run --project src/GcodeFem.Cli -- info samples/cube20.stl`
- App: Helix SharpDX viewport, Z-up, opens an STL or loads `samples/cube20.stl` on
  start. Rendering checked by screenshot.
- Helix 3.1.2 gotchas:
  - `ZoomExtents(Rect3D)` leaves the camera pointing at nothing, so `MainWindow.FitCamera`
    places the camera by hand.
  - `PerspectiveCamera` clashes with WPF's type of the same name, so it's aliased.
  - In 3.x the types are split across namespaces: `MeshGeometry3D` is
    `HelixToolkit.SharpDX`, collections are `HelixToolkit`, and the WPF elements are
    `HelixToolkit.Wpf.SharpDX`.

### M1 slice & parse: done
- Martin logged into Bambu Studio mid-session. The selection switched to **X1 Carbon 0.4**,
  and 27 filament + 34 process user presets synced.
- **The first spike was wrong.** The CLI ignores `inherits`/`include` and used defaults for
  most settings (200 °C instead of 220 °C, and more). The A1M fixture was sliced that way,
  so it was replaced.
- `Core/Slicing`:
  - `BambuInstallation`: exe, profiles, version; `GCODEFEM_BAMBU_DIR` overrides the path.
  - `BambuSelection`: the current pick from `BambuStudio.conf`.
  - `PresetLibrary`: system + user presets; `Resolve` flattens parent chain → includes →
    own keys; `CompatibleWith` lists presets for a machine.
  - `BambuSlicer`: writes the rotated STL + flattened JSONs, runs the CLI with a timeout,
    tails the log on failure, caches by hash in `%LOCALAPPDATA%\GcodeFem\cache\slices`.
  - `SlicerReport`: reads `result.json`.
  - `Placement`: part ↔ print ↔ bed. It rejects a slicer that changed the bbox size.
- `Core/Gcode`:
  - `GcodeParser` handles G0–G3 (arcs split into chords), G4, G90/91, G92, M82/83,
    M104/109, M106/107 (part fan only), and adds `extruder_offset` back.
  - `Toolpath` / `ExtrusionSegment` / `ToolpathLayer` carry width, height, volume, time,
    layer, feature, nozzle temperature and fan per segment.
  - `ToolpathStatistics`: per-feature totals and per-part value ranges.
- `Core/Geometry`: `Orientation.FromEulerDegrees` (X → Y → Z, exact quarter turns),
  `MeshFactory.Box`, `Stl.ToBinary`.
- CLI:
  - `gcodefem presets [--all]`
  - `gcodefem slice <stl> [--rot x,y,z] [--filament] [--process] [--machine] [--no-cache]`
  - `gcodefem parse <gcode>`
  - Output is ASCII on purpose: PowerShell 5.1 garbles UTF-8.
- Verified:
  - Bead centrelines sit 0.210 mm inside the model on all sides, rotated or not.
  - Part filament is 1206.45 mm, equal to the header total minus the start purge.
  - User presets (eSun PLA+ / 0.20mm Ultra Engineering) slice with their own settings.
- Tests: 27 green, including 4 Bambu integration tests that skip without Bambu Studio.
- Gotcha: don't round-trip UTF-8 sources through PowerShell 5.1 `Get-Content`/`Set-Content`.
  It reads them as ANSI, and doing so garbled `°` in Program.cs once.

## Next
- M2 solver core: block-sparse bead-cell voxelizer (φ only, isotropic), H8 assembly, C#
  PCG + AmgclBridge DLL, cantilever check, DOF/time/memory measured (PLAN.md §13).
- Open item for M7: our feed-rate segment times ignore acceleration (≈2× short on solid
  infill). Scale per feature using `result.json` `feature_type_times` when the bond model
  needs layer times.
