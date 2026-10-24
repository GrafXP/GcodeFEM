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
  - **A user's preset holds "nil" where it leaves a list entry as inherited.** Bambu
    Studio 2 keeps a list setting with one value per nozzle variant of the printer and
    writes "nil" for the variants the user did not set (`nozzle_temperature: ["230",
    "nil"]`). Handed on as it is, the CLI refuses the preset with "Invalid parameter
    value(s)". `PresetLibrary.Merge` fills such an entry from what the preset inherits,
    in user presets only: a system preset's "nil" says that the filament leaves the
    setting to the printer, and stays. Found in M6.
- **`extruder_offset`** (X1C: `0x2`) shifts every G-code Y by −2 mm relative to the bed.
  The parser adds it back, so segments are in true bed coordinates. Checked: bead
  centrelines sit exactly 0.210 mm (half a line width) inside the model on all four sides.
- **The CLI assumes the Cool Plate** and refuses a filament that does not print on it
  (PETG HF, ABS, ASA, PC, PA: their presets give that plate a temperature of 0): "Filaments
  are not compatible with the plate type".
  For such a filament `BambuSlicer` names a plate with `--curr-bed-type`: the first one
  the filament prints on, the textured one first. A filament that prints on the Cool Plate
  gets none named, so PLA slices exactly as before. Found in M6.
  - The plate chosen in Bambu Studio is not read. `BambuStudio.conf` keeps it as a number
    (`curr_bed_type: "4"`), and which plate that is could not be confirmed.
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
  ignored. Since M7 `PrintClock` stretches each line type to the slicer's own
  `feature_type_times` (§6).

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
| TDS import | PdfPig 0.1.16 (PDF text extraction) |
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
      Fem/              bead-cell voxelizer and per-cell print data, stiffness, layer bond,
                        H8 elements, octree + Galerkin coarsening, hanging nodes, assembly,
                        BCs, stress recovery, failure criteria and safety factor, adaptive
                        loop, ILinearSolver
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
| deposition time | from feed rates along the G-code, stretched to the slicer's own times (M7) → time gap to the cell below and to the neighbouring bead |
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
- E₃ = E_z from the TDS (E_xy if not given).
- E₂ = bead-to-bead. First planned to default to E₃; since M7 it defaults to E₁, as the
  sheets give no in-plane anisotropy and E₃ would contradict their own flat specimens
  (What M7 built, below). Where the cell's directions spread, E₁ and E₂ are mixed by
  their shares.
- Everything is scaled by φ.

### Strength per cell
- X_t = σ_xy (TDS)
- Z_t = σ_z (TDS) × bond factor
- Y_t = bead-to-bead, defaulting to Z_t
- shear S estimated
- compression from the TDS bending data

### Print temperatures → bond factor
Print temperatures are the only temperature input; service temperature is out of scope.
- v1 was to be a calibratable table over nozzle temp and fan %, adjusted by the time gap to
  the cell below. A long gap means a colder interface and a weaker bond.
- Later: a cooling + reptation/healing model that turns the interface temperature at
  deposition into bond strength.
- M7 built a small version of the second instead of the table: cooling of the layer below,
  the interface temperature, healing above the glass transition (What M7 built, below).

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

### What M7 built

**Every cell keeps how it was printed** (`CellGrid`, filled by `Voxelizer`). Each piece of
bead the voxelizer drops into a cell brings its line's direction, line type, time, nozzle
temperature and fan, weighted by its volume:

| Per cell | How |
| --- | --- |
| line direction θ and spread | the eigenvectors of the in-plane orientation tensor Σ v d dᵀ. The spread is the share of the plastic running across the main direction: 0 for one line, ½ where two cross in equal parts (corners, grid infill) |
| line type | the class holding most of the plastic: outer wall, inner wall, sparse infill, solid infill, top, bottom, bridge (both kinds), other (gap infill, overhang wall, ironing, floating vertical shell) |
| deposition time | volume-weighted mean, on the calibrated clock below |
| nozzle temperature, fan | volume-weighted means |

- The sums take 60 bytes per cell slot of a block that holds material, on top of the 4 of
  the volume (15 MB for the cube). Plastic that says nothing of its lines (`CellGrid.Solid`
  in tests and benchmarks) counts as spread fully: the same every way in the plane.
- Checked: a bead along X gives its cells θ = 0 and spread 0, a 45° bead 45° in every cell
  it touches, two crossing beads spread ½; the cube's outer walls run along its sides.

**The clock is stretched to the slicer's** (`PrintClock`). Feed-rate times leave out
acceleration. Bambu Studio's result.json has its own seconds per line type, and each line
type's segments are stretched to match, the moves between them to its travel time. Stretches
outside 1–4 are taken for a mismatch and not applied.

| Measured | walls | sparse infill | solid infill | top | bottom | travel |
| --- | --- | --- | --- | --- | --- | --- |
| cube | 1.04–1.13 | 1.10 | 1.87 | 2.15 | 2.43 | 3.05 |
| 40 mm bracket | 1.41–1.66 | 1.78 | 2.18 | 2.65 | 3.09 | 2.82 |

**Stiffness per cell** (`OrthotropicMaterial`, `CellStiffness`). Orthotropic in the line's
frame: E₁ along the line, E₂ across it in the plane, E₃ across the layers, one Poisson's
ratio.

- The other Poisson's ratios and the shear moduli follow **Huber's rule**:
  ν_ij = ν √(E_i / E_j), G_ij = √(E_i E_j) / 2(1 + ν). That is the isotropic matrix with
  each direction scaled, so it stays positive definite, and with one modulus it is the
  isotropic material. No sheet gives more than one ν, nor any shear modulus.
