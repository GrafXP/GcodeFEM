# GcodeFem — FEM for parts *as they will be printed*

Predict where an FDM part bends and where it breaks, using the sliced G-code (walls vs
infill, line direction, layer lines, print temperatures) instead of treating the part as a
solid block. Then close the loop: **reorient → reslice → reanalyse → compare**.

C# / .NET 10 / WPF, 3D with HelixToolkit SharpDX, slicing through the installed Bambu
Studio, linear solve through AMGCL.

---

## 1. The loop

```
 Model (STL/3MF)  +  interfaces & loads (stored in PART frame)  +  filament
        │  rotation R  (manual or candidate generator)
        ▼
 Bambu Studio CLI ─────────► plate_1.gcode + result.json
        ▼
 G-code parser ────────────► toolpath segments: feature, width, height, temp, fan, time
        ▼  bed → print frame
 Bead-cell voxelizer ──────► one cell ≈ one piece of one line: fill, direction, feature, bond
        ▼  + material from the filament's TDS
 Octree FEM: coarse pass → refine at hotspots → re-solve   (C# assembles, AMGCL solves)
        ▼
 Deflection · stresses · safety factor · weakest spot ──► compare orientations ──► next R
```

---

## 2. Verified on the two development machines

The laptop was set up on 2026-10-05 (M0–M2) and the desktop on 2026-10-06 (M3). Timings in
this plan say which machine they come from.

| Fact | Value |
| --- | --- |
| Bambu Studio | 2.08.02.61 on both, `C:\Program Files\Bambu Studio\bambu-studio.exe`. A fresh slice of the cube on the desktop matches the laptop's fixture in every G-code line. |
| Active setup | Read live from `BambuStudio.conf`, so it differs per machine. Laptop: X1 Carbon 0.4 nozzle · Bambu PLA Basic @BBL X1C · 0.20mm Standard @BBL X1C. Desktop: X1 Carbon 0.4 nozzle · Bambu TPU for AMS · the user process "0.12mm Normal". Tests and fixtures name their presets, so they don't depend on it. |
| Account presets | Bambu login syncs them to `%APPDATA%\BambuStudio\user\<app.preset_folder>\{filament,process}`: about 25 filaments (mostly eSun, Sunlu, Purefil, addNorth, Prusament, greentec) and 30 processes, all inheriting from system presets |
| .NET SDK | 10.0.201 (laptop), 10.0.203 (desktop) |
| C++ toolchain | Laptop: VS Build Tools 2026 (18.10.2), MSVC 19.51, CMake 4.3.1. Desktop: VS 2022 Build Tools (17.14), MSVC 14.44, with its bundled CMake. `native\build.cmd` finds whichever install has the C++ workload through `vswhere`. OpenMP 2.0 (`/openmp`). |
| Hardware | Laptop: i5-8350U 4 cores · 8 GB RAM · Intel UHD 620 (DX11 OK). Desktop: i7-8700K 6 cores / 12 threads · 32 GB RAM · GTX 1070 Ti. |

**Headless slicing works, but only with flattened presets** (about 1.4 s for a 20 mm cube):

```
bambu-studio.exe --debug 2 --arrange 1 ^
  --load-settings "<dir>\machine.json;<dir>\process.json" ^
  --load-filaments "<dir>\filament.json" ^
  --slice 0 --outputdir <dir> --export-3mf plate.gcode.3mf <dir>\model.stl
```

Outputs: `plate.gcode.3mf`, `plate_1.gcode`, `result.json`. The `result.json` holds the
`return_code`, the placed object's bbox, per-feature print times and the filament grams.
`BambuSlicer` (Core/Slicing) does all of this. The fixture
[samples/cube20_X1C_PLA.gcode](samples/cube20_X1C_PLA.gcode) comes from it.

Gotchas, all handled in code:
- **The CLI does not resolve `inherits` or `include`.** Passing a system preset path
  silently falls back to built-in defaults for every inherited key. The first spike used
  200 °C, 2 mm³/s, 20 % cubic infill and 0.40 mm lines instead of PLA Basic's 220 °C,
  21 mm³/s, 15 % grid and 0.42 mm.
  - `PresetLibrary.Resolve` flattens each preset: the parent chain, then the `include`
    templates (machine start/end/layer-change G-code live in separate "template"
    presets), then the preset's own keys.
  - User presets inherit from system presets and resolve the same way.
- **`extruder_offset`** (X1C: `0x2`) shifts every G-code Y by −2 mm relative to the bed.
  The parser adds it back, so segments are in true bed coordinates. Checked: bead
  centrelines sit exactly 0.210 mm (half a line width) inside the model on all four sides.
- The exe is a GUI-subsystem binary, so `--help` prints nothing. Diagnose through the exit
  code, `result.json` and `%APPDATA%\BambuStudio\log`.
- `--arrange 1` only translates: a 30×10×5 box kept its orientation, centred on the bed.
  `Placement.FromSlice` rejects any change in bbox size.
- `BambuStudio.conf` is JSON followed by an `# MD5 checksum` line. The selection is in
  `presets.machine/process/filaments[0]` and the account folder in `app.preset_folder`.
- The header's "total filament length" includes the start G-code purge line. For the cube,
  1316.16 mm = 109.71 mm purge + 1206.45 mm part, which the parser matches exactly.
- Our feed-rate times are close to the slicer's for walls and sparse infill, but about 2×
  short for short-segment features (solid infill, bottom surface) because acceleration is
  ignored. To revisit for the bond model (M7): scale per feature with the slicer's own
  `feature_type_times`.

**What the G-code gives us**:

| Marker | Use |
| --- | --- |
| `; CHANGE_LAYER`, `; Z_HEIGHT:`, `; LAYER_HEIGHT:` | layer index and thickness → cell Z boundaries |
| `; FEATURE: <type>` (emitted only on change) | line type per segment |
| `; LINE_WIDTH:` | bead width per segment |
| `M104` / `M109 S<t>` | nozzle temperature (first layer often hotter, e.g. eSun PLA+ 240 → 230 °C) |
| `M106 S<0–255>` (no P, or P1) | part-cooling fan. `P2` is the aux fan and `P3` the chamber fan, both ignored. |
| `G1 X Y E F` with `M83` (relative E) | geometry, deposited volume, speed → deposition time |
| `G2/G3 … I J [P]` | arcs. Bambu uses them for spiral z-hop travel; extruding arcs are split into chords. |
| `; MACHINE_START_GCODE_END` / `; MACHINE_END_GCODE_START` | only lines between the first `CHANGE_LAYER` and the end G-code are recorded |
| config block at the file **start** | `line_width`, `layer_height`, `extruder_offset`, `filament_type`, `filament_settings_id`, `sparse_infill_pattern`, `sparse_infill_density`, `wall_loops`, … |

