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

### M2 solver core: done
- `native/`:
  - `third_party/amgcl` holds the vendored AMGCL 1.5.0 headers (MIT).
  - `AmgclBridge/amgcl_bridge.cpp` is the elasticity solver: block CG + SA-AMG with
    rigid-body modes, and a selectable smoother (ILU0 default, SPAI0, Chebyshev).
  - `build.cmd` produces `native/bin/AmgclBridge.dll` (git-ignored). Core.csproj copies
    it to every output folder when it exists.
  - **After a fresh clone, run `native\build.cmd` once.** Without it everything falls back
    to the C# PCG and the AMGCL tests skip.
- `Core/Fem`:
  - `CellGrid`: block-sparse cells, 8³ blocks, layer-wise Z boundaries.
  - `Voxelizer`: each bead's footprint is sampled 4 across and 4 per pitch along;
    volume is conserved exactly.
  - `FemMesh`: active cells (φ ≥ 0.05), node numbering, face-connected groups.
    Groups not touching a fixture are dropped and reported. Also gives boundary faces.
  - `HexElement` + `IsotropicMaterial`: 8-node hex, 2×2×2 Gauss.
  - `Assembler`: CSR with 27-bit neighbour masks and 8-colour lock-free parallel assembly.
    Loads are uniform tractions on selected boundary faces; fixtures become identity rows.
  - `Solvers`: `PcgSolver`, `AmgclSolver`, `LinearSolvers.Best()/ByName()`.
  - `StaticAnalysis`: runs it all and returns stresses at cell centres.
  - `LoadCase.ClampAndPush`: a bbox-face stand-in until M5 interfaces.
- CLI:
  - `gcodefem solve <stl> [--rot] [--filament] [--fix zmin] [--force 0,0,-100] [--solver auto|amg|amg-spai0|amg-chebyshev|pcg]`
  - `gcodefem bench [--scales 2,4,6,8]`
- Measured (details in PLAN.md §6 and §7.1):
  - Printed cube: 117k cells, 478k DOF, solved in 13 s, 1.6 GB peak.
  - Cantilever: iterations flat at about 24 up to 1.29M DOF (48 s, 3.9 GB).
  - Bead-resolution limit is about 1.5M DOF, ≈ 11–12 g of PLA.
  - Accuracy: 1.5–2.5 % vs Timoshenko; patch test exact.
- Tests: 38 green (17 s). Slowest is the printed cube under load (13 s).
- Material is still generic isotropic PLA (E 2500, ν 0.35) until M6/M7.

## 2026-10-06 — session 2, on the desktop

### New machine
- i7-8700K, 32 GB, .NET SDK 10.0.203, Bambu Studio 2.08.02.61, VS 2022 Build Tools
  (PLAN.md §2).
- `native\build.cmd` used to hardcode the laptop's VS 2026 Build Tools path. It now finds
  the C++ toolchain through `vswhere`, so it works on both machines.
- Before any change: build clean, 38 tests green, including the Bambu integration tests.
- Bambu CLI checked end to end:
  - A fresh slice of the cube matches the committed fixture in all 7,643 G-code lines.
  - The account's presets synced; a user process + TPU slices correctly as well.
- The Bambu selection on this machine is X1C / "0.12mm Normal" / Bambu TPU for AMS. To
  reproduce the plan's numbers, pass
  `--process "0.20mm Standard @BBL X1C" --filament "Bambu PLA Basic @BBL X1C"`.

### Solver default changed (affects M2 results)
- **Single-precision ILU(0) does not converge on printed parts in bending.** On the
  L-bracket at bead resolution CG stalls at a residual of 2e-4. M2 never saw it because it
  only ran the cube in compression and solid beams.
- `AmgclSolver` now defaults to `Auto`: ILU(0) on a double-precision preconditioner, and
  Gauss–Seidel smoothing if that has not converged after 150 iterations.
  - The fallback is needed: double-precision ILU(0) stalled on an 80 mm bracket at
    2.43 M DOF (no convergence in 9 minutes), where Gauss–Seidel took 274 iterations, 177 s.
- Cost: 15–30 % more time, a third more memory (about 5 KB per DOF). The laptop's
  bead-resolution limit drops from 1.5 M to about 1 M DOF.
- **The solver checks memory first** (`MemoryStatus`: free RAM and free commit). It
  refuses a solve whose estimated need plus a quarter does not fit, and `Auto` goes
  straight to the leaner Gauss–Seidel when only that fits.
  - Reason: running out of memory inside the native solver kills the process (access
    violation). It happened twice while other programs held most of the desktop's memory.
  - The desktop's page file is only 2 GB, so its commit limit is 34 GB. A larger page
    file would give big solves more room.
- The bridge has three more smoothers (`amgcl_bridge_version` is 3): Gauss–Seidel,
  ILU(0) double, ILU(1). **Rebuild the DLL on the laptop** with `native\build.cmd`.
- Measurements in PLAN.md §7.1.

