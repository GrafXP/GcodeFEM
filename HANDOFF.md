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

## 2026-10-07 — session 4, on the desktop

- M4 is committed as `2824104` and pushed, also on **`m3-octree`**; `main` is still at M2
  (see session 3 for how to bring it over).
- Before any change: 80 tests green.

### M5 interfaces and loads editor: done
- `Core/Geometry`:
  - `MeshTopology` (which triangles share an edge), `FaceRegions.Grow` (a face from one
    triangle, by an angle between neighbours) and `FaceRegions.OnPlane`.
  - `MeshPicker.Pick` (ray → first triangle) and `.Nearest` (point → triangle).
  - `SurfacePatch`: picked triangles as one surface, with area, centroid, mean normal,
    `Components()` and `Spread(n)` (where to put markers). `PatchLocator` finds the patch
    triangle next to a point through a grid of boxes; big triangles are cut up for it.
  - `Cylinder.Fit`: axis, radius, length and hole-or-pin from a patch.
  - `TriangleMesh.GeometryHash()`, `MeshFactory.LBracket(…, holes, holeDiameter)`.
- `Core/Fem`:
  - `LoadCase` has two more lists, `FaceFixtures` and `Tractions`: mounts and loads given by
    the cell faces they reach. The old node and face selectors still work (`ClampAndPush`).
  - `FemMesh.Build(grid, minFill, loadCase)` keeps the cells that hang together with a
    face a mount holds.
  - `Assembler.FreeMotion` names the rigid motion that the mounts leave free;
    `FixedDofs` throws on it. `Assembler.Reach` reports what each mount and load got hold of.
- `Core/Study`: `PartStudy`, `PartInterface`, `StudyLoadCase`, `LoadValue`; `StudyFile`
  (JSON, `*.study.json`); `InterfaceMapper.Map` (interfaces + load case → `LoadCase` for
  the cells) and `.Covers`; `FaceDescription`. PLAN.md §10 has the rules.
- `Core/Visual`: `InterfaceGlyphs` (colours per triangle, cones, rods, arrows as a
  `GlyphMesh`); `CellSurface.FaceColours`.
- `GcodeFem.App`:
  - `MainViewModel.Study.cs`: interfaces, load cases, picking, study open and save.
    `InterfaceItem` keeps a value per load case and edits it as text.
  - `MainWindow`: the "Mounts and loads" panel, Open study / Save study, left click picks
    while "Pick on the model" is on. The stand-in load case is gone from the window.
  - Cells view: "Colour by: interfaces, as the solver takes them".
- CLI: `study new|add|show`, `solve|adapt --study file [--case name]`,
  `sample bracket --holes 2 --hole 5`. `solve` and `adapt` now print how many nodes are
  held and loaded and what each interface reached.
- New samples: `samples/bracket40_holes.stl` and `samples/bracket40_holes.study.json`
  (two bolt holes, a force on the tip, load cases "Pull sideways" and "Push down").
- Tests: 141 green (about 35 s; 61 are new and take a few seconds together).
- Checked, all with PLA Basic / 0.20mm Standard (the study names them):

  | Check | How |
  | --- | --- |
  | Bracket, bolts and tip force, flat: 4 passes, 132k unknowns, 4 s, peak 18.4 MPa at the inner corner | `gcodefem adapt --study samples\bracket40_holes.study.json` |
  | The same in the app, with what each interface held | `GcodeFem.App --study samples\bracket40_holes.study.json --view results` |
  | Second load case: peak 12.1 MPa at the inner corner, bed side | `… adapt --study … --case "Push down"` |
  | Standing on edge: same force, same spot in the part, peak 24.6 MPa | `… adapt --study … --rot 90,0,0` |
  | Interfaces and the force arrow turn with the part | `GcodeFem.App --study … --rot 90,0,0 --select Tip --look -25,-40,-30` |
  | Cell faces by interface | `GcodeFem.App --study … --view cells --colour interfaces --look -25,-40,-30` |
  | Clicking: the top face, then the wall of the first hole (142 triangles) | `GcodeFem.App samples\bracket40_holes.stl --add fixed --click "640,420;757,490"` |
  | A pin alone is refused: "free to slide along Z" | a study with `study add … --kind bolthole --free-along` |
  | The old stand-in, as two interfaces | `GcodeFem.App samples\beam60.stl --view results --fix xmin --force 0,0,-20` |

### After M5: lay a face on the bed
- Martin asked for it: the model has to be placed before anything else, as in the slicer,
  by picking the face that goes on the bed.
- `Orientation.LayFlat(current, normal)` gives the three rotation angles;
  `Orientation.ToEulerDegrees` reads angles back off a rotation; `FaceRegions.FlatNormal`
  is the direction of the flat face around a triangle. PLAN.md §10 says how the angles
  are found and why the face ends up exactly flat.
- App: "Lay a face on the bed" at the top of the Print panel, then a click on the model.
  It is a one-shot: the button comes up again after the click. Scripted: `--lay "x,y"`.
- CLI: `gcodefem study new … --lay x,y,z`.
- Tests: 156 green (15 new, on the angles).
- Checked in the app on the bracket: a click on its long side gives rotation 90, 0, 0 and
  one on its end 0, 90, 0, with the bolt holes and the tip force turned along
  (`GcodeFem.App --study samples\bracket40_holes.study.json --lay "700,560"`).

### After M5: forces in the directions of the view
- Martin found a force hard to line up once the part had been laid on a face: the field
  took it in the model's own directions, while the view and its axes show the print's.
- The value field of a force and of a bearing load now shows and takes X, Y, Z of the
  print as the part is turned by the rotation fields. `InterfaceItem` still keeps the
  force in the part's own directions (`Show(loadCase, partToPrint)`, `ToPart`), and so
  do the study file and the CLI, so the force stays on the part when it is turned:
  the arrow turns with it and the numbers in the field follow.
- Checked on the bracket turned by 90, 0, 0: "Push down", stored as 0, 0, −10, shows as
  0, 10, 0 with its arrow along the view's Y; typing 0, 0, −20 gives an arrow straight
  down and is saved as 0, −20, 0
  (`GcodeFem.App --study … --rot 90,0,0 --select Tip --value "0, 0, -20" --save-study out.study.json`).
- No change in Core; 156 tests green.

### After M5: the middle mouse button pans
- Martin asked for it. One `MouseBinding` on the viewport in `MainWindow.xaml` sends a
  middle click to Helix's pan command.
- Helix's own gestures stay as they are; listed once from the running app:

  | Mouse | Does |
  | --- | --- |
  | right button | turn |
  | middle button (new), or Shift + right | pan |
  | wheel, or Ctrl + right | zoom |
  | Ctrl + Shift + right | zoom to a rectangle |
  | Alt + right | field of view |
  | Ctrl + right double click | set what the camera turns about |
  | Ctrl + left double click, or Ctrl + E | fit the view |
  | Ctrl + middle double click | reset the camera |
  | left button | pick a face, while "Pick on the model" or "Lay a face on the bed" is on |

- Checked by listing the viewport's bindings from the running app: the middle click goes
  to the same command as Shift + right, and nothing else was bound to it. Not checked by
  actually dragging: that needs a hand on the mouse.