- Turned about Z to the cell's direction (C' = M C Mᵀ). Where lines cross, the two in-plane
  moduli are mixed by their shares.
- Cells share an element matrix by layer height, direction (5° steps) and spread (steps of
  1/8). A material that is the same every way in the plane needs one per layer height, as
  before. The octree sums each cell's own matrix into its element, so coarse elements carry
  the directions of their cells (a full element of slanted lines is the exact brick: tested).
- **What a material file gives: E₁ = E₂ = E_xy, E₃ = E_z.** That is not what this plan said
  at first (E₂ = E₃). The sheet's in-plane value comes from flat specimens whose line pattern
  it does not show, and it says nothing of how much stiffer a line is along than across.
  With E₂ = E₃ a solid layer at ±45° would come out a tenth softer than the sheet's own
  number (laminate theory: 2309 against 2580 MPa for PLA Basic), with no data behind that.
  `OrthotropicMaterial` takes any E₂, and `--across-line` sets it in the CLI; the 0°/45°
  coupons of M10 are to measure it.
  - How much that choice matters on a real slice depends on where the slicer puts ±45°
    lines. On a 6 × 1.6 mm bar Bambu Studio laid the solid infill along the bar and only
    the top and bottom at 45°: E₂ = E₃ makes it 2 % softer (2368 → 2322 MPa).

**The check of the milestone: a bar lying flat and standing up** (`gcodefem bars`, and a
Bambu test). A bar of 60 × 4 × 1.6 mm, which prints solid both ways, is sliced flat and
turned up on its end (0, −90, 0), clamped at one end and pulled along its length. E is read
off the strain between a quarter and three quarters of the length, on the bar's own
section. Each bar is solved twice: with the material, and with its in-plane modulus every
way. The second leaves only what the slicer's lines and the cells make of the two bars, so
the ratio of the two ratios is what the material adds.

| Filament | E flat / upright, MPa | Ratio | The same every way | What the material adds | Sheet E_xy / E_z |
| --- | --- | --- | --- | --- | --- |
| Bambu PLA Basic | 2367 / 1787 | 1.325 | 1.058 | **1.252** | 1.252 |
| Bambu PETG HF | 1627 / 1310 | 1.242 | 1.057 | **1.175** | 1.175 |
| Bambu ABS | 1978 / 1667 | 1.186 | 1.057 | **1.122** | 1.122 |

- The material does exactly what the sheet says. The ratio of the printed bars is 6 %
  higher than the sheet's because, the same every way, the standing bar already comes out
  6 % softer than the flat one. Its cells hold 94 % plastic against the flat bar's 96 %, its
  layers are walls only, and in a tenth of its cells lines cross.
- That second factor depends on the bar: 0.95 for a 2 mm bar, which has two layers of
  sparse infill lying flat (88 % plastic against 94 %), 0.86 for 10 × 4 mm (59 % and 60 %
  plastic). The material's own ratio is 1.252 for every one of them.
- On their own section the bars come out at 0.92 (flat) and 0.87 (upright) of the sheet's
  moduli. A cell holds the bead's real cross-section (a single wall line fills 0.88 of its
  cell), and plastic above a full cell is lost to the cap at φ = 1 (4 % of the plastic of the
  2 mm bar). A specimen of the sheet is not solid either, so some of this is counted twice.

**What it does to a part.** The bracket of §10, "Pull sideways":

| Printed | Material | Peak von Mises | Loaded face moves, along the force |
| --- | --- | --- | --- |
| flat | the same every way (as in M6) | 18.44 MPa | 0.945 mm |
| flat | orthotropic | 18.68 MPa | 0.947 mm |
| on edge (90, 0, 0) | the same every way | 24.64 MPa | 0.903 mm |
| on edge (90, 0, 0) | orthotropic | 25.49 MPa | **0.987 mm** |

- Flat, the force bends the bracket in the plane of its layers, and E_z hardly enters. On
  edge it bends it across them, and it comes out 9 % softer. The peak moves from one side of
  the inner corner to the other.
- The printed cube under 100 N: 38 CG iterations where the isotropic matrix took 29; setup
  and solve 7.3 s. Voxelizing with the print sums takes 0.15 s.

**The bond across a layer line** (`BondModel`, `BondReference`). Per cell: how well it has
bonded to the cell below, as a share of the sheet's specimens. Not the table this plan
foresaw for v1, but the small physical model it put off for later, as it needs no more
invented numbers than a table would and gives them a meaning:

1. The cell below has cooled since it was printed: Newton's law towards the chamber
   (35 °C), with a time constant of 20 s in still air and 1 + 6 × fan times shorter.
2. The bead lands; the layer line takes the mean of the two temperatures, as two bodies of
   one plastic in contact do.
3. The bond grows with how far that is above the glass transition; the factor is that
   excess over the specimens', capped at 1, since the sheet's numbers are the best the
   plastic can do.

- The specimens' conditions: the sheet's nozzle temperature and glass transition (40 of the
  45 materials give the first; ABS, ASA and TPU sheets give no glass transition, and the
  usual one is assumed and said). No sheet says how long a specimen's layers took or how
  hard the fan blew; they are the filament preset's shortest layer time
  (`slow_down_layer_time`, 4 s for PLA Basic) and its highest fan, on the view that a
  small specimen prints at that.
- A cell with nothing below (the first layer, or over air) has no layer line: factor 1,
  and no time gap.
- So a hotter nozzle bonds better, a long wait or a strong fan worse, and the bond stops
  falling once the layer below is at the chamber's temperature: for PLA Basic at 220 °C
  that is 0.75 of the specimens'.