### M3 octree adaptivity: done
- `Core/Fem`:
  - `OctreeForest`: sparse forest over the active cells of a `FemMesh`.
    - Galerkin stiffness per leaf, summed bottom-up from its cells with the real layer
      heights, in parallel.
    - `Refine` splits leaves into their occupied children and keeps the 2:1 balance
      across faces, edges and corners.
  - `OctreeMesh`: a snapshot of the forest as a FE mesh.
    - Nodes, hanging nodes with their masters, `CellLeaf` (which leaf each bead cell is in).
    - `Assemble`: K̂ = Tᵀ K T, each row built by its own node, parallel without locks.
    - `RestrictLoads` / `RestrictFixtures` carry the bead-level load case over.
    - `CellStresses` downscales to every bead cell; `Interpolate` gives the warm start.
  - `AdaptiveAnalysis.Run`: the pass loop (mark → refine → re-solve), with an `onPass`
    callback for progress. `AdaptiveOptions` holds k, buffer, energy share, limits.
  - `OctreeAdvisor`: DOF budget from free RAM, root level from part size.
  - `Assembler.Loads` / `FixedDofs` are public now; `StaticAnalysis.Run` has an overload
    that takes a ready `FemMesh`.
- Defaults chosen from the measurements (PLAN.md §7.2): **k = 2, buffer 1, 90 % of the
  strain energy in fine elements, L = 3**, and L = 0 (no octree) under 250k DOF.
- CLI:
  - `gcodefem adapt <stl> [--level auto|0..5] [--k] [--buffer] [--energy] [--reference]`
    prints every pass as it finishes.
  - `--sweep "k=2;k=3,buffer=2;…"` solves the reference once and compares option sets.
  - `gcodefem bench --adaptive [--verbose]`: solid cantilevers, octree vs full solve.
  - `gcodefem sample bracket <out.stl>`: writes an L-bracket (`MeshFactory.LBracket`).
  - `gcodefem solve … --max-iterations n`.
- Fixed on the way: `solve --fix xmax` (or `ymax`) found no nodes, because the model's
  far X/Y faces fall between node planes. The stand-in load case now uses
  `CellGrid.OccupiedBounds`, the cells' own outer faces.
- New sample: `samples/bracket40.stl` (40 mm legs, 10 wide, 8 thick; 3.5 g).
- Tests: 56 green (about 35 s, and they need about 3 GB of free memory). The octree tests
  cover the exact brick, 2:1 balance, the patch test through hanging nodes, identity with
  the M2 matrix at L = 0, monotone compliance, and adaptive vs full on a solid beam and on
  the printed cube. Two more cover the Gauss–Seidel fallback and the memory check.
- The whole suite was rerun with memory free before the commit: 56 green.

### What M3 found
- Accuracy target met: peak von Mises within 5 % of the full solve on every part tried,
  with 20–67 % of its DOF (table in PLAN.md §7.2). k = 3 brings it within 1 %.
- **Big coarse elements read low on printed structure**: 0.5–0.6 of the real peak at 8³
  cells, 0.75–0.9 at 4³, and 0.87–1.16 at 2³. A result cut short by the DOF budget is not
  to be trusted; the UI has to show the element level at the peak.
- **The octree only pays off on big parts.** Its passes together cost 1.6–2.8× one solve
  of the last mesh. On the 3.5 g bracket and the cube it is no faster than the full solve.
  On an 18 g bracket (2.43 M DOF) it took 29 s and 3.1 GB against 177 s and 8.4 GB, with
  the peak at 0.95 of the full solve.
- Bead resolution gets harder with size on thin printed structure: the solver's iteration
  count grows (92 at 378k DOF, 274 at 2.43 M), which the octree's meshes do not show.
- The root level changes the cost of the first passes, not the result.
- An evenly stressed part (cube in compression) refines almost everywhere.

## 2026-10-07 — session 3, on the desktop

- M3 is committed as `57f5d82` and pushed, on the branch **`m3-octree`**, not on `main`.
  `main` is still at M2. To bring it over: `git switch main`, `git merge --ff-only m3-octree`,
  `git push`.
- The whole suite was rerun with memory free before that commit: 56 green.

### M4 viewer: done
- `Core/Visual` makes everything the viewer draws as plain arrays, so it is tested without
  a window:
  - `Palette`, `ColourScale`, `Legend`: the colours and what they mean (rules in PLAN.md §12).
  - `ToolpathGeometry`: beads as pointed tubes (10 vertices each) or as lines;
    `Indices(layers, features)` picks what is drawn. `ToolpathColours`: colour per segment
    and its legend, for line type, temperature, fan, speed, time, width.
  - `CellSurface`: the cell faces outside air can reach, for a `CellRange`; per-face cell
    and per-vertex grid node, so colours and the deformed shape are applied afterwards.
  - `OctreeWireframe`: outlines of the coarse elements, kept to the shown material.
  - `ResultFields`: per-node displacements, per-cell displacement and σzz.