Feature types seen so far: Outer wall, Inner wall, Sparse infill, Internal solid infill,
Top surface, Bottom surface, Bridge, Floating vertical shell, Skirt, Custom. Also mapped:
Overhang wall, Gap infill, Internal bridge, Ironing, Brim, Support, Support interface,
Support transition, Prime tower. Unknown types are reported and kept out of the part.

---

## 3. Tech stack

| Concern | Choice |
| --- | --- |
| Runtime | .NET 10, C# 14, `net10.0-windows` for the app, `net10.0` for everything else |
| UI | WPF + CommunityToolkit.Mvvm 8.4 |
| 3D | **HelixToolkit.Wpf.SharpDX 3.1.2** (DirectX 11): handles millions of toolpath line segments, per-vertex result colours, clipping planes, hit-testing for face picking |
| Linear solver | **AMGCL** (MIT, header-only C++, OpenMP) behind our own small C++ DLL, called through P/Invoke. Fallback: pure C# PCG. See §7. |
| Native build | Visual Studio Build Tools 2026 (C++ workload) + CMake |
| TDS import | PdfPig (PDF text extraction) |
| Tests | xUnit |
| Validation reference | CalculiX `ccx` (external) on small models |

---

## 4. Solution layout

```
GcodeFem/
  GcodeFem.slnx
  src/
    GcodeFem.Core/      net10.0 — everything except UI
      Geometry/         STL/3MF I/O, transforms, face regions
      Slicing/          Bambu CLI runner, preset & filament discovery, result.json, cache
      Gcode/            parser → Toolpath (segments + per-layer data)
      Materials/        material DB, TDS import, property models
      Voxel/            bead-cell voxelizer, per-cell material state
      Fem/              H8 elements, octree + Galerkin coarsening, hanging nodes,
                        assembly, BCs, stress recovery, adaptive loop, ILinearSolver
      Failure/          criteria, safety factor, weakest-point search
      Study/            project file, load cases, orientation runs, comparison
      Visual/           what the viewer draws, as plain arrays: beads, cell faces, element
                        outlines, colours and legends (no UI types, so it is unit-tested)
    GcodeFem.App/       net10.0-windows WPF — views, viewport, view models
    GcodeFem.Cli/       net10.0 console — whole loop headless (batch sweeps, testing)
  native/
    AmgclBridge/        C++ DLL: CSR matrix + node coords in → displacements out
  tests/
    GcodeFem.Tests/     parser fixtures, analytic FEM checks
  materials/            *.material.json  (+ tds/ with the source PDFs)
  samples/              test models and sliced fixtures
```

Rules:
- Core never references WPF. The app is a thin shell over Core, and the CLI can run the
  whole loop without it.
- The native DLL sits behind `ILinearSolver`, so everything still runs (slowly) without it.

---

## 5. Coordinate frames

Getting the frames right once avoids a whole class of bugs later.

- **Part frame**: the model as imported. Interfaces, loads and displayed results live
  here and never move when the part is reoriented.
- **Print frame**: part rotated by R, dropped to z = 0 and placed on the bed by the
  slicer. We apply R ourselves by writing a rotated STL, and never use the slicer's
  auto-orient. M1 checks that `--arrange` only translates, by comparing bbox dimensions.
- **Bed → print frame**: translation = `result.json` bbox min − AABB min of our rotated
  mesh.
- The **FEM is solved in the print frame**, where layers are horizontal and line up with
  the cell grid. Loads are rotated in by R, and results are rotated back for display.
  - Until orientations are compared (M9) the viewer shows the print frame itself: the part
    as it lies on the bed, Z up (§12).

Units: mm, N, MPa (= N/mm²), s, °C everywhere.

---

## 6. Bead cells: turning the print into a material model

### Grid: one cell ≈ one piece of one line
- **XY pitch** = the profile's default `line_width` from the G-code settings block (0.40 on
  the A1M Standard profile, about 0.42 on most Bambu 0.4 mm profiles). Cells are square
  in XY, so a cell is one line width wide and one line width long.
- **Z** = the real layers from the `; Z_HEIGHT:` markers. A different first layer and
  variable layer height come for free; element height simply varies per layer.
- XY origin at the part's bbox min in the print frame.

### Per cell
| Quantity | Source |
| --- | --- |
| occupied, fill fraction φ | deposited bead volume in the cell (width × height × length, cross-checked with E) / cell volume, capped at 1 |
| line direction θ | volume-weighted direction of the segments in the cell (mod 180°), plus spread where lines overlap (wall corners, wall↔infill) |
| feature | volume-weighted: outer wall / inner wall / solid infill / sparse infill / top / bottom / bridge. Support is excluded from the part. |
| deposition time | from feed rates along the G-code → time gap to the cell below and to the neighbouring bead |
| bond factor | nozzle temp, fan, and those time gaps (below) |

### Rasterising diagonal lines
Lines along X or Y fill exactly one row of cells. Diagonal lines (45° infill, gyroid,
curved walls) cross cells at an angle, so they are rasterised **face-connected**: every
cell the bead passes through is included, with its φ < 1. Cells that touched only along an
edge would act as hinges in the FEM and make 45° infill far too soft. The remaining error
is calibrated with 0° vs 45° raster bars (M10).

### Why this resolution pays off
- **Infill is modelled explicitly.** Its real lines, gaps and joints are in the mesh, so no
  homogenisation guesswork (Gibson–Ashby) is needed.
- **Each cell has one line direction**, which gives a clean local material frame.

### Stiffness per cell
Orthotropic in a local frame: 1 = line direction θ, 2 = in-plane across the line,
3 = build Z.
- E₁ = E_xy from the TDS.
- E₃ = E_z from the TDS (or E_xy × ratio if not given).
- E₂ = bead-to-bead, defaulting to E₃. It is raised where the cell's directions spread.
- Everything is scaled by φ.

### Strength per cell
- X_t = σ_xy (TDS)
- Z_t = σ_z (TDS) × bond factor
- Y_t = bead-to-bead, defaulting to Z_t
- shear S estimated
- compression from the TDS bending data