| Part | Bond across the layers, median and weakest |
| --- | --- |
| cube 20 mm | 0.95; layer 2 at 0.78, 14 s after the first |
| 40 mm bracket, flat | 0.83, 98 % of the layer lines below 0.9; layer 37 at 0.75, 23 s after the one below (the solid top layers take longest) |
| the same on edge | 0.94; layer 2 at 0.77 |
| 4 × 1.6 mm bar standing up | 1.00: its layers come 1 s apart, as the slicer cannot slow below 20 mm/s |
| PETG HF / ABS bar lying flat | weakest 0.92 / 1.00: less fan |

- **The stiffness does not use it.** It weakens the strength across the layers (Z_t, §8),
  which the failure check of M8 takes. The stiffness keeps the sheet's E_z: poorer bonding is known to show far
  more in the strength than in the stiffness, and E_z was measured across printed layer
  lines already.
- The cell's time is a mean. Where grid infill crosses itself, a cell's mean falls late in
  its layer, and the cell above it reads a gap a few seconds shorter than the layer time.
- The bond between neighbouring beads in a layer (Y_t) is not computed; §6 lets it default
  to Z_t.

**In the app.** The cells view colours by fill, by which way the lines run (along X, along
Y, the two diagonals, or crossing), by line type (the colours of the toolpaths view), by the
bond to the layer below, and by the time since the layer below; cells with nothing below are
grey. The result names the stiffness in the plane and across the layers, and the range of
the bond with its weakest layer. **In the CLI** `solve` and `adapt` print the clock's
stretches, the cells by line type and how many have crossing lines, and the bond with what
it is measured against and what of that is assumed. `--isotropic` solves with the in-plane
modulus every way, `--across-line` sets E₂, and `gcodefem bars` is the check above.

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
  (layer height × 5° angle bin × spread step), then scaled by φ per cell (§6, M7). The line
  type does not change the stiffness.
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
   - elements holding a cell stressed above peak / k (default **k = 2**)
   - since M8, also elements holding a cell whose safety factor is under kf × the lowest
     (default **kf = 4**; §8 has why it is not SF < k × min SF alone)
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
- **Block-sparse storage.** Cell data lives in dense per-block arrays (structure of arrays),
  only for blocks with material. Since M7 that is 64 bytes per cell slot rather than the 4
  first planned: the volume, and the sums the line direction, line type, time, temperature
  and fan are taken from (§6). The octree only decides which elements the FEM uses; it keeps
  no more of each cell than its fill and which matrix it takes.
- **No per-cell integration.** Leaf stiffness comes from a small cache (layer height × 5°
  angle bin × spread step), scaled by φ.
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

### What M8 built

**The strengths** (`Material.Strength()`, `PrintedStrength`). In the frame of a cell's lines:

| Strength | From | Bambu PLA Basic |
| --- | --- | --- |
| X, along the line | the sheet's tensile strength in the plane | 35 MPa |
| Z, across the layers | the sheet's tensile strength across them, × the cell's bond factor (§6) | 31 MPa × 0.75…1 |
| Y, bead to bead in a layer | Z, without the bond factor: assumed | 31 MPa |
| in compression, every direction | the sheet's bending strength in the plane: assumed | 76 MPa |
| shear along a plane | that plane's tensile strength / √3 (von Mises): assumed | 20.2 / 17.9 / 17.9 MPa |

- Y = Z is what this plan always meant (§6), and unlike E₂ = E₃ (M7) it contradicts nothing in
  the sheets: a flat specimen whose lines run along it never loads the joint between beads.
- A bent specimen breaks where the plastic gives first, rarely on its pressed side, so the
  bending strength is below what the plastic takes in compression and stands in for it.
- A material without a tensile strength in the plane gets no safety factor, and says why. Of
  the 45 materials, 40 give both tensile strengths; the generic stand-in of last resort gives
  none. A material without one across the layers takes the one in the plane and is called
  optimistic; without a bending strength, compression takes the tensile strength.

**The check** (`CellStrength`, `CellFailure`). Each cell's stress is turned into the frame of
its lines and divided by its fill: the stress in its plastic. Three planes of a printed cell
can give way, each opening under tension across it and sliding under the shear along it:

| Mode | Plane | Index |
| --- | --- | --- |
| layers pull apart | the layer line below the cell | √((⟨σ₃₃⟩ / Z)² + (τ₃₁² + τ₂₃²) / S_z²) |
| beads of a layer pull apart | the joint between two beads | √((⟨σ₂₂⟩ / Y)² + (τ₁₂² + τ₂₃²) / S_y²) |
| lines break | the line's own cross-section | √((⟨σ₁₁⟩ / X)² + (τ₁₂² + τ₃₁²) / S_x²) |
| crushed | | the largest compression of σ₁₁, σ₂₂, σ₃₃ / the compressive strength |

- Every index grows in proportion to the load, so SF = 1 / the largest is the factor on the
  loads at which the cell breaks, and the part breaks at SF × its loads.
