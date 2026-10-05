# Handoff — live state

Read [PLAN.md](PLAN.md) for the design. This file only tracks where things stand.

## 2026-10-05 — session 1
- Project created, plan written.
- **Slicer risk retired**: Bambu Studio 2.08.02.61 slices headless from system preset
  paths in ~4 s (exact command in PLAN.md §2). Fixtures in `samples/`:
  - `cube20.stl`: 20 mm cube
  - `cube20_A1M_PLA.gcode`: the cube sliced on A1 mini / PLA Basic / 0.20mm Standard
  - `cube20_A1M_PLA.result.json`: the slicer's summary
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

## Next
- M1 slice & parse, M2 solver core (uniform bead-resolution reference), then M3 octree
  adaptivity measured against that reference (PLAN.md §13).