### Print temperatures → bond factor
Print temperatures are the only temperature input; service temperature is out of scope.
- v1: a calibratable table over nozzle temp and fan %, adjusted by the time gap to the cell
  below. A long gap means a colder interface and a weaker bond.
- Later: a cooling + reptation/healing model that turns the interface temperature at
  deposition into bond strength.

### Size and cost (measured in M2)
The printed 20 mm cube (3.7 g of PLA, X1C, 0.20 Standard) gives:
- 116,744 active cells (**≈ 32k cells per gram**)
- 159,274 nodes and 477,822 DOF (**≈ 130k DOF per gram**, about 4 DOF per cell)

Storing cells is cheap: about 4 bytes each. Solving every cell at once is what's
expensive: about 5 KB of RAM per DOF at the peak (§7.1), so full bead resolution stops
around **1 M DOF ≈ 7–8 g of PLA** on the 8 GB laptop and around 4 M DOF ≈ 30 g on the
32 GB desktop.
The octree solve (§7.2) only goes down to bead resolution where stress is high, so the
cost follows the size of the hotspots, not the size of the part.

---

## 7. FEM

### 7.1 Solver: existing engine for the linear algebra, own code for the physics

**Decision:** use an existing, optimised engine where the time goes, which is the linear
solve (≈ 90 % of the runtime). Write in C# the small, project-specific part no engine
offers: per-cell rotated orthotropic material from toolpaths.

| Option | Verdict |
| --- | --- |
| Voxel solvers from bone research: ParOSol, VOX-FE2, Faim, MFEM-based μFE | Built exactly for millions of voxels, but use **isotropic** material per voxel and target Linux/MPI clusters (Faim is commercial). They can't express "stiff along the line, weak across layers", which is the whole point. |
| CalculiX | Supports orthotropy with per-element orientation, and Windows binaries exist. But millions of elements through a text `.inp` file plus its default direct solver won't fit in 8 GB. Used as the **validation reference** on small models. |
| FEniCSx / Kratos | Capable (Kratos even bundles AMGCL), but they bring a full Python stack and would turn the C# app into a launcher. |
| **AMGCL** ✔ | Algebraic multigrid in MIT-licensed, header-only C++, OpenMP-parallel. Smoothed aggregation with **rigid-body near-null-space** for elasticity: in its own benchmark that took 32 iterations where plain AMG took 698. It also offers a mixed-precision preconditioner (float) to halve memory, and AMG copes with irregular infill geometry where geometric multigrid struggles. |

**AMGCL's stock C API** (`lib/amgcl.h`) cannot pass rigid-body modes or block size, and
those are where the 20× comes from. So `native/AmgclBridge` is our own ~150-line C++ DLL:
- **In:** CSR matrix, node coordinates, right-hand side, start guess, tolerance, smoother.
- **Inside:** builds `rigid_body_modes` from the coordinates and solves with CG on 3×3
  double blocks, preconditioned by smoothed aggregation (`as_scalar`).
- **Out:** displacements and iteration info.
- Built by `native\build.cmd` (CMake + MSVC, `/openmp`) into `native\bin`, which is not
  in git. The vendored AMGCL 1.5.0 headers live in `native\third_party`.

**The preconditioner runs in double precision** (changed in M3). M2 used single-precision
blocks to halve its memory, and that worked on the cube in compression and on solid beams.
On a printed bracket in bending it does not: CG stalls at a residual of 2e-4 and never
reaches 1e-8. Octree meshes with hanging nodes suffer the same way (550 iterations where
double precision needs 33). So the default is now:

- ILU(0) smoothing on double blocks. Where it works it needs 20–70 iterations.
- If that has not converged after 150 iterations, carry on with Gauss–Seidel smoothing,
  which cannot break down on a positive definite matrix. This is needed: even in double
  precision ILU(0) stalled on the 2.4 M DOF bracket below.
- Single precision stays available as `--solver amg-ilu0-single`.
- **The solver checks memory before it starts.** A native allocation that fails takes the
  whole process down (seen twice on the desktop while other programs held most of its
  memory). So a solve is refused with a clear message unless its estimated need plus a
  quarter fits in the free RAM and in the free commit (`MemoryStatus`). When ILU(0) does
  not fit but Gauss–Seidel does, the automatic choice goes straight to Gauss–Seidel.

**Measured on the laptop (M2, single precision):**

| Test | Result |
| --- | --- |
| Smoothers on the printed cube (478k DOF) | **ILU(0): 31 iterations, setup 5.6 s, solve 7.3 s**. Chebyshev: 146 iterations, 58 s. SPAI-0: 453 iterations, 79 s. Thin infill walls need the strong smoother. |
| OpenMP | 4 cores give 3.5× over one thread |
| Solid cantilevers, `gcodefem bench` | 23k / 170k / 555k / 1.29M DOF → 0.8 / 4.2 / 18 / 48 s total, iterations flat at about 24, peak RAM 0.1 / 0.7 / 2.2 / 3.9 GB. **AMG setup is the largest single cost.** |
| C# Jacobi-PCG | 1,054 iterations and 91 s at 170k DOF, about 45× slower than AMGCL. Only a fallback. |
| Accuracy | tip deflection 1.5–2.5 % stiffer than Timoshenko (8-node hex with a fully clamped root). The patch test is exact to 7 digits. |

**Measured on the desktop (M3), bead resolution:**

| Test | ILU(0) double (default) | ILU(0) single | Gauss–Seidel |
| --- | --- | --- | --- |
| Printed cube in compression, 478k DOF | 29 iterations, 6.4 s, 2.1 GB | 33 iterations, 5.5 s, 1.6 GB | 82 iterations, 11.7 s, 1.7 GB |
| Printed L-bracket in bending, 378k DOF | 67 iterations, 9.8 s, 1.6 GB | **not converged**: residual 2e-4 after 600 iterations | 92 iterations, 8.8 s, 1.4 GB |
| Solid cantilever, 1.29M DOF | 21 iterations, 20 s, 6.4 GB | 23 iterations, 17 s, 4.8 GB | not measured |
| Printed L-bracket 80 mm in bending, 2.43M DOF | **stalled**: no convergence in 9 minutes, 11.3 GB | not measured | 274 iterations, 177 s, 8.4 GB |

- Double precision costs 15–30 % more time and about a third more memory, **about 5 KB per
  DOF at the peak**; the single-precision smoothers need about 3.6 KB.