- **Shear goes into every plane** it runs along. With only the normal stress in the line and
  bead modes (this section's first plan), a plastic as strong every way pulled at 30° to its
  lines would hold 1.27 × its tensile strength. With it, at any angle, 1 to 1.06 ×.
- Where lines cross, the strengths along and across them are mixed by their shares, as the
  stiffnesses are.
- **Tsai–Wu** with the same strengths runs alongside as the cross-check. Each shear goes
  against the weaker of the two planes it runs along; F_ij = −½ √(F_ii F_jj). Its linear terms
  do not scale with the load, so its safety factor is the root of λ² a + λ b = 1.
  - It reads 1.3–1.5 × higher than the modes at the bracket's weakest spot. Its linear terms
    count compression in one direction as strengthening the cell against tension in another;
    the modes do not.

**A cell less than half full** is taken as half full. Without that, the weakest spot of the
bracket was a cell a tenth full. Such a cell holds the edge of a bead whose middle is in the
next cell; the mesh makes it a soft link between its neighbours, and its strain is not that of
its plastic. Measured on the bracket ("Pull sideways", bead resolution):

| Stress taken over at least | Flat: SF, where | On edge: SF, where |
| --- | --- | --- |
| the plastic of the cell | 1.49, an inner wall 12 % full | 0.82, a bridge 9 % full |
| a quarter of the cell | 1.57, the outer wall at the inner corner (53 % full) | 0.835, the layer line at the inner corner |
| **half the cell** | **1.57, the same** | **0.835, the same** |
| the whole cell | 2.44, the same corner, top layer | 1.53, the same layer line |

A quarter and a half agree. The whole cell would take a wall that straddles two cell columns
(half full in each) at half its real stress.

**The octree refines where the part is nearest to breaking** (`AdaptiveAnalysis`, with a
`CellStrength`). It refines where the stress is above peak / k, as in M3, *and* where the safety
factor is under kf × the lowest (`FailureRefineFactor`, `--kf`, default 4). The stress zone
carries the load to the weak spot and is far wider: 433 cells of the flat bracket are above
half the peak stress, but only 70 are within 2 × the lowest safety factor (10 on edge).
Refined by the failure index alone, the surroundings stayed too coarse and too stiff. Safety
factor of the full bead-resolution solve / the octree's, and the octree's share of the DOF:

| Refined where | Bracket flat | Bracket on edge | Cube 50 N sideways |
| --- | --- | --- | --- |
| SF < 2 × the lowest, alone | 0.923 (31 %) | 0.902 (22 %) | |
| stress > peak / 2, and SF < 2 × the lowest | 0.967 (35 %) | 0.934 (23 %) | 0.987 (39 %) |
| … SF < 3 × the lowest | 0.984 (46 %) | 0.942 (27 %) | 0.989 (40 %) |
| **… SF < 4 × the lowest (default)** | **0.991 (55 %)** | **0.959 (37 %)** | **0.995 (67 %)** |
| stress > peak / 3, and SF < 3 × the lowest | 0.997 (50 %) | 0.961 (32 %) | 0.998 (83 %) |

- In every case the weakest spot is the same cell as in the full solve.
- The bracket on edge is the hard case for the octree, as it was before M8: its peak stress
  alone was 0.93 with M3's defaults.
- The passes stop as in M3, now when the weakest spot (not the stress peak) is at bead
  resolution and settled. `--mark vonmises` refines by the stress alone, as before.

**The bracket** (§10, "Pull sideways": 20 N at the tip, two bolts; PLA Basic, 0.20mm Standard):

| Printed | Safety factor | Breaks at | How, where | Weakest layer line | Tsai–Wu |
| --- | --- | --- | --- | --- | --- |
| flat | 1.58 | 31.7 N | beads of a layer pull apart: the outer wall of the inner corner, layer 1 | under layer 4, 0.6 mm up: SF 2.86 (bond 0.76) | 2.10 |
| on edge (90, 0, 0) | **0.87** | **17.4 N** | **layers pull apart**: at the inner corner, where the upright leg meets the base | **under layer 51, 10 mm up: the same, bond 0.80** | 1.31 |

On edge it breaks at a layer line at 55 % of the flat load, the classic way a printed bracket
fails. The cube, clamped on the bed and pushed sideways with 50 N: SF 4.26, its first layer
line pulling apart at the clamp.

**The bars of M7, pulled until they break** (`gcodefem bars`), in their middle half:

| Bar | Breaks at | How | Over the bar's section | Against the sheet |
| --- | --- | --- | --- | --- |
| flat | 156 N | beads of a layer pull apart (the ±45° skins) | 24.3 MPa | 0.69 of X, 35 |
| upright | 137 N | layers pull apart | 21.4 MPa | 0.69 of Z, 31 |

The ratio, 1.14, is the sheet's 35 / 31 = 1.13. In absolute terms both break at 0.7 of the
sheet's strength over their section. The stress is taken in each cell's plastic, and a cell
holds less than its share of the section (the M7 bars hold 94–96 % plastic, and the cells at
their edges about half). That is the cautious side, and what M10's coupons are to calibrate.

**In the app.** The results view colours by safety factor, in four bands that get darker
from breaking (red, below 1) through amber (1–2) and green (2–4) to a dark grey-green (4 and
up), so the eye goes to the danger first and the bands read without colour vision. It also
colours by what breaks first: the mode of each cell within 2 × the lowest safety factor, the
rest grey. A white arrow points at the weakest spot from outside the part, and a white outline
traces the weakest layer line. A click on the result probes a cell: what it is, how it was
printed, its bond, its stress in the frame of its lines, and the safety factor of each mode,
in a box over the viewport. The summary leads with the safety factor and the load it breaks at,
how and where, the weakest layer line and Tsai–Wu, and what the strengths rest on. The pass
table has the safety factor of each pass. `--colour safetyfactor|failuremode` and
`--probe "x,y"` script it.

**In the CLI.** `solve` and `adapt` print the strengths with what is assumed, then the safety
factor, the load it breaks at, the weakest layer line, the share of each mode near the
weakest spot and Tsai–Wu. `adapt --reference` and `--sweep` compare the safety factor with the
full solve. `bars` prints the breaking loads.

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
  Bambu's own are in `materials/tds/` since M6; those of the other vendors' filaments
  Martin prints (eSun, Sunlu, Prusament, …) are still to come.

### What M6 built

**A material file** (`materials/<name>.material.json`, read and written by `Core/Materials`)
is a filament as the solver and the failure model need it:

- its name, vendor and type (Bambu Studio's `filament_type`), the filaments it is for, and
  whether it stands in for its type
- the document its numbers are from: title, file (kept in `materials/tds/`), where it was
  downloaded and its SHA-256
- up to 21 properties, and a note on how the specimens were printed and treated, in the
  sheet's words

| Group | Properties |
| --- | --- |
| Stiffness | Young's modulus in the plane of the layers and across them; Poisson's ratio |
| Strength | tensile strength, elongation at break, bending modulus and bending strength, each in the plane and across; impact strength in the plane (plain and notched) and across |
| Other | density, glass transition temperature, lowest and highest nozzle temperature |
| Specimens | the nozzle temperature, bed temperature and speed the specimens were printed at |

"In the plane" (X-Y in Bambu's sheets) is measured on a specimen printed lying flat,
"across the layers" (Z) on one printed standing up.

**Every number says where it is from.** A value has a source (the id of the material's
document, its page, and the test method as the sheet names it) or a reason why it is
assumed. Never both and never neither: a file that breaks this is refused when it is read,
and a material that breaks it is not saved.

- The unit is written with every value and checked against the property's, so 2.5 typed
  under "MPa" for 2.5 GPa cannot pass.
- Poisson's ratio is the one number no sheet gives. It is assumed by filament type (PLA
  0.35, PETG 0.38, ABS and ASA 0.35, PC 0.37, polyamides 0.39, TPU 0.48, anything else
  0.35) and marked so.
- A material that gives no Young's modulus is kept for what it does say (the support
  filaments, PVA) and refused by the solver.

**From a filament to its material** (`MaterialLibrary.Match`):

| The filament | gets | and the user sees |
| --- | --- | --- |
| is named by a material file | that material | its numbers |
| is not, but a material stands in for its type | that material | the same, with "stands in for it" beside it, in the print panel, the result and the CLI |
| has neither | a generic PLA (E 2500 MPa, ν 0.35, all assumed) | the warning box of the result: the displacements are not this filament's |

- A name without a printer ("Bambu PLA Basic") covers that filament's presets on every
  printer ("Bambu PLA Basic @BBL X1C 0.2 nozzle"); a name with one covers that preset alone
  and goes first.
- Names are compared by their letters, digits and "+": a sheet's "ASA Aero" is the slicer's
  "ASA-Aero", and "PLA Silk" is not "PLA Silk+".
- **The solve takes the filament from the G-code's header** (`filament_settings_id`,
  `filament_type`). So the material is that of the filament the part was sliced with, not
  of whatever is selected by the time Solve is pressed.