- `Core/Fem`: `AdaptiveOptions.KeepPassFields` keeps every pass's element level and von
  Mises stress per cell (`AdaptivePass.CellLevels` / `.VonMises`), which is what lets the
  viewer step through the passes. `OctreeMesh.CellLevels()` is new.
- `GcodeFem.App`:
  - `MainViewModel`: settings, Slice and Solve (both off the UI thread), the legend, the pass
    table and the result summary with its warning.
  - `Scene.cs`: `SceneBuilder` turns a `ViewState` into Helix geometry on a background
    thread. Redraws run one at a time and the last request wins. `PrintJob` holds one slice
    with its caches and is replaced as a whole by the next slice.
  - `MainWindow`: one viewport, the four view buttons, a legend laid over the viewport, the
    pass table below it.
  - `StartupOptions`: the command line, including `--screenshot`.
- CLI: `gcodefem sample beam <out.stl> [--length 60] [--width 10] [--height 10]`.
  New sample: `samples/beam60.stl`.
- Tests: 80 green (about 30 s; 24 are new and take under a second together).
- Checked by screenshot, each with
  `--process "0.20mm Standard @BBL X1C" --filament "Bambu PLA Basic @BBL X1C"`:

  | Check | Arguments |
  | --- | --- |
  | Cube toolpaths by line type, cut at layer 60 | `cube20.stl --view toolpaths --layers 1-60` |
  | Cube cells | `cube20.stl --view cells --layers 1-60` |
  | Printed beam as a cantilever: 4 passes, 119k unknowns, 7 s | `beam60.stl --view results --fix xmin --force 0,0,-20 --look 30,60,-35` |
  | The same after the coarse pass | the line above with `--pass 1` |
  | Where it refined | the same with `--colour elementsize` |
  | Bracket standing on edge | `bracket40.stl --view toolpaths --rot 90,0,0 --hide sparse` |
  | 80 mm bracket (`sample bracket --leg 80 --width 20 --height 16`, 574k cells): 5 passes, 554k unknowns, 61 s, 1.8 GB | `bracket80.stl --view results --fix ymax --force 0,0,-30` |

### What M4 found
- **Screenshots need no screen capture.** `RenderTargetBitmap` of the window content
  includes the Helix viewport (it renders through a D3DImage). The content needs an opaque
  background, or the panels come out transparent.
- **The far walls are split over two cell columns.** The cells view shows the +X and +Y
  outer walls with fill 0.4 and 0.6 where the −X and −Y walls have 1.0: the part is 20 mm
  wide and the grid 48 × 0.42 = 20.16 mm, so the last wall straddles a cell boundary. The
  plastic is all there, but a wall two soft cells thick is not the same as one full cell.
  Worth a look when cells get their line direction (M7).
- Outlining every element above bead level paints large areas grey on a real part, and
  outlines through infill voids hide the cut. Hence 4³ cells and up, along material only.
- The load on the 80 mm bracket above twists it as well as bending it. Its last two passes
  took 163 and 126 iterations (554k unknowns); M3's bending load on its 80 mm bracket took
  35 in the last pass (483k unknowns).
- Helix gotchas:
  - `Color`, `Axis` and `MeshGeometry3D` exist in Helix, in WPF and in Core; the view model
    names the ones it means with `using` aliases.
  - A Helix collection is filled fastest with `Resize` and a copy into
    `GetInternalArray()` (`SceneBuilder.Fill`).
  - The big models are not hit-testable (`IsHitTestVisible="False"`), or every mouse click
    would test millions of triangles. Probing (M8) should march through the cell grid.

## Next
- M5 interfaces and loads editor: picking, region growing, glyphs, load cases, project
  save/load (PLAN.md §10, §13). It replaces the stand-in load case in `MainViewModel.SolveAsync`.
  Interfaces live in the part frame, the viewer shows the print frame: the model view is
  the natural place for picking.
- Open items from M4:
  - A running solve cannot be cancelled: `AdaptiveAnalysis.Run` takes no cancellation
    token, and neither does the native solver.
  - The layer range is the only cut. A vertical cut needs `CellRange` bounds in I or J,
    which `CellSurface` and `OctreeWireframe` already take.
  - Every redraw uploads new vertex buffers. Fine at 574k cells (0.1–0.5 s); a part several
    times that size would want index-only updates for the layer sliders.
- Open items from M3:
  - Save the last "fringe" pass by marking with a margin below peak / k (PLAN.md §14).
  - Keep the AMG hierarchy alive in the bridge (create / solve / destroy) so that load
    cases sharing a matrix reuse the setup (PLAN.md §7.3).
  - `FemMesh.Build` still uses dense per-grid-slot arrays (8 bytes per slot of the
    bounding box). Fine up to a few hundred million slots; replace before very large,
    sparse parts.
- Open item for M7: our feed-rate segment times ignore acceleration (≈2× short on solid
  infill). Scale per feature using `result.json` `feature_type_times` when the bond model
  needs layer times.