- **Thin printed structure in bending gets harder with size**: Gauss–Seidel needs 92
  iterations at 378k DOF and 274 at 2.43M, where solid beams stay at about 22 at any
  size. The octree's meshes do not show this: its last pass on the same 80 mm bracket
  (483k DOF) took 35 iterations.

**C# side (Core/Fem):**
- **Element stiffness:** H8 element stiffness for rotated orthotropic material, cached per
  (layer height × 5° angle bin × feature class), then scaled by φ per cell.
- **Assembly and BCs:** parallel CSR assembly. Cells are processed in 8 parity colours
  that share no nodes, so it needs no locks. Each node has a 27-bit neighbour mask, which
  gives sorted columns and popcount slots. Fixed DOFs become identity rows and columns, so
  the 3×3 block structure survives for AMG.
- **Connectivity:** flood fill from the fixtures drops cells that aren't connected to the
  part. Stray islands would make the matrix singular.
- **Results:** stress recovery at element centres, then the failure criteria (§8).
- **Fallback solver:** a pure C# Jacobi-PCG behind the same `ILinearSolver`, for tests and
  small models.

### 7.2 Octree: coarse pass first, refine only at hotspots

**Structure.** Bead cells are the finest level (level 0). The part is covered by root
blocks of 2ᴸ × 2ᴸ × 2ᴸ cells, each the root of an octree. Only blocks that contain material
exist: a sparse forest, the same idea as p4est and OpenVDB. Every tree node is one hex
element, and the analysis starts with each root block as a single element.

**Coarseness L** is a user setting:

| L | Cells per coarse element | Element size (0.4 × 0.4 × 0.2 mm cells) | Preset |
| --- | --- | --- | --- |
| 2 | 4³ = 64 | 1.6 × 1.6 × 0.8 mm | Accurate |
| **3** | **8³ = 512** | **3.2 × 3.2 × 1.6 mm** | **Balanced (default)** |
| 4 | 16³ = 4 096 | 6.4 × 6.4 × 3.2 mm | Fast |
| 5 | 32³ = 32 768 | 12.8 × 12.8 × 6.4 mm | big parts on small machines |

- L = 3 is the power of two closest to the 10×10×10 Martin asked for. Splitting halves
  each axis, so sizes must be powers of two.
- `OctreeAdvisor` suggests L from free RAM and part size, and the user can override it:
  - The DOF budget is what fits in 60 % of the free physical memory at 5 KB per DOF.
  - It picks the finest of L = 3, 4, 5 whose coarse pass uses at most 1/20 of that budget.
  - **L = 0 for small parts** (under 250k DOF at bead resolution): every bead cell is an
    element and there is one pass. The octree only pays off on big parts (see the
    measurements below).
- Measured: **L changes the cost of the first passes, not the result.** On the printed
  bracket, L = 2, 3 and 4 end on the same mesh and the same numbers.

**Coarse elements get their stiffness from their own bead cells** (Galerkin coarsening):

  K_parent = Σᵢ Pᵢᵀ K_childᵢ Pᵢ  over the 8 children

- Pᵢ is the fixed trilinear interpolation from the parent's corners to child i's corners.
- It's built bottom-up once per orientation, so each coarse element already reflects its
  walls, infill lines and gaps, with no separate homogenisation model.
- P is sparse: each child corner depends on 1, 2, 4 or 8 parent corners. That makes the
  products ~7× cheaper than dense ones. Measured: the coarse pass of a 570k-cell part is
  set up in 1.3 s, Galerkin sums, mesh and assembly together.
- P uses the real layer heights. With a thicker first layer the plane between two
  children is not halfway up their parent, and using ½ there would break rigid rotations.
- A matrix is built when its element becomes a leaf, straight from its cells, and only
  kept while it is one (4.6 KB each). Bead-cell leaves store nothing: they share one
  matrix per layer height, scaled by E φ.
- A fully filled coarse element comes out as the exact brick element of its size (tested).
- Bias: interpolation forces the inside of a coarse element to deform trilinearly, so
  coarse elements come out **stiffer** than reality, and the stresses read off big ones
  come out **much lower** (measured below). The bias is largest where an element contains
  thin members that would bend (an infill line, or a thin rib with one element through
  its thickness). That's acceptable for *finding* hotspots, because the refined passes
  produce the numbers.

**Adaptive loop** (`AdaptiveAnalysis`):

1. **Coarse solve** with every root block as one element. Under a second, even for big
   parts.
2. **Downscale:** evaluate every bead cell's stress at its centre from its element's
   displacement field and the cell's own stiffness. This is cheap and already separates
   walls from infill inside one coarse element.
3. **Mark** elements for refinement:
   - elements holding a cell stressed above peak / k (default **k = 2**; with real failure
     criteria in M8 this becomes SF < k × min SF)
   - always: elements touching mount and load interfaces, where stress concentrates
   - strain energy: at least **90 %** of it must sit in elements of at most 2³ cells, so
     the coarser elements holding the most energy are marked until it does. This is what
     keeps the load paths around a hotspot from being too stiff.
   - one ring of same-size neighbours around everything marked (the buffer)
4. **Refine** marked elements by one level (split into the children that hold material).
   The tree stays 2:1 balanced across faces, edges and corners, so a split can force
   coarser neighbours to split too.
5. **Re-solve.** A node in the middle of a coarser neighbour's edge or face hangs: it
   follows that edge's or face's corners by linear interpolation and is eliminated during
   assembly (K̂ = Tᵀ K T). The previous solution, interpolated, is the starting guess.
6. **Stop** when
   - nothing is left to refine: every cell above peak / k is a bead-resolution element, or
   - the peak sits in bead-resolution cells and the last pass moved it by less than 3 %, or
   - the next pass would exceed the DOF budget, or the pass limit (10) is reached.

**Loads and fixtures stay defined on the bead cells** and are carried to whatever mesh a
pass uses:

- A load on a bead node goes to the corners of the element it lies in, by their shape
  functions (f̂ = Tᵀ Pᵀ f). The total force is preserved exactly.
- A bead node held by a fixture sits on a corner, an edge, a face or the inside of its
  element, and every corner of that part of the element is fixed. That is exact once the
  element is a bead cell, and too stiff rather than too loose before.
- So the octree's displacement fields are always a subset of the bead cells' fields. Its
  compliance f·u can only grow towards the full solution as it refines (tested), and at
  L = 0 it assembles the very same matrix as the M2 mesh (tested).