- Two materials that name one filament, or stand in for one type, are reported; the first
  by name is used.

**Reading a data sheet** (`PdfText`, `TdsImporter`, `BambuTds`):

1. PdfPig gives the words of a page with their places. Words on one baseline make a line,
   whatever order the file holds them in; that puts the cells of a table row side by side.
   Two baselines count as one if they are closer than 0.3 of the letter size.
2. The template for Bambu Lab's sheets has one pattern per row: label, test method, value,
   ± spread, unit. Rows count only under the heading they belong to (the nozzle temperature
   stands in two tables: the recommended range and what the specimens were printed at).
3. Bambu Studio's own preset of the filament gives the name and the type, since those are
   what a filament is matched by. It also carries one number of the sheet,
   `impact_strength_z`, which is compared: a difference means another version of the sheet.
4. The result is shown with what was not found. In the app it fills a form that is saved
   only when the user says so; in the CLI the file is written and printed for looking over
   (`--dry-run` writes nothing).

Bambu has two layouts, and both are read:

| Layout | Headed | Differences |
| --- | --- | --- |
| since 2022 (43 of the sheets) | "Bambu Filament – Technical Data Sheet V3.0" | the specimens' conditions in a table of their own, with a footnote on drying and annealing |
| 2026 (PLA Pure, PETG Matte) | "TDS of Bambu PLA Pure" | charts beside the tables, some labels on two lines, and the specimens' conditions in a note beside the mechanical properties |

What the files do that the template has to take:

- Headings come out with their words run together ("PETGHF", "TechnicalDataSheetV1.0"),
  so the name is taken from the sentence "…the performance of PETG HF material".
- Full-width punctuation (，；（）) and another apostrophe in "Young's".
- The impact strength in the plane is one cell with two values, plain and notched, on
  lines above, beside or below the label.
- "N / A", and bounds such as "> 650 %" for a specimen that did not break: neither is a
  value. They are left out and named.
- In the 2026 layout the file does not give all the letters of the note (no "°", no "fi").
  Its numbers are taken (nozzle, bed, speed, infill); its text is left to the user, and
  the import says so.
- The footnote on the specimens is not always marked with a star. Of it, only the
  sentences that begin "All the specimens…" are kept; the rest is advice on ovens.

There is no template for another vendor yet. Their sheets are typed into the app's form,
with the sheet open alongside.

**The library: every Bambu Lab filament with a published sheet.** Martin asked for this on
2026-10-08 in place of the two the milestone named. Bambu Studio 2.08 knows 51 filaments
of Bambu's own; 45 have a sheet and are in `materials/`.