### Seen once: the test host crashed in the native solver
- In a window of about ten minutes, 5 of 10 full test runs went wrong: four ended with
  the test host crashing (0xC0000005 inside `AmgclSolver.SolveElasticity`, in
  `VoxelizerTests.The_printed_cube_carries_a_load`, a second or two into the run), and
  in one `OctreeTests.Adaptive_cantilever_finds_the_reference_peak_with_fewer_unknowns`
  got NaN from its reference solve.
- **It did not come back and the cause is not known.** Straight after, 32 full runs were
  clean: 8 each of the M4 commit, the M5 commit and the code as it is now, and 8 more of
  the latter through the solution file, which is how the bad runs had been started. The
  binaries that crashed are the ones that then passed. The crashing test on its own
  passed 6 of 6 right after the bad runs.
- Free commit was 11–12 GB of 34 GB whenever it was looked at, during and after. That is
  not short, but it is only what was seen between runs. An access violation in the
  native solver is what running out of memory there looks like (session 2), and other
  programs were in use on the desktop at the time. This is the likeliest explanation and
  it is not shown.
- If it comes back: note the free commit at that moment, and check what the solver is
  handed (columns in range, no NaN in the matrix or the load) before the native call, so
  that bad input can be ruled out or caught.

### What M5 found
- **An interface follows the print where it steps back from the model.** On the beam the
  first layer's corners are rounded, so two corner cells are missing. The stand-in clamps
  the cells on the bounding plane only (1,273 nodes); the interface also takes the two
  cell faces behind the missing corners (1,281 nodes). The deflection differs by 0.6 %,
  but the peak at the clamp goes from 16.6 to 20.4 MPa and moves one cell in.
  - The peak at a clamp is a singularity either way. M8 has to keep it out of the safety
    factor (PLAN.md §7.4).
- **The steps decide what a mount can do**, not only how accurately it does it: a sliding
  mount on a slanted face cannot slide, and a bolt hole also stops the part from turning
  about the bolt (PLAN.md §10). Exact versions need constraints that couple directions,
  which the octree's "held or not" per direction cannot express.
- The steps of a round hole measure 1.18 times its wall (296 against 251 mm²; the limit
  is 4/π). A force is weighted so that this does not matter, and a pressure's total is
  exact; only the reported area is larger.
- Printed standing on edge, the bracket is 5 % stiffer under the same force and its peak a
  third higher (24.6 against 18.4 MPa). The material is still the same every way, so
  this is the layout of walls and infill alone.
- Helix: `Viewport.UnProject(point)` gives the ray under the mouse and was right on the
  first try (checked by clicking at pixels read off a screenshot). The model stays out of
  Helix's hit test; `MeshPicker` does it on the triangles.
- System.Text.Json writes every number of a vector on its own line in an indented file.
  Not pretty, left as it is.

## 2026-10-08 — session 5, on the desktop

- Everything up to "the middle mouse button pans" is committed and pushed on
  **`m3-octree`** (`18baba9`); `main` is still at M2 (see session 3).
- Before any change: 156 tests green.
- M6 was committed at the start of session 6, **without `materials/tds/`**: Bambu's 45
  data sheets, 20 MB of PDFs that are Bambu's, stay on the desktop, untracked, until
  Martin decides whether they go into a public repository. Without them the material
  files still work, and the test that reads every sheet again does not.

### M6 materials and TDS: done
- Martin widened it during the session: not PLA Basic and PETG HF alone, but **a library of
  every Bambu Lab filament**. It has 45 materials (PLAN.md §9 has what is in it and what
  is missing).