**Measured against the full bead-resolution solve (M3, desktop).** Defaults: L = 3, k = 2,
buffer 1, energy share 90 %. "Peak" is the highest von Mises stress and "deflection" the
mean displacement of the loaded face, both as a fraction of the full solve. Where the
location was compared (both brackets, the compressed cube) the peak was in the same cell
as in the full solve. Times vary by ±30 % on this machine.

| Part and load (DOF at bead resolution) | Coarse pass: peak | Final: peak | Final: deflection | DOF | Passes | Time vs full |
| --- | --- | --- | --- | --- | --- | --- |
| Printed L-bracket 40 mm, 3.5 g, bending (378k) | 0.54 | 0.97 | 0.98 | 36 % | 4 | 3.6 s vs 10 s |
| Printed cube 20 mm, pushed sideways (478k) | 0.62 | 0.99 | 0.98 | 36 % | 5 | 7 s vs 8 s |
| Printed cube 20 mm, compressed (478k), energy rule off | 0.88 | 1.00 | 1.00 | 67 % | 4 | 6 s vs 7 s |
| Solid cantilever 84 × 13 × 13 mm (1.29M) | 0.70 | 1.00 | 1.00 | 22 % | 4 | 11 s vs 20 s |
| Printed L-bracket 80 mm, 18 g, bending (2.43M) | 0.51 | 0.95 | 0.95 | 20 % | 5 | 29 s vs 177 s |

- The 80 mm bracket also needed 3.1 GB instead of 8.4 GB, and its full solve only
  converged with Gauss–Seidel smoothing (§7.1).
- **What the element size at the hotspot does to the peak** on printed parts: 0.5–0.6 of
  the real value at 8³ cells, 0.75–0.9 at 4³, anywhere from 0.87 to 1.16 at 2³. Only
  bead-resolution cells give a number to rely on.
- **The fine zone has to reach beyond the hotspot.** With every cell above half the peak
  at bead resolution, the still-coarse surroundings are a few percent too stiff and take
  load off the hotspot. That is the remaining 1–5 %.

How the options move that, on the 40 mm bracket:

| Options | Peak vs full | DOF vs full |
| --- | --- | --- |
| k = 2, no buffer, no energy rule | 0.88 | 20 % |
| k = 2, buffer 1, no energy rule | 0.96 | 34 % |
| **k = 2, buffer 1, energy 90 % (default)** | **0.97** | **36 %** |
| k = 2, buffer 2 | 0.99 | 46 % |
| k = 3, buffer 1 | 0.99 | 52 % |
| k = 4, buffer 1 | 1.00 | 58 % |

- k = 2 meets the 5 % target at the lowest cost. k = 3 is the setting for 1 %.
- The energy rule buys about a point of accuracy on the peak and two on the deflection
  for a few percent more DOF.
- **The octree pays off on big parts only.** On the 3–4 g parts it is no faster than the
  full solve; on the 18 g bracket it is 6× faster and uses a third of the memory.

### 7.3 Performance rules from day one
- **Block-sparse storage.** Cell data lives in dense per-block arrays (structure of arrays,
  4 bytes per cell: φ, angle bin, feature, bond), only for blocks with material. The
  octree only decides which elements the FEM uses; it never copies cell data.
- **No per-cell integration.** Leaf stiffness comes from a small cache (layer height × 5°
  angle bin × feature class), scaled by φ.
- **Parallel wherever it's cheap:** voxelizing per layer, Galerkin coarsening per block,
  CSR assembly with a precomputed pattern, stress recovery.
- **One matrix, many load cases.** Load cases with the same mounts share one matrix and
  one AMGCL setup, and each extra load case is just another solve. Scaling a load is free
  because the model is linear.
- **Orientation sweeps screen at the coarse level** and run the adaptive loop only on the
  best few candidates (§11).
- **Caches** per orientation: G-code, cells and coarse stiffness. Changing loads never
  re-slices or re-voxelizes.
- **`gcodefem bench`** records cells, DOF, passes, time and peak memory per run, so speed
  regressions show up.

### 7.4 Limits and validation

Known limits:

- **Staircase surfaces** on slanted faces create artificial stress peaks. Mitigation:
  evaluate the safety factor one cell in from the surface and flag surface peaks.
- **Linear static only** in v1: no plasticity, creep, large deflection, contact or
  buckling.
- **A result that stops short of bead resolution is optimistic.** When the DOF budget ends
  the refinement early, the hotspot still sits in coarse elements and its stress reads
  low (§7.2 has the factors). The pass table shows the element level at the peak, and the
  UI must say so next to the safety factor.

Validation:

- Cantilever vs Euler–Bernoulli (δ = FL³ / 3EI): done in M2.
- Uniform-tension patch test: done, on the uniform mesh (M2) and through hanging nodes (M3).
- Adaptive octree vs full bead resolution on parts small enough for both: done in M3
  (§7.2), as tests and as `gcodefem adapt --reference`.
- A bar printed flat vs upright reproduces the TDS XY/Z ratio.
- CalculiX on the same small mesh.
- Real coupons (M10).

---

## 8. Failure model ("breaking points")

Per cell, in the local frame:
- **Interlayer separation** (the classic FDM failure), Hashin-type:
  (⟨σ₃₃⟩₊ / Z_t)² + (τ₁₃² + τ₂₃²) / S_z²
- **Along the line**: σ₁₁ / X_t (or X_c in compression)
- **Bead-to-bead** (across lines): σ₂₂ / Y_t
- **Tsai–Wu** as an overall cross-check

Safety factor SF = 1 / max(failure index). Because the model is linear, the failure load is
F × SF.

Reported results: min SF, its location, which mode governs, the failure load, and max
deflection. They are shown as an SF heat map (red < 1, amber 1–2, green > 2), a
weakest-spot marker and a colouring by governing mode.

---

## 9. Printer, process, filament, material data

- **Printer + process are fixed** to whatever Bambu Studio has selected, read from
  `%APPDATA%\BambuStudio\BambuStudio.conf` (currently X1 Carbon 0.4 / 0.20mm Standard
  @BBL X1C). Bambu printers behave alike, so they can be changed in settings but are not
  part of the loop.