- The sheets are the ones Bambu's store links on each filament's page (40). For filaments
  that are no longer sold, and where a newer version than the store's was found, they are
  the copies on Bambu's own Shopify CDN (PA-CF, PLA Tough, TPU 95A; PC and PLA Sparkle).
  Of several versions the highest was taken. Every file records the link and the hash.
- The check against Bambu Studio's presets: 33 agree. 5 differ (PETG Basic, PETG Matte,
  PLA Tough+, PLA Translucent, TPU 85A); there the preset holds the number of an older
  sheet or of a sibling filament. 7 sheets give no such number.
- 808 numbers: 763 from the sheets with their page, 45 assumed (Poisson's ratio).
- 5 have no stiffness: PVA and the four support filaments.
- **No sheet was found for 6**: PLA Dynamic, PLA Impact, PLA Lite, PPA-GF, Support W and
  Support G. They get the stand-in of their type like any other filament without data.
- A test reads every sheet again and compares it with its material file, value by value.

| Material | Sheet | E in the plane / across, MPa | Tensile strength in the plane / across, MPa |
| --- | --- | --- | --- |
| Bambu PLA Basic | V3.0 | 2580 ± 220 / 2060 ± 170 | 35 ± 4 / 31 ± 3 |
| Bambu PETG HF | V1.0 | 1810 ± 190 / 1540 ± 130 | 34 ± 4 / 23 ± 4 |
| Bambu PETG Basic | V3.0 | 2780 ± 65 / 2550 ± 100 | 51 ± 1 / 35 ± 6 |
| Bambu ABS | V3.0 | 2200 / 1960 | 33 / 28 |
| Bambu PAHT-CF | V3.0 | 3860 / 2180 | 92 / 47 |
| Bambu TPU 95A HF | V1.0 | 9.8 / 7.4 | 27.3 / 22.3 |

The stand-ins, one per filament type: PLA Basic for PLA, **PETG HF for PETG**, ABS, ASA,
PC, TPU 95A HF for TPU, PAHT-CF for PA-CF, and the only material of its type for PLA-CF,
PETG-CF, PET-CF, PA6-CF, PA-GF, ABS-GF, ASA-CF, PPA-CF, PPS-CF, PLA-AERO, ASA-AERO and
TPU-AMS. PETG HF and not PETG Basic was Claude's choice: the stiffness in PETG Basic's
sheet moved from 1460 to 2780 MPa between V2.0 and V3.0, and 1810 lies in the range usual
for printed PETG. A tick in the materials window moves a stand-in.

**What the solver did with a material in M6:** the same stiffness in every direction,
the one in the plane of the layers, with the assumed Poisson's ratio (since M7: the one
across the layers too, §6; `--isotropic` still solves the M6 way). Measured on the bracket
of §10 ("Pull sideways"):

| Filament | Material | Peak von Mises | Loaded face moves, along the force |
| --- | --- | --- | --- |
| Bambu PLA Basic | the numbers used until now (`--E 2500 --nu 0.35`) | 18.44 MPa | 0.975 mm |
| Bambu PLA Basic | its own, E 2580 | 18.44 MPa | 0.945 mm |
| eSun PLA+ | PLA Basic, standing in | 18.44 MPa | 0.945 mm |
| Bambu PETG HF | its own, E 1810, ν 0.38 | 17.98 MPa | 1.374 mm |

- **The stresses of a part loaded by forces do not depend on its stiffness** while that is
  the same everywhere: 2500 → 2580 MPa changes the displacement by 2500 / 2580, to the three
  digits shown, and the peak not at all. So until M7 brought stiffness that differs by
  direction, a material moved the displacements only. PETG HF's peak differs because its
  slice does.
- The strengths are in the files; since M8 the failure check takes them (§8).

**In the app.** The print panel says in a line what the selected filament will be solved
with, in the colour of a caution if that is not its own material. "Materials…" opens the
library: the materials on the left, a form on the right with one row per property (value,
±, unit, test method, source, page, or the reason it is assumed). "Import a data sheet…"
fills the form from a PDF, "Open the data sheet" shows the sheet beside it. Changes that
are not saved lock the list, so picking another material cannot lose them.

**In the CLI.** `gcodefem material list` shows the materials and what each filament preset
gets, `material show <name>` every number with its source, `material import <tds.pdf>`
makes a material from a sheet. `solve` and `adapt` print the material they use;
`--material <name>` takes another, `--E` and `--nu` give the numbers directly.

**What the sheets' numbers are, and are not.** The specimens were printed solid (100 %
infill) at the temperatures and speeds the file records, dried, and for most filaments
annealed before testing. A real part is not annealed and not solid. The numbers are what
the plastic can do at best, most of all across the layers. Closing that gap is what the
bond factor (M7) and the coupons (M10) are for.

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

Left for later: elastic supports, torque, gravity (the density it needs is in the
material files since M6), and solving several load cases on one matrix.

---

## 11. Orientation loop

- **Manual**: rotate in the viewport (gizmo, "lay this face on the bed", 90° snaps), then
  Slice & Solve. Each result is added to a comparison table.