- `Core/Materials`:
  - `Material`, `MaterialValue`, `MaterialDocument`, `MaterialProperty` (21 properties
    with their units and labels), `TypicalValues` (Poisson's ratio by filament type).
    `Material.Isotropic()` is what the solver takes; `Lacking()` says what it is missing;
    `Material.Generic()` is the stand-in of last resort.
  - `MaterialFile`: `*.material.json`. Refuses a value without source or reason, and a
    value in another unit than its property's.
  - `MaterialLibrary`: a folder of material files. `Match(filament, type)` and
    `Match(toolpath)`, `Save`, `Attach` (copies a sheet into `tds/`), `Problems`.
    `DefaultDirectory()` is `GCODEFEM_MATERIALS`, else the nearest `materials` folder with
    material files at or above the program (the repository's), else
    `%APPDATA%\GcodeFem\materials`.
  - `PdfText`: a PDF's words as lines (PdfPig 0.1.16, new in `GcodeFem.Core.csproj`).
  - `TdsImporter` and `BambuTds`: a sheet → a material, with what was not found and what
    the user should look at. `BambuTds.AlignWithStudio` takes name and type from Bambu
    Studio's preset and compares `impact_strength_z`.
- `Core/Slicing` (both found while checking M6 end to end, both older than M6):
  - `PresetLibrary.Merge(…, inheritNil)`: a "nil" in a user's preset is what the preset
    inherits at that place. **"eSun PLA+" did not slice on this machine before**: the CLI
    refused its `nozzle_temperature: ["230", "nil"]`.
  - `BambuSlicer.PlateFor`: a filament that does not print on the Cool Plate gets a plate
    named (`--curr-bed-type`). **PETG, ABS, ASA, PC and PA did not slice before.**
    `SliceResult.Plate` says which. Slices of PLA and TPU keep their cache key.
- `GcodeFem.App`:
  - `MainViewModel`: `MaterialText` / `MaterialIsStandIn` for the print panel; the solve
    takes `materials.Match(job.Toolpath)`; the result's last line names the material, and
    a generic one goes into the warning box. `SolvedJob.Material`.
  - `MaterialsWindow` + `MaterialsViewModel` (`ValueRow`, `MaterialEntry`): the library,
    the form, import, save, discard.
  - `StartupOptions`: `--materials-window [name]`, `--import-tds sheet.pdf`; with
    `--screenshot` the picture is then of that window.
- CLI: `material list|show|import`; `solve` and `adapt` print the material and take
  `--material <name>`, `--materials <folder>`; `--E` and `--nu` still override. `slice`
  prints the plate if one was named. `Text.Ascii` keeps units readable in PowerShell 5.1.
- `IsotropicMaterial.Pla` stays, for the benchmarks, the tests and `Material.Generic()`.
- Tests: 228 green (about 35 s). 72 are new: the template on both layouts and
  its quirks, the file format, matching, and every material file against its sheet
  (`RepositoryMaterialsTests` reads all 45 PDFs again, in 4 s).
- Checked, on the bracket study of M5 unless said otherwise:

  | Check | How |
  | --- | --- |
  | PLA Basic from its sheet: E 2580, peak 18.44 MPa, loaded face 0.945 mm | `gcodefem adapt --study samples\bracket40_holes.study.json` |
  | The numbers used until now give 0.975 mm and the same peak: 0.945 / 0.975 = 2500 / 2580 | the same with `--E 2500 --nu 0.35` |
  | A filament of another vendor slices, and is solved with the stand-in, with a warning | `… --filament "eSun PLA+"` |
  | PETG HF: the textured plate is named, E 1810, ν 0.38, 1.374 mm | `… --filament "Bambu PETG HF @BBL X1C"` |
  | A support filament is refused: "it gives no Young's modulus" | `… --filament "Bambu Support For PLA @BBL X1C"` |
  | The library, and what each of Martin's 22 presets gets | `gcodefem material list` |
  | A sheet read without saving, with the check against Bambu Studio | `gcodefem material import sheet.pdf --dry-run` |
  | The print panel's material line and the result's | `GcodeFem.App --study samples\bracket40_holes.study.json --view results --screenshot out.png` |
  | The same for a filament without data: caution colour, then the warning box | `… --filament "eSun PLA+"`, `… --filament "Generic HIPS @BBL X1C" --view results` |
  | The materials window on PLA Basic | `GcodeFem.App samples\cube20.stl --materials-window "Bambu PLA Basic" --screenshot out.png` |
  | A sheet of each layout read into the form | `GcodeFem.App samples\cube20.stl --materials-window --import-tds sheet.pdf --screenshot out.png` |

- Not checked by hand: clicking through the materials window (editing a cell, Save,
  Discard, the question on closing). The view model's paths ran in the scripted import;
  the buttons themselves need a hand on the mouse.

### How the library was collected, for when Bambu adds a filament
- The store's page of a filament links its current sheet:
  `https://eu.store.bambulab.com/products/<handle>` (`us.store…` redirects there from
  here). The links go to `store.bblcdn.com`, `store.bblcdn.eu` or `cdn.shopify.com`.
  - The handles are not the filament names: `pla-basic-filament`, `abs-filament`,
    `asa-filament`, `pc-filament`, `pla-tough-upgrade` (PLA Tough+), `pla-silk-upgrade`
    (PLA Silk+), `pla-silk-multi-color` (PLA Silk), `support-for-pla-new`,
    `tpu-85a-tpu-90a`; the others are the name in lower case with hyphens.
  - **The store limits requests.** Six pages at once got HTTP 429 after 19 pages. One at
    a time with 12 s in between went through.
- Many links are bare hashes, and the pages link test reports as well. **Which PDF is the
  sheet is told by its content**: `gcodefem material import <pdf> --dry-run` either reads
  it or says that no template knows it.
- Sheets of filaments that are no longer sold were still on Bambu's Shopify CDN under
  guessable names: `https://cdn.shopify.com/s/files/1/0584/7236/6216/files/Bambu_<Name>_Technical_Data_Sheet.pdf`,
  and the same under `…/0645/5876/0155/…`. The two hold different versions of some sheets.
- Then, for each: `gcodefem material import <pdf> --url <link> [--stand-in] [--replace]`.
  The scripts that did the fetching and the choosing were scratch and are not kept; every
  material file records the link and the hash of its sheet.

### What M6 found
- **Bambu's sheets and Bambu Studio's presets are not always of one mind.** The preset's
  `impact_strength_z` is the sheet's number in 33 of 38 cases. Where it is not, the preset
  is behind (PETG Basic: the sheet is V3.0 of January 2026) or holds a sibling's number
  (PLA Tough+ has PLA Basic's 13.8).
- **A new version of a sheet can change the numbers a lot.** PETG Basic: E 1460 MPa in
  V2.0, 2780 in V3.0. PLA Basic: 2680 in V1.0, 2580 in V3.0, and its strength across the
  layers 35 → 31 MPa. The version is in every material's title for that reason.
- Stores hold different versions under one file name: the PLA Basic sheet of one Shopify
  store is V1.0, of the other V3.0. The copy on the CDN of PA6-CF says 5.5 kJ/m² where
  the store's says 15.5.
- Most specimens were annealed; not all. PETG Basic V3.0 does not say so, the TPU 85A and
  90A were only dried, and the two sheets in the new layout speak of annealing as
  something one may do. The notes are in the material files.
- TPU is three orders of magnitude softer than the rest (E 5–10 MPa) except "TPU for AMS"
  (1190 MPa). The desktop's selected filament is that one.
- PdfPig reads these files well. What it cannot give: spaces in some headings, and some
  letters of the newest sheets' side notes.
- The stresses of the bracket do not change with the material (PLAN.md §9). That makes
  M6 look like less than it is: the strengths and the stiffness across the layers are in
  the files, waiting for M7 and M8.

### Open items from M6
- **No template for any vendor but Bambu.** Martin's own filaments run on stand-ins until
  their sheets are typed into the materials window, or get templates of their own.
- No sheet for PLA Dynamic, PLA Impact, PLA Lite, PPA-GF, Support W, Support G.
- In the new layout the note on the specimens has to be read by a person (PLA Pure, PETG
  Matte): their material files say "Infill density 100 %." and no more.
- A value given as a bound ("> 650 %" elongation of the TPUs) is left out.
- The plate chosen in Bambu Studio is not read (PLAN.md §2).
- A study names a filament, not a material. Another material can be forced in the CLI
  (`--material`) but not in the app.
- A stand-in is chosen by a tick per material; two ticks for one type are reported, not
  prevented.
- Gravity can be built now that the density is there (PLAN.md §10); it is not.

## 2026-10-23 — session 6, on the desktop

- M6 is committed as `282e200` and pushed on **`m3-octree`**, without `materials/tds/`
  (see session 5). The two tests that read the sheets now skip where they are missing
  (`SheetsFact`), so a fresh clone stays green.
- Before any change: 228 tests green.
- M7 was committed and pushed at the start of session 7, again without `materials/tds/`.

### M7 print-aware material: done
- `Core/Gcode`:
  - `FeatureClass` (8 classes, the toolpath view's groups) and `FeatureType.Class()`.
    `ToolpathColours.Group` now goes through it.
  - `PrintClock`: segment times stretched per line type to result.json's
    `feature_type_times`, travel to its "Travel". `FromFeedRates` is the parser's clock.
- `Core/Fem`:
  - `CellGrid` keeps, per cell slot of a block with material, volume-weighted sums for the
    in-plane orientation tensor, time, nozzle temperature, fan and volume per line type
    (`Deposit(…, Deposition)`). Read back with `Direction` (`LineDirection`: angle and
    spread), `Feature`, `DepositionTime`, `NozzleTemperature`, `Fan`. `Solid(…, how)` for tests.
  - `Voxelizer.Voxelize(…, clock)` fills them.
  - `OrthotropicMaterial` (E along the line, across it, across the layers, ν; Huber's rule
    for the rest; implicit from `IsotropicMaterial`) and `CellStiffness` (one element matrix
    per layer height × 5° × spread step, `Kind` per cell).
  - `Assembler`, `StaticAnalysis`, `AdaptiveAnalysis`, `OctreeForest` take an
    `OrthotropicMaterial`; the forest sums each cell's own matrix. `OctreeMesh.CellStresses`
    takes no material any more (the forest's `Cells` has it).
  - `LayerBond.cs`: `BondModel` (cooling of the layer below, interface temperature, healing
    above the glass transition), `BondReference` (the specimens' conditions and what of them
    is assumed), `LayerBonds` (factor and gap per cell).
- `Core/Materials`: `Material.Stiffness()` replaces `Isotropic()`: E_xy along and across
  the lines, E_z across the layers. `Material.Bonding(toolpath)`. `TypicalValues.GlassTransition`.
- `Core/Visual`: `CellColours` (direction groups, line-type colours, their legends).
- `GcodeFem.App`: the calibrated clock and the bond at slice time (`PrintJob.Bonds`, again
  when the materials window saves); the cells view colours by direction, line type, bond
  and gap (`--colour direction|feature|bond|gap`); the result's material line names both
  moduli, and a line gives the bond's range and weakest layer.
- CLI: `solve` and `adapt` print the clock's stretches, the cells by line type and the bond;
  `--isotropic` (the M6 way), `--across-line <MPa>`; `gcodefem bars` is the milestone's check.
- Tests: 278 green (about 40 s); 50 are new. They cover the material's directions (Huber,
  turning, crossing), the cells' print data, the bond model, the clock against the cube's
  result.json, patch tests along each direction, the octree's exactness with slanted and
  mixed lines, flat against upright bars of cells, and the same with Bambu Studio's slices.
- Checked:

  | Check | How |
  | --- | --- |
  | Bars 60 × 4 × 1.6 mm, flat / upright: PLA Basic 2367 / 1787 MPa, ratio 1.325; 1.252 once what the cells alone do is taken out, the sheet's 1.252. PETG HF 1.175 and ABS 1.122, the sheets' exactly | `gcodefem bars --process "0.20mm Standard @BBL X1C" --filament "Bambu PLA Basic @BBL X1C"` |
  | Bracket "Pull sideways": flat 18.68 MPa, 0.947 mm (isotropic 18.44, 0.945); on edge 25.49 MPa, 0.987 mm (isotropic 24.64, 0.903) | `gcodefem adapt --study samples\bracket40_holes.study.json [--rot 90,0,0] [--isotropic]` |
  | Cube: 38 iterations, 7.3 s; bond median 0.95 | `gcodefem solve samples\cube20.stl --process … --filament …` |
  | Cells view by direction, line type, bond and gap on the bracket | `GcodeFem.App --study samples\bracket40_holes.study.json --view cells --colour direction --layers 1-20 --screenshot out.png` |
  | The result's material and bond lines, bracket on edge | `GcodeFem.App --study … --rot 90,0,0 --view results --screenshot out.png` |

- Not checked: the skip of the two sheet tests without the PDFs (the PDFs are on this
  desktop, so they ran). It is a check for files in `materials/tds`.

### What M7 found
- **The plan's E₂ = E₃ would contradict the sheets.** Bambu's flat specimens (a dogbone,
  10 mm gauge, 4 mm thick: the sheet's pictures) are what E_xy is measured on, and the
  sheets show no line pattern. With E₂ = E₃ a ±45° solid layer is 10 % softer than E_xy.
  So E₂ = E₁ until coupons say otherwise (PLAN.md §6). It mattered little on the slices
  tried: Bambu Studio laid the solid infill of a 6 mm bar along it, not at 45°.
- **The bars' raw ratio depends on the bar as much as on the material.** The same every
  way, the standing 1.6 mm bar is already 6 % softer than the flat one; of 2 mm bars, whose
  flat print has two layers of sparse infill, the flat one is 5 % softer. Comparing each bar
  with itself solved the same every way takes that out.
- **The cells hold 0.87–0.92 of the sheet's stiffness on a solid bar**: a single wall
  fills 0.88 of its cell, and plastic above a full cell is lost to the cap (4 % of a bar's).
  The sheet's specimens are not solid either.
- **The slicer's clock differs most on the flat bracket**: walls ×1.4–1.7, solid infill
  ×2.2, travel ×2.8. Small layers are slowed to the preset's 4 s, but a 4 × 1.6 mm bar
  standing up still prints a layer a second: the slicer will not go below 20 mm/s.
- **The bond floor for PLA Basic is 0.75** (layer below at the chamber's temperature).
  The flat bracket sits near it almost everywhere (median 0.83), the cube at 0.95.

### Open items from M7
- The bond model's parameters are assumptions (35 °C chamber, 20 s, fan 7× at full) and the
  specimens' layer time and fan are guessed from the preset. M10 calibrates.
- How much stiffer along a line than across it is unknown; E₂ = E₁. M10's 0°/45° coupons.
- The bond between neighbouring beads in a layer (Y_t) is not computed.
- A cell's time is a mean: where lines of one layer cross, the gap to the cell above reads
  short.
- Line-direction glyphs are not drawn; the cells view colours by direction instead.
- φ is still capped at 1, and the far walls of a part still straddle two cell columns
  (M4); both cost a few percent of stiffness.

## 2026-10-23 — session 7, on the desktop

- M7 is committed as `fa6dc6d` and pushed on **`m3-octree`**, again without `materials/tds/`.
- Before any change: 278 tests green.
- **M8 is not committed yet**: Martin asked to commit M7 and then to build M8.

### M8 failure and results: done
- `Core/Fem/Failure.cs`:
  - `FailureMode`: layers pull apart, beads of a layer pull apart, lines break, crushed.
  - `PrintedStrength`: along, across the line, across the layers, compression; the shear
    strengths follow (/ √3).
  - `CellStrength`: per cell of a mesh, its line direction, spread and bond. `LocalStress`
    gives the stress in the plastic in the line frame, over at least half the cell
    (`FillFloor`). `Evaluate` gives every mode's index and Tsai–Wu; `Check` does every cell.
  - `CellFailure`: index, mode, layer-line index and Tsai–Wu per cell; `Weakest`,
    `WeakestLayerLine`, `SafetyFactor`.
- `AdaptiveAnalysis.Run(…, strength)`:
  - It marks cells stressed above peak / k **or** with a safety factor under kf × the lowest
    (`AdaptiveOptions.FailureRefineFactor`, 4).
  - `PeakCell` is the weakest cell when there is a check. New: `AdaptivePass.SafetyFactor` and
    `FailureIndex` (kept per pass), and `AdaptiveResult.Failure`.
- `Material.Strength(out lacking)`: the sheet's strengths, and what is assumed in words.
- `AmgclSolver`:
  - Checks what it hands the native solver (lengths, columns in range, finite numbers).
  - Solves once more from the same start when the answer holds NaN (`SolveStatistics.Retried`;
    see below).
- `Core/Visual`:
  - `FailureColours`: the four bands of the safety factor, the colours of the modes.
  - `ResultMarks`: the arrow to the weakest spot, the outline of a layer line.
  - `CellSurface.Pick`: the face under a click.
- `GcodeFem.App`:
  - The results view colours by safety factor (now the first choice) and by what breaks
    first. A white arrow marks the weakest spot, a white outline the weakest layer line
    (`MarkLines`).
  - A click on a result probes the cell under it, in a box over the viewport.
  - The summary leads with the safety factor and the load it breaks at. The pass table has
    each pass's safety factor.
  - `--colour safetyfactor|failuremode`, `--probe "x,y"`.
- CLI:
  - `solve` and `adapt` print the strengths with what is assumed, the safety factor, the load
    it breaks at, the weakest layer line, the modes near the weakest spot and Tsai–Wu.
  - `adapt` takes `--kf` and `--mark failure|vonmises`; `--reference` and `--sweep` compare
    the safety factor with the full solve.
  - `bars` prints the breaking loads. A pass that had to be solved again says so.
- Tests: 310 green (about 50 s); 32 are new.
  - The modes on stresses worked out by hand: each plane, the bond, shear, compression, a
    plastic as strong every way at seven angles, turned lines, crossing lines, fill and the
    fill floor.
  - Bars of cells flat and upright, linearity, and the octree against the full solve.
  - The material's strengths and what they assume; the colours' lightness; the layer-line
    outline; the arrow's way out; picking.
  - The milestone's bracket through Bambu Studio, flat and on edge.
- Checked:

  | Check | How |
  | --- | --- |
  | Bracket flat: SF 1.583, beads of a layer pull apart at the inner corner, breaks at 31.7 N; full solve 1.569 | `gcodefem adapt --study samples\bracket40_holes.study.json --reference` |
  | Bracket on edge: SF 0.870, layers pull apart under layer 51 (10 mm up, bond 0.80), breaks at 17.4 N; full solve 0.835 | the same with `--rot 90,0,0` |
  | Cube pushed sideways with 50 N: SF 4.261, its first layer line at the clamp; full solve 4.240 | `gcodefem adapt samples\cube20.stl --process "0.20mm Standard @BBL X1C" --filament "Bambu PLA Basic @BBL X1C" --fix zmin --force 50,0,0 --reference` |
  | Bars break at 156 N flat (beads) and 137 N upright (layers): 1.14, the sheet's 35 / 31 = 1.13 | `gcodefem bars --process … --filament …` |
  | The safety-factor bands, the arrow and the layer line, flat and on edge | `GcodeFem.App --study samples\bracket40_holes.study.json [--rot 90,0,0] --view results --colour safetyfactor --look -1,-1,-0.6 --zoom 1.4 --screenshot out.png` |
  | What breaks first, and a probe | the same with `--colour failuremode --probe "929,301"` |

- Not checked by hand: clicking the result with the mouse (the scripted `--probe` goes through
  the same `PickAt`), and the mode view up close (at the zoom above its cells are a few
  pixels).

### What M8 found
- **The stress in a cell's plastic is its stress / fill, but not in slivers.** Without a
  floor, the bracket's weakest spot was a cell a tenth full. The floor at half a cell gives
  the same answer as a quarter (PLAN.md §8 has the table).
- **Refined by the failure index alone, the octree read the safety factor 8–10 % high.**
  Only 10–70 cells are within 2 × the lowest safety factor, against 433 above half the peak
  stress. The stress zone stays (k = 2), and the cells with SF < 4 × the lowest join it. That
  puts the bracket flat at 0.9 %, on edge at 4.1 % and the cube at 0.5 % of the full solve.
- **The first plan's modes let a nearly isotropic plastic hold 1.27 × its strength** at
  30° to its lines: σ₁₁ / X ignored the shear. Now every plane takes the shear along it.
- **The model breaks the bars at 0.7 of the sheet's strength** over their section; the ratio
  flat / upright is the sheet's. Cautious by about that much until M10.
- Tsai–Wu reads 1.3–1.5 × higher than the modes at the bracket's weakest spot: its linear
  terms count compression in one direction as strengthening against tension in another.

### The native solver fails now and then (M5, and again in M8)
- **What was seen**:
  - Between 21:44 and 21:59, 8 runs of the CLI died with an access violation (c0000005)
    inside `AmgclSolver.SolveElasticity` (Windows' event log, Application, event 1026).
  - Twice a pass came back NaN. One was the flat bracket's pass 2: ILU(0) gave NaN after its
    150 iterations, Gauss–Seidel at once. The same system converged in 23 iterations on each
    of the next runs.
  - Afterwards, 27 runs in a row were clean, and a run under full CPU load crawled (no solve
    in 10 minutes) and was stopped. Free commit was about 14 GB each time it was looked at.
  - At 23:20 a full test run crashed the same way (testhost.exe, c0000005, address ending
    7A21 as in two of the CLI's crashes). `dotnet test` then reported "Passed! … Total: 290"
    of 316, without a failure. **Check the total of a test run**, not only that nothing
    failed. The next run passed all 316.
- **Where**: the crash of a run whose modules were logged was in AmgclBridge.dll at offset
  0x3A0A. That is the OpenMP body of `plain_aggregates`' constructor for float matrices,
  reading the diagonal at a column index: `(*dia)[A.col[j]]`.
  - A float hierarchy is what the Gauss–Seidel fallback builds after ILU(0) stalls. The
    on-edge bracket's full solve (185 iterations) always takes that path.
  - Offsets 0x404F (the same loop for double matrices), 0x46F8 (`spai0` setup) and 0x7A21
    (`ilu0` setup) come from crashes whose module base was not logged; they are likely but
    not certain.
  - These are all AMGCL setup code reading its own coarse matrices, and the input passes the
    new check. That points at a corrupt coarse matrix inside AMGCL.
- **Ruled out**: MSVC's OpenMP has dynamic adjustment off (`omp_get_dynamic()` 0), and every
  parallel region got all 12 threads even with every core busy (2000 regions each way). So
  AMGCL's per-thread tables sized by `omp_get_max_threads()` are not short of threads.
- **How the offsets were mapped**: a build of the bridge with `/Zi`, `/DEBUG` and `/MAP`
  (CMake: `-DCMAKE_CXX_FLAGS_RELEASE="/O2 /Ob2 /DNDEBUG /Zi"`,
  `-DCMAKE_SHARED_LINKER_FLAGS_RELEASE="/DEBUG /MAP /OPT:REF /OPT:ICF"`) has the same code as
  `native\bin\AmgclBridge.dll`: the same size of code, the same bytes at every offset looked
  at. So an offset from the event log can be read off that map. No debugger (cdb) is
  installed.
- **What is in place**: the input check and one more solve on NaN. A crash still takes the
  program with it.
- **Next steps, cheapest first**:
  - When it comes back in bursts, rerun with `OMP_NUM_THREADS=1`. No crash then means a race
    in AMGCL's parallel setup.
  - Try the AMGCL release after 1.5.0, or AMGCL's own `amgcl::backend::builtin` with
    `AMGCL_NO_OPENMP` for the setup only.
  - Solve in a child process, so that a crash costs a retry instead of the program.

### After M8: a progress bar
- Martin asked for a progress bar that shows how long a solve takes, and the orientation
  sweeps to come.
- Core:
  - `WorkProgress`: step, time taken, share done, time left.
  - `SolveEstimate`: the progress of an adaptive solve, from what it has done so far
    (PLAN.md §12).
  - `AdaptiveAnalysis.Run(…, onSolving)` reports each pass as it goes to the solver
    (`AdaptiveSolving`).
- App:
  - `MainViewModel.Progress.cs`: `BeginWork(title, source)` / `EndWork()`, a timer that reads
    the source five times a second, and `StepClock` for work that can only tell its time.
  - The status bar has the bar and the times; the status text names the step.
  - Slicing and solving use it. The sweep of M9 is to hand it a source of its own (PLAN.md §11).
  - `--busy-screenshot file.png --busy-after s` takes a picture while the solve runs.
- Tests: 316 green; 6 new, which replay the bracket's passes on a clock of their own. The bar
  never goes back and ends with the solve, the pass and its size are named, and the time left
  is within 2× at three moments.
- Checked: `GcodeFem.App --study samples\bracket40_holes.study.json --rot 90,0,0 --view results --busy-screenshot busy.png --busy-after 4 --screenshot done.png`.
  At 3.4 s: about 30 %, "about 9.0 s left", "Solving: refining after pass 3…"; the solve took
  about 10 s.
- Not checked by hand: a slice that is not in the cache (the bar runs to and fro then; every
  slice here came from the cache).

### Open items from M8
- Assumed, until M10's coupons:
  - Y = Z (bead to bead as across the layers, without the bond).
  - The shear strengths (/ √3) and the compression (the bending strength).
  - The 0.7 of the bars.
- A cell less than half full is taken as half full: measured to be insensitive between a
  quarter and a half, but a rule of thumb.
- The mode view and the outline of the weakest layer line are of the last pass; earlier
  passes show their safety factor and their weakest spot only.
- Staircase peaks on slanted faces are not flagged (PLAN.md §7.4): the weakest spot can sit
  on a step.
- Tsai–Wu is reported, not used.
- `materials/tds/` is still untracked (Bambu's PDFs; Martin to decide).

## 2026-10-24 — session 8, on the desktop

- M8 and the progress bar are committed as `2b6fd12` and pushed on **`m3-octree`**, again without
  `materials/tds/` (Martin asked to "commit all"; the PDFs stay out until he decides about them).
- Before any change: 316 tests green.
- M8b was committed as `dc74f34` and pushed on **`m3-octree`** (Martin: "commit all and push, then
  implement m9"), again without `materials/tds/`. **M9 is not committed yet.**

### M8b results on the print lines: done
- `Core/Fem/Voxelizer.Walk`: the cells of a bead's centre line, cut exactly at the cell boundaries
  of the voxelizer's grid (a step per boundary crossed, with the voxelizer's rule for a point on a
  boundary). Stretches under a thousandth of a cell, left by rounding, go to the next cell.
- `Core/Visual/BeadPieces`: `Whole` (one piece per bead, for the toolpath view) and `Cut` (one per
  cell of the mesh, the rest whole). A stretch outside the solve under half a pitch joins its
  neighbour (a slanted line clipping a cell the voxelizer's samples missed). Also `Displaced`
  (trilinear in the cell), `Shown` (layer range and line types), `Pick` (ray against the bead's
  cross-section, an ellipse) and `Highest` (the weakest piece shown).
- `ToolpathGeometry` builds from pieces: flat where a bead is only cut, pointed where it ends;
  `ends` for the deformed shape keeps the bead's frame. `VerticesPerBead` / `IndicesPerBead` are now
  `…PerPiece`; `Indices(int[] shown)`; `VertexColours(Func<int, Vector4>)`.
- App:
  - `ResultDrawing` and **Draw on** in the Result group: the cells or the print lines (`--on lines`).
    On the lines the line-type switches and thin lines show there too.
  - The results scene computes one colour per cell (`CellColour`) for both drawings. Lines with no
    cell are grey, with a legend entry.
  - The arrow goes to the weakest line shown and the status bar names it. The layer line is
    outlined; the coarse elements are not (they showed between the beads as dots).
  - A click on a bead probes it: the line (type, layer, time on the slicer's clock, nozzle, fan), then
    its cell. `PrintJob.Clock` keeps the calibrated clock for that. `SolvedJob` caches the pieces and
    their undeformed geometry.
- CLI: `adapt` and `solve` print "on the lines": the pieces, and the weakest piece of each line type,
  with a note when no line runs through the middle of the weakest cell. `FemCommands.Problem` now
  carries the toolpath and its placement.
- Tests: 336 green (about 60 s); 20 are new. They cover the walk (cut points both ways, neighbouring
  cells without gaps, corners and boundaries, against the voxelizer's cells), the pieces (cells, tips,
  lines outside the solve, slanted lines, linear displacement, flat and pointed ends, colours, what is
  shown, picking, the highest), and the milestone's bracket through Bambu Studio. That test and M8's
  share one solve of each orientation (`Brackets`).
- Checked:

  | Check | How |
  | --- | --- |
  | Flat: 105,727 pieces, 1.11 per cell, cut in 0.03 s; weakest line outer wall layer 40, SF 1.618; the weakest cell (1.583) has no line through its middle | `gcodefem adapt --study samples\bracket40_holes.study.json` |
  | On edge: 97,661 pieces, 1.03 per cell; weakest line outer wall layer 51, SF 0.870, the weakest cell's | the same with `--rot 90,0,0` |
  | Safety factor on the lines, whole part and from above cut at layer 20 | `GcodeFem.App --study samples\bracket40_holes.study.json --view results --colour safetyfactor --on lines [--layers 1-20 --look -0.2,0.3,-1 --zoom 2.6 --no-elements] --screenshot out.png` |
  | On edge, sparse infill off: the arrow on the outer wall at layer 51; the deformed leg, lines joined; the Result group's new controls | `… --rot 90,0,0 --on lines --hide "sparse infill" --deform 1 --size 1400x1350 …` |
  | What breaks first, and a probe on a wall bead (outer wall, layer 11, 0:04:18, 220 °C, fan 100 %) | `… --colour failuremode --on lines --layers 1-20 … --probe "648,296"` |
  | Thin lines; the cells drawing and the toolpath view as before | `… --colour vonmises --on lines --thin`, `… --colour safetyfactor --probe …`, `… --view toolpaths …` |

- Not checked by hand: clicking a bead with the mouse (the scripted `--probe` goes through the same
  `PickAt`), and switching between the drawings in the window.

### What M8b found
- **The weakest cell need not lie on a line.** Lying flat, the bracket's weakest cell (SF 1.583,
  layer 1, half full) holds the edge of the outer wall's bead, whose middle runs through the next
  cell. On the lines the lowest is 1.618 at the same corner. The cells view keeps the lowest number;
  the CLI says when the two differ (PLAN.md §12).
- **About one piece per cell** (1.03–1.11), not the 1–2 guessed. The bracket draws as tubes in
  150–180 ms; the 80 mm bracket would be about 630k pieces, over the tube budget, so lines.
- **The walk and the voxelizer differ at the corners of cells.** The voxelizer samples 4 points per
  pitch along a bead and 4 across. A slanted line can clip the corner of a cell that none of them
  land in, so that cell may hold none of that bead's plastic. On its own such a stretch showed as a
  grey sliver; it now joins the piece next to it.
- **Element outlines and beads do not mix**: the outlines lie on cell faces, and between the round
  beads they show as rows of dots.

### Open items from M8b
- The deformed lines are built again on each redraw (positions only change): 150 ms on the
  bracket, more on big parts.
- The probe tests every piece shown, one by one: fine at 100k pieces, slow at millions.
- Switching a line type off is a view only; the solve and its numbers include those lines.

### M9 orientation study: done
- `Core/Geometry`:
  - `ConvexHull`: quickhull in doubles. A point within a millionth of the points' extent of a facet
    counts as on it.
  - `LayFaces.Find`: hull planes (facets within 1° merged) and the model's contact with each (its
    triangles within 0.05 mm of the plane, facing its way within 1°), largest first.
- `Core/Study`:
  - `SweepSettings` (smallest contact, count, spins; saved in the study file as `"sweep"`, left out
    until set) and `OrientationCandidates.Find`: the faces laid down by `LayFlat` from a starting
    rotation, spun, with height and overhang area (`OverhangArea`, the process's support threshold).
  - `PrintedPart` (slice → toolpath → clock → cells) and `PartSolution.Solve` (one load case on a
    printed part): the one pipeline that the app, the sweep and the tests use.
  - `OrientationSweep`: screens every candidate in every load case, solves the best 3 in full, ranks
    (`Ranked`), reports progress (`Now`) and stops between steps when cancelled. A candidate that
    fails is noted (`SweepEntry.Error`) and the sweep goes on. `OrientationSweep.Slicing` makes
    the slicer's part.
- `AdaptiveOptions.FinestLevel` (0 by default): no element below 2^level cells. The sweep screens at
  1. `AdaptiveSolving.FinestLevel` lets `SolveEstimate` expect the shorter solve.
- `GlyphMesh.Append`.
- App:
  - `PrintJob` wraps a `PrintedPart`, `SolvedJob` a `PartSolution`. Slice and solve end in
    `ShowPrint` and `ShowSolution`, which opening a compared orientation uses as well.
  - `MainViewModel.Sweep.cs`: `CompareRow` (a candidate or a solve by hand, with what came of it),
    the sweep's settings, the candidates (`RefreshCandidates`, keeping ticks and results of an
    orientation listed again), `StartSweepAsync`, `CancelSweep`, `OpenCompareRow`, clicks on copies.
  - The **Compare** tab (`ViewMode.Compare`): `CompareLayout` puts each copy on a patch of its own;
    `SceneBuilder.Compare` draws copies or their safety-factor maps, numbers (`BillboardText3D`),
    contact rims (`ContactLines`), patches and the selected one in white.
  - The **Orientation sweep** panel on the left, and the table with "Open its result" under the
    compare view. A solve by hand adds a row.
  - Command line: `--view compare`, `--min-contact`, `--count`, `--spins`, `--untick "3,4"`,
    `--click` on copies, `--sweep`, `--row n`, `--open n`, `--compare` (end on the compare view).
- CLI: `gcodefem sweep` (faces, then the sweep and the ranking; `--check` solves all in full and
  compares orders; `--screen-level`, `--best`, `--save`, `--list`). `adapt`/`solve` unchanged;
  `FemCommands.Problem` as in M8b.
- Tests: 351 green, now about 2 min 20 s (the sweeps add a minute); 15 are new. They cover the hull (a box, a sphere, finely meshed flat sides,
  nothing to span), the faces and candidates (cube, bracket, the corner plane, a face 0.03 mm off,
  spins, overhangs), the study file's sweep settings, the sweep on the samples' cube with a stand-in
  slicer (screening, the full solve of the best, a refusing slicer, ranking, progress, cancelling),
  the ranking rules, and the bracket swept through Bambu Studio.
- Checked:

  | Check | How |
  | --- | --- |
  | The bracket's six faces: caps 661 mm², outer sides 320, ends 80; heights, overhangs, rotations | `gcodefem sweep --study samples\bracket40_holes.study.json` |
  | All six in full against the screening at three levels (PLAN.md §11): only screening to 2³ ranks as the full solves | `… --min-contact 50 --check [--screen-level 3\|2\|1]` |
  | The sweep: 55 s from the cache, #1, #2, #6 solved in full (1.58, 1.54, 1.05), #3, #5, #4 screened; 2.1 GB peak with all six in full | `… --min-contact 50` |
  | The preview: copies on patches, numbers, contact rims, interfaces, a copy left out, a row selected | `GcodeFem.App --study samples\bracket40_holes.study.json --view compare --min-contact 50 --untick 2 --row 4 --screenshot out.png` |
  | The panel before a smallest contact is typed: the sweep cannot start | `… --view compare --look -0.4,1,-1.4 --size 1400x1350 …` |
  | The sweep in the app: the bar at 12 s ("about 48 s left", candidate 3 of 6), then the ranked table and the safety-factor maps (69 s) | `… --view compare --min-contact 50 --sweep --busy-screenshot busy.png --busy-after 12 --row 1 …` |
  | A row opened in the results view: #6, rotation 0, 90, 0, its full solve of 'Pull sideways' | `… --sweep --open 6 --colour safetyfactor …` |
  | A solve by hand in the table: tilted 45°, SF 1.12, 'Pull sideways' only | `… --rot 45,0,0 --view results --compare …` |

- Not checked by hand: the mouse on the compare view (a click on a copy ticks it; the scripted
  `--click` goes through the same `PickAt`), double-clicking a row, Stop in the middle of a sweep
  (the cancelling is tested in Core), and the settings typed into the panel while the compare view
  is open.

### What M9 found
- **The coarse pass alone does not rank the bracket's faces right**, nor does one more pass
  (PLAN.md §11 has the table). It reads the upright sides much higher than the ends. Their layers
  lie differently against the 8³ elements, and that is what differs from candidate to candidate.
  Screened down to elements of 2³ cells it ranks all six as the full solves do. That costs 3.5 s a
  candidate against 1.3 s, still a third of a full solve.
- **#3 and #5 are near ties** (screened 1.236 both; in full 0.93 and 0.96). The ranking breaks the
  tie by displacement and print time, so the table puts #3 before #5 where the full solves put #5
  first. Neither is solved in full by default.
- **Slices of the bracket take under a second each** when not cached; the screening's solves are
  the cost.
- **Lying flat as modelled is best** (SF 1.58), flipped is nearly as good (1.54; its layers run the
  same way). Of the standing ones, the bolted leg's end (#6, 1.05) beats the tip's end (#5, 0.96)
  and the outer sides (0.93, 0.87). Why the ends do better than the sides was not looked into.
- **The sweep keeps every candidate's print and governing solution**, so that any row can be
  opened: 2.1 GB peak with the bracket's six solved in full, the solver's own peak included.

### After M9: a sweep of Martin's TPU part
- Martin swept `C:\temp\025856.study.json` (a 27 × 22 × 42 mm part in TPU for AMS, 0.12mm Normal,
  every bead cell, 141 N on a pin) from its rotation 0, 45, 0. Two of five candidates failed:
  "The slicer changed the part's size or orientation". None had been solved in full.
- **Bambu Studio's arrange turns a part to the smallest box around its footprint** unless told not
  to. The two that failed lay on a side, spun 45° on the bed (the shortest turn from 0, 45, 0), and
  came back turned by 31.07°. That turn is in the build item of the exported 3mf; result.json's
  box was the part's box turned (52.2 × 44.7 against 46.4 × 24.2). So the size check was right to
  refuse it. Axis-aligned parts are not turned, which is why M1 never saw it. A spun box (30°)
  was turned straight the same way. With `--arrange` left out, the part is arranged and turned
  all the same; `--arrange 0` leaves it where the STL has it, off the plate.
- Fixed: `--allow-rotations=0` (the option is a flag: `--allow-rotations 0` reads "0" as an input
  file). `BambuSlicer.TurnOnPlate` reads the turn from the 3mf; a slice with a turn is refused, and
  a cached one is sliced again. A half turn keeps the box's size and would pass the size check.
  Slices in the cache from before keep their key; those of axis-aligned parts show no turn.
- **The sweep screened at level 0 too**, because the study solves every bead cell, so each
  screening was a full solve, and the full solves would have repeated them. Screening now uses the
  octree whatever the study's level; a screening that ran at level 0 (a small part) counts as the
  full solve; the best three are chosen by the screening alone (`OrientationSweep.Order`).
- The slicer's warning (floating regions, a floating cantilever: no support in this process) is
  now in the table's State and in `gcodefem sweep`'s lines.
- Swept again: all five slice; #3 and #4, the two that had failed, rank first in full (SF 0.051
  and 0.032), then #1 (0.027); #5 and #2 screened. Screening 12–25 s a candidate; the full solves
  at level 0 about 2 min each, 7 min in all, 4 GB peak.
- **The numbers are the study's, not a fault**: 141 N on a TPU part that starts to break at about
  3 N (SF 0.02–0.05), so the linear displacements (40–67 mm) are far past where they mean anything.
  Its two load cases are the same (141 N along X), which doubles every solve.
- Tests: 355 green (about 2 min 25 s); 4 are new. The 3mf's turn read for three matrices; a box
  spun 30° sliced unturned (it fails without the flag: checked by slicing it the old way), and a
  sweep of that box, each screening the full solve.

### Open items from M9
- A sweep of many candidates on a big part would hold many octrees: the screened ones' could be
  dropped and solved again when opened.
- Mirror-alike faces are swept twice (PLAN.md §11): the bracket's caps differ only in the side the
  first layer is on.
- The candidates are listed from how the part lay when they were first shown; "Show the faces"
  lists them again from the rotation fields. Changing the process does not update the overhangs
  until then.
- The ticks are not saved with the study; the smallest contact, the count and the spins are.
- Labels of the back row can hide behind tall copies in front, from the camera's starting angle.
- Each load case is solved on its own, twice per candidate here (the M5 item below).

## 2026-10-24 — session 9, on the desktop

- Before any change: M9 still not committed; the work below sits on top of it.
- Martin asked why `C:	emp856_sweep.study.json` (the TPU part from the sweep, lying on its side,
  rotation 90, 0, 45) reads SF 0.05 when the load runs along the layer lines, and asked for results to be
  saved with the study for diagnosis.

### Results saved with the study: done
- `Core/Study/ResultFile`: `part.results.zip` beside `part.study.json` (PLAN.md §10). `Save(path, model,
  part, solution, summary)`, `ReadHeader`, `Load` (rebuilds `PrintedPart` and `PartSolution`), `Mismatch`
  (why a saved result is not one of a study, or null), `BesideStudy`.
  - The octree is grown again from the saved leaves (`OctreeMesh` sorts its leaves, so the same leaves
    give the same nodes); stresses and failure come from the saved displacements and the saved material.
  - A slice read back has its G-code in the zip: `SliceResult.Directory` is the zip, and saving it again
    copies the G-code from there.
- `PartSolution.Study`: the study as it was solved. `PrintedPart.Read(slice, toolpath)`.
  `StudyFile.ToJson/FromJson`, `MaterialFile.ToJson/FromJson`.
- App: **Save study** also writes the result on screen, if it is one of the study as saved (else the status
  says why not). **Open study** (`OpenStudyAsync`) shows the saved result when it still fits, with the
  progress bar; else the status says why it was left out. `--saved` shows it from the command line instead
  of slicing and solving.
- CLI: `gcodefem results solve <study>` (slices and solves one load case as the app does, saves beside the
  study or `--out`), `gcodefem results show <zip or study>` (`--top`, `--by`, `--feature`, `--box`, `--cell`,
  `--at`, `--along … --steps`).
- Tests: 362 green (about 2 min 10 s); 7 new (round trip on the samples' cube at level 3 with 3 passes, saving again what was read,
  the names, what counts for a study, the turn a sweep candidate is saved with, damaged files, another model).
- Checked:

  | Check | How |
  | --- | --- |
  | Martin's TPU part: solved, saved (4.2 MB, 0.2 s), read and rebuilt in 0.8 s, SF 0.051 as solved | `gcodefem results solve C:	emp856_sweep.study.json --out …`, `gcodefem results show …` |
  | The app opens a copy of the study with the result beside it: the same summary and picture as Martin's screenshot, 4.6 s with the app's start | `GcodeFem.App --study …856_sweep.study.json --saved --view results --colour safetyfactor --on lines --screenshot out.png` |
  | Saved again from the app (G-code from the first zip), read back the same | `… --saved --view results --save-study …
esaved.study.json` |
  | A study whose load changed: the result is not opened, and the status says why | the same with the force edited in the study |

### What the TPU part on its side says (Martin's question)
- **The beam behaves as expected**: it is a 3 × 8 mm strip (z 7–15 mm on its side) and 141 N pushes across
  its 3 mm, about 20 mm out. By hand that is 237 MPa at the root (SF ≈ 0.09 against 22.4 MPa). Along the
  beam the outer walls carry σ11 along their lines, tension on one side and compression on the other, and
  "lines break" governs: SF 0.107 4 mm from the root, 0.14–0.19 in the middle, where beam theory gives
  ~140 MPa and the cells 112–150.
- **0.05 comes from the junction with the 3 mm plate**, held over its whole back face: the sharp inside
  corner (a stress singularity, so the number falls further with finer cells), the walls turning round
  the corner (lines up to 27° off the stress, so shear across the bead joints: "beads pull apart"), and
  the 8 mm tall beam feeding into a 22 mm tall plate, so the load spreads up and down across the layer
  lines right at the beam's top and bottom (the weakest layer lines are under layers 57 and 129, z 6.8 and
  15.4). Cells there have all three normal stresses large and tensile.
- **Not the assumed ν 0.48**: solved with 0.40 and 0.33, the SF is 0.045 and 0.044, at the same corner.
  ν 0.48 does make the solver slow: ILU(0) stalls and Gauss–Seidel takes 613 iterations (52 s), against 61–98
  iterations (7–11 s) at 0.33–0.40.
- **The whole beam shows "breaks"** because the load is 20 × what the part holds: everything stressed
  above 5 % of the peak is under SF 1. Of the sweep's candidates this side ranked first (0.051), against the
  other side (0.032, the pin's ear then near the bed) and the flange's end (0.027); the mount face down
  (0, 45, 0, the study's start) was not among them.
- The bearing load's applied force is 1.5° off: (102.5, 97.4, 0) N for (100, 100, 0). The cosine pressure is
  summed over the cells' steps and only scaled along its own sum (`Assembler`), so its direction follows
  the steps. Small, but it could be scaled to the force exactly.

### Open items from session 9
- The sweep's candidates are not saved with the study, only the result on screen; neither is the compare
  table.
- A results file holds one load case: the one on screen.
- The bearing load's direction (above).
- `Mismatch` does not see an edited material file: the result keeps the material it was solved with,
  which the summary names.

## Next
- The native solver's crash (above), now that a sweep runs dozens of solves. None was seen in this
  session's sweeps (about 100 solves).
- Open items from M5:
  - A result stays on screen when the interfaces or the load case change after it; the
    summary names the load case it belongs to, but nothing marks it as out of date.
  - One load case is solved at a time. Solving all of them on one matrix needs the AMG
    hierarchy kept alive in the bridge (the M3 item below).
  - Gravity, torque and elastic supports are not there (PLAN.md §10).
  - The model view does not show which face is under the mouse before the click.
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