- **The user picks the filament.** The list is Bambu's system filament presets compatible
  with the printer, plus user presets from
  `%APPDATA%\BambuStudio\user\<id>\filament\`. The preset drives the slice, and with it the
  temperatures and fan that end up in the G-code.
- **Each filament preset maps to a material file** `materials/<name>.material.json`, by
  `filament_settings_id`, falling back to `filament_type`. The file holds:
  - stiffness: E_xy, E_z
  - strength: σ_t xy, σ_t z, elongation at break, bending modulus and strength, impact
  - other: density, nozzle temperature range
  - provenance: every value carries its source (PDF + page, test standard) or is marked
    *assumed*, so measured and guessed numbers never mix silently.
- **TDS import**: PdfPig extracts the text, regex templates per vendor pre-fill the form,
  and the user confirms. Bambu comes first, since their TDS give X-Y and Z values
  separately, which is exactly what §6 needs. Other vendors get manual entry with the PDF
  alongside.
- **To collect:** TDS PDFs for the filaments actually printed. None were found on disk.

---

## 10. Interfaces and loads (stored in part frame)

Interfaces are the faces where the part mounts to or touches other parts.

- **Picking**: a named face group picked in 3D. Clicking a triangle flood-fills by a
  normal-angle tolerance, which catches planar faces and cylinder bores.
- **Mount interfaces**:
  - *fixed*
  - *sliding*: normal direction only
  - *bolt hole*: radial + axial on a cylinder
  - *elastic support* (spring stiffness): later
- **Load interfaces**:
  - *force vector*: total N spread over a face, direction in part frame or along the face
    normal
  - *pressure*: MPa
  - *bearing load* on a hole: cosine-distributed
  - *gravity/acceleration*
  - *torque*: later
- **Mapping to the FEM**: surface triangles are rotated by R into the print frame, and
  each interface takes the boundary nodes of the cells it touches.
- **Load cases**: multiple per study. They are saved in the project JSON as triangle
  indices plus a geometry hash, so a changed STL is detected instead of silently
  mis-mapped.

### What M5 built

**Picking.** A click in the model view sends a ray into the model (`MeshPicker`). The
triangle it meets grows into a face across shared edges for as long as the two triangles
at an edge differ by less than a set angle, 20° by default (`FaceRegions.Grow`). That
takes a flat face up to its edges and the wall of a 32-sided hole all the way round. A
click on a face that is already picked takes it off again, so one interface can hold
several faces (both bolt holes). Slivers, whose computed normal is noise, go with
whatever they lie in.

**A study** (`Core/Study`, saved as `*.study.json`) is the model, how it is printed
(rotation, process, filament, mesh level), its interfaces and its load cases.

- An interface is a name, a kind and the model's triangles that make it up. It belongs to
  the part, so it stays put when the part is turned for printing.
- **The mounts are the same in every load case; each load has a value per load case.**
  That is what will let load cases share one matrix (§7.3). For now the app and the CLI
  solve one load case at a time.
- The file names the model relative to itself, stores faces as runs of triangle numbers
  (`"40-103,180-243"`) and keeps the model's hash. When the hash no longer matches, the
  interfaces keep their names, kinds and values but lose their faces, which have to be
  picked again.

**From the model's faces to the cells** (`InterfaceMapper`). The model's surface runs
through or alongside the outermost cells, which follow it in steps. An interface takes the
free cell faces that lie within 1.25 line widths of one of its triangles and face the same
way as that triangle, even slightly: on a slanted face that is both kinds of step, on a
round hole the steps all the way round. Faces square to the triangle belong to the next
face round the corner.

| Kind | On the cells | What the steps do to it |
| --- | --- | --- |
| Fixed | every corner of its cell faces is held in X, Y and Z | nothing |
| Sliding | each cell face is held along its own normal | exact on a face that lies along the print's directions. On a slanted face the two kinds of step hold both ways, so it cannot slide there. |
| Bolt hole | each cell face of the wall is held along its own normal, and along the hole's axis too if the bolt is done up tight | the steps round a hole face both ways, so the wall is held across the hole in every direction and cannot turn about the bolt either. A hole that is slanted in the print frame is held every way. Without "along the hole" it is a pin, and the part can slide along it. |
| Force | the force is shared out over the cell faces, each weighted by how squarely it faces the model's surface | the weights add up to the face's own area, so the traction is even over the true face and the total is exact |
| Pressure | on every cell face along its own normal | the steps' forces add up to exactly the pressure's force on the stepped surface |
| Bearing load | pressure on the side of the wall that the pin pushes against, falling off as the cosine to nothing at the sides | the total is the force across the hole; what there is of it along the hole is left out, since a pin cannot push that way |

- The axis and size of a hole come from the picked triangles themselves (`Cylinder.Fit`):
  the axis is the direction their normals have nothing of, the radius a circle through
  their corners seen along it. Two holes in one interface are fitted one by one.
- A force is fixed to the part: it is kept in the model's own directions, in the study
  file and in the CLI, and turned by R on its way to the solver. Or it is one number
  pushing onto the face along its normal.
  - **In the app a force is shown and typed in the directions of the view**: X, Y, Z of
    the print as the part is turned now, Z up from the bed, which is what the axes in the
    corner show. Typed in the model's own directions it could not be lined up with
    anything on screen once the part had been laid on a face. Turning the part afterwards
    turns the force with it, and the numbers in the field change to match.
- **Mounts that do not hold the part are refused before the solve**, with the motion that
  is left: "free to slide along Z" for a pin alone, "free to turn about an axis along Y"
  for a hinge. A rigid motion is six numbers, and every held direction at a node rules
  some of them out; if the 6 × 6 sum of these has a zero eigenvalue, something is left.
  Without this check the solver would simply not converge.
- Whatever an interface does not reach is named: an interface with no faces, a load case
  with no load, a load whose faces have no printed cell near them.

**In the app.** The left panel lists the interfaces (blue: held, orange: loaded), with an
editor for the selected one and the load cases below. The model view tints their faces
and marks them: cones standing on a mount's face, a rod along the axis of a bolt hole, an
arrow for a force, small arrows for a pressure, an arrow at each mouth of a hole for a
bearing load. The cells view can colour the cell faces by the interface that takes them,
which shows what the solver will work with. After a solve the summary says how many cell
faces each interface held or loaded, and with what force.

**In the CLI.** `gcodefem study new|add|show` builds a study by naming a point on each
face, and `solve` and `adapt` take `--study file [--case name]`. The whole loop still
runs without the app.

**Placing the model first.** "Lay a face on the bed" in the Print panel works like the
slicer's: click a face of the model and the model turns until that face points straight
down. Of all the turns that do that, it takes the shortest from how the model lies now, so
a part that is nearly right is only tipped, not spun round. The three rotation fields
show the result and can still be typed into. The interfaces are faces of the part, so
they turn with it, and it does not matter whether the model is placed before or after
they are picked. In the CLI it is `study new … --lay x,y,z`, with a point on the face.

- X and Y of the rotation alone decide whether the face lies flat, and they follow from
  the face's normal directly: X = atan2(−n.y, −n.z), Y = atan2(n.x, √(n.y² + n.z²)). Only Z,
  the spin on the bed, is read off the matrix of the shortest turn. So flat is exact to
  the rounding of the fields (a ten-thousandth of a degree), also near Y = ±90°, where
  angles taken from a matrix lose their accuracy.
- The face is the flat face around the clicked triangle, its normal averaged by area.

Left for later: elastic supports, torque, gravity (it needs a density, which comes with
the materials in M6), and solving several load cases on one matrix.

---

## 11. Orientation loop

- **Manual**: rotate in the viewport (gizmo, "lay this face on the bed", 90° snaps), then
  Slice & Solve. Each result is added to a comparison table.
- **Automatic**:
  - Candidates: lay-flat on each large planar face, plus Fibonacci-sphere samples.
  - Prefilter: overhang area.
  - Screen every candidate with a coarse pass only (slice + coarse solve, seconds each),
    then run the full adaptive loop on the best 3.
  - Rank by min SF, then max deflection, print time and filament (from `result.json`),
    then support volume.
- **Compare view**: table, side-by-side SF maps, and the weakest spot of each candidate.
- **Cache key**: hash(mesh, R, filament, presets, overrides). Reruns reuse G-code and cells.
- **Stretch goal**: also compare filaments, and sweep slicer settings (wall loops, infill %
  and pattern) through process overrides.

---

## 12. Visualisation (app)

Tabs: **Model & Interfaces · Toolpaths · Cells · Results · Compare**

- Toolpaths: colour by feature, temperature, fan, deposition time or width, with a
  layer-range slider.
- Cells: φ, line-direction glyphs, feature, bond factor. Instanced boxes with a layer
  slider.
- Results: displacement (deformed shape × scale), von Mises, interlayer σ_z, SF. Includes a
  clip plane, a legend and click-to-probe values.
- Octree overlay: element size per region and refinement pass, so it's visible where the
  solver looked closely.

### What M4 built

One viewport with four views (Model · Toolpaths · Cells · Results) and a layer range that
applies to the last three. Everything is drawn in the print frame, on a 10 mm grid at the
height of the bed.

| View | Shows | Notes |
| --- | --- | --- |
| Model | the STL, turned by the rotation fields as they are typed | |
| Toolpaths | every bead at its real width and height, coloured by line type, nozzle temperature, fan, speed, time or width | Line types can be switched off one by one. Above 400k beads, or on request, plain lines instead. |
| Cells | the bead cells, coloured by fill φ | Line direction, feature and bond factor come with M7. |
| Results | von Mises, displacement, stress across the layers (σzz) or element size, on the deformed shape | Selecting a row of the pass table shows the mesh and the stresses as they were after that pass. |

- The load case was still the stand-in from the CLI: clamp one side of the printed part,
  spread a force over the opposite side. Interfaces replaced it in M5 (§10); `--fix` and
  `--force` on the app's command line now set up the same thing as two interfaces.
- **Only faces that outside air can reach are drawn.** A flood fill from outside the shown
  range finds them. A whole part is then little more than its skin, and a layer range that
  cuts it open shows the infill behind the cut. Measured on the 80 mm bracket (574k cells):
  318k faces for layers 1–50, built in 0.1 s; the results view with deformation and
  outlines in 0.5 s.
- **A bead is a tube with a diamond cross-section**, shaded round, with pointed ends that
  reach half a line width beyond the nozzle's path. Ten vertices per bead; neighbouring
  lines stay visibly apart and corners close.
- **Element outlines** are drawn for elements of 4³ cells and up, and only along material.
  Outlines of 2³-cell elements lie too close together and grey out the part; lines through
  infill voids hide what is behind them.
- **The result must say when it is optimistic** (§7.4): if the peak still sits in a coarse
  element, or a pass did not converge, a warning stands next to the numbers.
- The top of the colour scale can be pulled below the peak, because the peak is usually one
  sharp corner at the clamp and would leave the rest of the part dark.

**Colours** follow one rule set, checked with a palette validator for the dark viewport:

- A magnitude (stress, displacement, fill, speed) gets one hue, dark for low and light for
  high. Lightness carries the value, so it reads without colour vision. No rainbow: its
  lightness goes up and down and invents boundaries that are not in the data.
- A signed value (σzz) gets blue for negative, red for positive and grey at zero.
- Line types get eight fixed hues in a fixed order, plus grey for lines that are not part
  of the model. Both bridge types share one hue and the rare part features share the last.
  Eight hues side by side cannot all be told apart (orange and red, and magenta and aqua
  for colour-blind readers), which is why single line types can be switched off.
- The lighting adds no colour of its own: a white light at the camera, a weaker one from
  above, and no ambient or specular share. A face is dimmed by its angle to the light,
  never tinted, and whatever is in view is lit.

**Scripted runs.** The app takes the whole job on its command line and can save a
screenshot and exit, which is how the views were checked:

```text
GcodeFem.App model.stl --view results --fix xmin --force 0,0,-20 --screenshot out.png
```

`StartupOptions` lists the options (rotation, presets, view, colouring, layer range, pass,
camera direction). Since M5 it also opens a study (`--study`), adds or selects an
interface, sets its value, and clicks on the model at pixels read off an earlier
screenshot (`--click "640,420;757,490"`), which goes through the same code as the mouse.

Still to come: line-direction glyphs, feature and bond colouring of cells (M7), safety
factor, probe and weakest-spot marker (M8), cuts other than by layer, the part frame and
the Compare tab (M9), and cancelling a running solve.

---

## 13. Milestones

Each milestone ends with something runnable. The slicer risk is retired (§2). The biggest
remaining unknown is **whether bead-resolution solves are fast enough on this laptop**, so
that comes early.

| # | Milestone | Done when |
| --- | --- | --- |
| M0 | **Scaffold**: slnx, Core / App / Cli / Tests, Helix viewport shows an STL. Install the C++ build tools. | `dotnet build` + `dotnet test` green, window shows `samples/cube20.stl` |
| M1 | **Slice & parse**: read the current Bambu selection, filament list, CLI runner with timeout and log capture, rotated-STL writer, `result.json` → placement, G-code parser incl. per-segment deposition time | `gcodefem slice model.stl --rot 0,90,0 --filament "eSun PLA+"` prints layer and feature stats; parser tests run on the cube fixture. **Done 2026-10-05.** |
| M2 | **Solver core**: block-sparse bead-cell voxelizer (φ only, isotropic), H8 assembly, C# PCG + AmgclBridge DLL, uniform bead-resolution solve | cantilever within a few % of beam theory; DOF vs time vs memory measured; this solve becomes the **reference** for M3. **Done 2026-10-05:** cantilever 1.5–2.5 % of Timoshenko, measurements in §6 and §7.1. |
| M3 | **Octree adaptivity**: forest of root blocks, Galerkin coarsening, 2:1 balance, hanging-node constraints, downscaling, mark/refine/re-solve loop, RAM-based L suggestion | adaptive min SF (isotropic von Mises for now) within 5 % of the M2 reference using a fraction of its DOF; time per pass and coarse-level bias measured; defaults for L and k chosen. **Done 2026-10-06:** peak within 5 % on every part tried, with 20–67 % of the DOF; k = 2 and L = 3 kept, L = 0 for small parts; measurements in §7.2. The solver had to move to double precision on the way (§7.1). |
| M4 | **Viewer**: model, toolpaths with layer slider, cells, octree overlay, result colouring | cube toolpaths coloured by feature; cantilever result and refinement shown. **Done 2026-10-07:** both shown, plus the cells view, the deformed shape and a pass-by-pass view of the refinement; details in §12. |
| M5 | **Interfaces & loads editor**: picking, region growing, glyphs, load cases, project save/load | define a bracket's bolt holes + force in the UI and solve. **Done 2026-10-07:** the bracket with two bolt holes and a force on its tip is set up by clicking, saved as a study and solved, flat and standing on edge; details in §10. |
| M6 | **Materials & TDS**: JSON DB, Bambu TDS import, filament → material mapping | PLA Basic + PETG HF generated from their PDFs with sources |
| M7 | **Print-aware material**: line directions, orthotropy, feature classes, bond factor from temps and time gaps | flat vs upright bar shows the TDS XY/Z ratio |
| M8 | **Failure & results**: criteria, SF, governing mode, weakest spot, deformed shape, probe. The refinement marking switches from von Mises to the real failure index. | bracket shows its weakest layer line and failure load |
| M9 | **Orientation study**: manual compare + automatic sweep (coarse screening, refine the best 3) and ranking | ranked table of orientations for a bracket |
| M10 | **Calibrate**: print and break coupons (flat/upright bar, 0°/45° raster, hook) to tune the bond and diagonal factors; CalculiX cross-check | predicted vs measured failure loads recorded in `docs/calibration.md` |

---

## 14. Risks and honest limits

- **Absolute numbers will be rough.** FDM strength scatters ±20 % even in the TDS. What the
  tool is good at is **ranking orientations and finding the weak spot**, and that is all the
  loop needs. Real coupons in M10 tighten the numbers.
- **The coarse pass can miss a hotspot.** Coarse elements are too stiff where they hold
  thin members that bend, so a real hotspot there can look harmless. Measured in M3: a
  hotspot inside an 8³ element reads at 0.5–0.6 of its real stress on printed parts. Two
  hotspots in similar structure are biased alike and keep their order, but a thin rib
  next to a massive region may not. Mitigations:
  - the threshold k: everything above half the peak is refined, every pass
  - interfaces are always refined
  - 90 % of the strain energy must sit in fine elements
  - per-cell downscaled stresses
  - small parts are solved at bead resolution outright (L = 0), and `--level 0` does the
    same for any part that fits in memory
- **Face-connected rasterisation of diagonal lines** adds a little extra material and
  stiffness. φ-scaling and 0°/45° coupons correct it.
- **Native dependency:** the AMGCL bridge needs MSVC to build. The C# fallback keeps
  everything working without it, but it is about 45× slower (M2). In practice it only
  handles tests and small parts.
- **The octree's passes are not free.** Each pass changes the matrix, so the AMG setup
  (20–40 % of a solve) can't be reused, and the passes together cost 1.6–2.8× one solve
  of the last mesh. The octree wins only when that mesh is well under the full one, which
  is why small parts skip it. Two things could cut this and are not done yet:
  - The last pass often only refines a fringe: when the peak drops between passes, more
    cells cross peak / k. Marking with some margin below the threshold would save that
    pass.
  - A looser tolerance (1e-6) for all but the last pass.
- **Evenly stressed parts refine almost everywhere.** If most of the part is above half
  the peak (the cube in compression), the last mesh is most of the full one. On a big
  part that ends at the DOF budget with a coarse, optimistic result (§7.4).
- **ILU(0) has no convergence guarantee.** Single precision failed on a small printed
  bracket and double precision on a 2.4 M DOF one (§7.1). Gauss–Seidel takes over after
  150 iterations; it has converged on everything so far, at 1.4–3× the iterations where
  both work.
- **Bead resolution on a big thin-walled part is slow as well as large**: the iteration
  count grows with size there (§7.1). That makes the octree the only practical route for
  big parts, not just the cheaper one.
- **Memory is shared with whatever else runs.** The desktop has 32 GB but a 2 GB page
  file, so its commit limit is 34 GB, and other programs held over 20 GB of that during
  M3. The solver now refuses a solve that will not fit (§7.1), and the octree's DOF
  budget shrinks with the free memory.
- The Bambu CLI is undocumented and can change between versions. The wrapper isolates it,
  logs the version and always checks `result.json` `return_code`.
- HelixToolkit SharpDX sits on the archived SharpDX library. It works on .NET 10, but it is
  a dependency to watch.

---

## 15. Decisions (2026-10-05)

| Question | Answer |
| --- | --- |
| Stack | C#, .NET 10, WPF, HelixToolkit.Wpf.SharpDX |
| "Interfaces" | faces where the part mounts to or touches other parts (§10) |
| Printers | Bambu printers behave alike. Printer and process are fixed and only the filament changes (§9). |
| Temperatures | print temperatures only, no service temperature (§6) |
| Cell size | one cell ≈ one piece of one line: line width × line width × layer height (§6) |
| Solver | existing optimised engine for the linear solve (AMGCL), own C# code for the orthotropic toolpath physics (§7.1) |
| Speed | octree: coarse pass first (default 8³ cells per element, user-selectable 4³–32³), refine only around hotspots (§7.2) |
| C++ toolchain | approved: VS Build Tools 2026 with C++ + CMake via winget |