- **Automatic** (amended 2026-10-23 by Martin: candidates are faces, as in Bambu Studio,
  with a minimum size, and a preview before the sweep starts):
  - **Candidates are the faces the part can be laid on**, as with Bambu Studio's "Lay on
    face". No samples of the sphere: an orientation that does not stand on a face is not one
    a user would print.
    - A part can only rest on a plane of its **convex hull**. The candidates are those planes,
      with the hull's facets merged where their normals differ by under 1°. A flat face
      inside a pocket or behind a step is no candidate. The "Lay a face on the bed" of M5
      turns any picked face downwards; if that face is not on the hull, something else
      touches the bed first.
    - What stands on the bed is the **contact**: the model's own triangles that lie in the
      plane (within 0.05 mm). That is what holds the print to the plate, and what counts for
      size. A ring standing on its end touches with its rim only, not with the disc its
      hull spans. A part resting on edges or points has no contact and is no candidate.
    - **The smallest contact that counts is the user's to set**, in mm², in the sweep's
      panel, and saved with the study. There is no built-in value: until one is typed, the
      preview shows every face with contact, largest first, and the sweep does not start.
      For the bracket, 50 mm² keeps its 10 × 8 mm end faces and leaves out the chamfers and
      fillets.
    - **How many of them are swept is the user's to set as well**, in the same panel and
      saved with the study: the largest that many of the faces left. There is no built-in
      number either: until one is typed, all of them are taken. Beside it stands what that
      will roughly cost: each candidate is a slice and a coarse solve (seconds each, from the
      cache when sliced before), and the best 3 are solved in full.
    - The turn is `Orientation.LayFlat` of M5, exact to the rounding of the angles. The spin
      on the bed is the shortest turn from how the model lies when the sweep starts. As an
      option, each face can also be tried spun by 45° and 90°: the walls follow the part,
      but sparse infill and the top and bottom skins follow the bed's axes.
    - Mirror-alike faces (the six of a cube) give alike results; they are not detected and
      simply cost their time.
  - **Preview before the sweep**: the candidates are shown together in 3D, each copy of the
    part laid on its face on a patch of bed of its own, in a grid, numbered, with its contact
    tinted and its interfaces marked as in the model view. A list beside the view gives each
    one's contact area and height. Candidates can be ticked off there or by a click on the
    copy (a cosmetic face that must not touch the plate, a print too tall for the printer).
    The sweep starts from that view and runs only what is ticked. The smallest contact is
    typed there, and the preview follows as it changes.
  - Prefilter: overhang area, shown in the preview's list, and support volume from the slice.
  - Screen every candidate with a coarse pass only (slice + coarse solve, seconds each),
    then run the full adaptive loop on the best 3. The coarse pass reads the safety factor
    high (M8: 3.35 against 1.58 for the bracket flat, 1.65 against 0.87 on edge). Screening
    ranks the candidates and does not judge them, and M9 checks that the coarse ranking is the
    final one on the bracket. If it is not, screening takes two passes.
  - Rank by min SF over all load cases, then max deflection, print time and filament (from
    `result.json`), then support volume.
- **Compare view**: table, side-by-side SF maps, and the weakest spot of each candidate. A
  row of the table opens that candidate's result in the views.
- **Cache key**: hash(mesh, R, filament, presets, overrides). Reruns reuse G-code and cells.
- **Progress**: the sweep shows in the status bar's progress bar (§12) like a single solve.
  Its share done is (candidates done + the current one's share) / their number, and its time
  left follows from the time per candidate so far. Each candidate's share comes from its own
  `SolveEstimate`; the slices that make up the screening tell only their time.
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
| Cells | the bead cells, coloured by fill φ | Since M7 also by line direction, line type, bond to the layer below and the time since it (§6). |
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

Since M8 the results view also colours by safety factor and by what breaks first, marks the
weakest spot and the weakest layer line, and probes a cell on a click (§8).

**A progress bar** in the status bar shows long work while it runs, with the time taken
and, where it can tell, about how long is left. Any work hands it a function that says
where the work stands (`WorkProgress`: step, time taken, share done, time left), read five
times a second.

- **Slicing**: Bambu Studio says nothing while it slices, so the bar runs to and fro beside
  the time, and the status names the step (slicing, reading the toolpath, the cells, the bond).
- **Solving** (`SolveEstimate`): the adaptive loop reports each pass as it goes to the solver
  (`AdaptiveSolving`: pass, unknowns, root level) and as it ends.
  - A solve takes about root level + 1 passes. A pass takes 0.3 s plus a time per unknown,
    read off the last pass of over 20,000 unknowns (25 µs until one has run). Passes to come
    grow as the last two did, at most 5×.
  - The native solver says nothing until it is done, so within a pass the bar runs on the
    clock towards the pass's predicted end and slows down rather than passing it; it never
    goes back.
  - On the bracket on edge, 3.4 s in, it said 9 s left of what took 10 s in all: it guessed
    the last pass larger than it came out, and corrects itself when the pass starts.

**Results on the print lines** (M8b; Martin asked for it on 2026-10-23). The results view
gets a second way to draw a result: the toolpath's beads instead of the cells, each piece of
a bead coloured by the result where it runs. Then a result reads as what the printer laid
down: which wall line breaks first, which infill lines carry the load, at which layer line.

- **How**: every bead is cut where it passes from one cell into the next, by the same walk
  along the line the voxelizer makes when it deposits it (`Voxelizer`). Each piece takes the
  value of its cell: safety factor, what breaks first, von Mises, the stress across the
  layers, the displacement, the element size. A wall line that is safe along most of its
  length and near breaking at a corner shows that along itself.
- **How fine**: the result exists per cell (line width × line width × layer height), so a
  piece is about one line width long. Two beads that share a cell (where infill meets a wall,
  where lines overlap) show the same value. The solve says nothing finer.
- **Deformed shape**: the ends of the pieces move with the displacement inside their cell,
  interpolated between its corners, as the cell faces do. The bead keeps its cross-section.
- **Everything the toolpath view has** still works:
  - the layer range;
  - switching line types off one by one (with the sparse infill off, the walls' safety
    factor shows);
  - plain lines instead of tubes for big parts.
- **Lines outside the solve** are drawn grey, with no value: skirt, support, prime tower, and
  islands no mount holds.
- **Marks and probe**: the weakest spot and the weakest layer line are marked as on the
  cells. A click on a bead probes its cell, and also says which line it is: line type,
  layer, when it was printed, nozzle temperature and fan, and its bond to the layer below.
- **Cost**: about one piece per cell a bead crosses, 1–2 × the cells. On the 80 mm bracket
  (574k cells) that is up to about 10 M vertices as tubes of 10 each. Over the tube budget of
  the toolpath view (400k pieces) it draws lines, as that view does already. The pieces are
  cut once per result; changing the quantity only colours them again.
- **Earlier passes**: they kept the safety factor, von Mises and the element size per cell,
  and show those; the rest is of the last pass, as on the cells.

Still to come: line-direction glyphs on the cells (M7 colours them by direction instead),
cuts other than by layer, the part frame and the Compare tab (M9), and cancelling a running
solve.

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
| M6 | **Materials & TDS**: JSON DB, Bambu TDS import, filament → material mapping | PLA Basic + PETG HF generated from their PDFs with sources. **Done 2026-10-08:** both are generated from their sheets, every number with its page, and so is every other Bambu filament with a published sheet (45 materials); the solver takes the stiffness of the filament that was sliced; details in §9. |
| M7 | **Print-aware material**: line directions, orthotropy, feature classes, bond factor from temps and time gaps | flat vs upright bar shows the TDS XY/Z ratio. **Done 2026-10-23:** every cell keeps its line direction, line type, time, nozzle and fan; the stiffness is orthotropic per cell; the bars' ratio is the sheet's once what the cells alone do is taken out (1.252 for PLA Basic, 1.175 PETG HF, 1.122 ABS), 1.325 as printed; the bond factor is computed and shown, for M8 to use; details in §6. |
| M8 | **Failure & results**: criteria, SF, governing mode, weakest spot, deformed shape, probe. The refinement marking switches from von Mises to the real failure index. | bracket shows its weakest layer line and failure load. **Done 2026-10-23:** four modes in each cell's line frame (layers, beads, lines, crushing) with Tsai–Wu alongside, Z × the bond factor; the bracket on edge breaks at its layer line 10 mm up at 17.4 N (SF 0.87), flat at 31.7 N; the octree also refines where SF < 4 × the lowest and gets the SF within 1–4 % of the full solve; shown as SF bands, modes, an arrow, the outlined layer line and a probe; details in §8. |
| M8b | **Results on the print lines**: each bead cut at its cells and coloured by their result, deformed with them, with the toolpath view's layer range, line types and thin lines; a click on a bead probes it (§12) | the bracket's safety factor and failure modes shown on its beads, its weakest wall line found with the sparse infill switched off |
| M9 | **Orientation study**: manual compare + automatic sweep over the faces the part can lie on (convex hull, contact at least the area the user sets, as many as the user sets), shown in a 3D preview to tick off before it starts; coarse screening, refine the best 3, ranking | the bracket's candidate faces previewed, swept, and ranked in a table |
| M10 | **Calibrate**: print and break coupons (flat/upright bar, 0°/45° raster, hook) to tune the bond and diagonal factors; CalculiX cross-check | predicted vs measured failure loads recorded in `docs/calibration.md` |

---

## 14. Risks and honest limits

- **Absolute numbers will be rough.** FDM strength scatters ±20 % even in the TDS. What the
  tool is good at is **ranking orientations and finding the weak spot**, and that is all the
  loop needs. Real coupons in M10 tighten the numbers.
- **A data sheet's numbers are the best the plastic can do.** Bambu's specimens are
  printed solid, dried and mostly annealed (§9). A real part is neither, and across the
  layers it is weaker than the sheet says. The bond factor (M7) takes some of that into
  account in the failure check (M8). Until the coupons (M10) calibrate it, a safety factor is
  a ranking more than a margin.
- **Shear and compression strengths are assumed** (§8): no sheet gives them. The joint
  between two beads of a layer is assumed as strong as a layer line printed like the
  specimens'. And taking the stress in each cell's plastic, the model breaks the M7 bars at
  0.7 of the sheet's strength over their section; it is on the cautious side by about that
  much until M10 says otherwise.
- **The native solver fails now and then** (HANDOFF.md): an access violation inside AMGCL's
  setup, or NaN, a few times in a hundred solves, in bursts, cause unknown. A NaN is solved
  again; a crash takes the program with it.
- **The bond factor is a model with assumed parameters** (§6): the chamber at 35 °C, a
  cooling time of 20 s in still air, the fan cooling 7 times faster at full speed, the
  specimens printed at the preset's shortest layer time. It gets the directions right (hotter
  nozzle, shorter wait and less fan bond better) but its size is not measured. For PLA Basic
  it runs from 1 down to 0.75. The chamber of an enclosed ABS or PC print is warmer than
  35 °C, which the model does not know.
- **How much stiffer a line is along than across is not known** (§6): the sheets give one
  in-plane modulus, and it is used both ways. The 0°/45° coupons of M10 decide it.
- **A stand-in is another filament's numbers.** Martin's own filaments (eSun, Sunlu,
  Prusament, …) have no material files yet and are solved with Bambu's material of the
  same type. The result says so, but it cannot say how far off that is. PLA+ and PLA
  Basic, or a PC blend and PC, can differ by a few tens of percent.
- **TPU is solved like everything else:** linear, with small strains, and with a
  Poisson's ratio of 0.48, near which 8-node elements are known to come out too stiff (not
  measured here). A flexible part that visibly deforms is outside what this model can say.
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
