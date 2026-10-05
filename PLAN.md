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

## 2. Verified on this machine (2026-10-05)

| Fact | Value |
| --- | --- |
| Bambu Studio | 2.08.02.61, `C:\Program Files\Bambu Studio\bambu-studio.exe` |
| Active setup | A1 mini 0.4 nozzle · Bambu PLA Basic · 0.20mm Standard @BBL A1M |
| .NET SDK | 10.0.201 (newest GA; .NET 11 ships Nov 2026) |
| C++ toolchain | VS Build Tools 2026 (18.10.2) at `C:\Program Files (x86)\Microsoft Visual Studio\18\BuildTools`: MSVC 19.51, CMake 4.3.1 (bundled), OpenMP 2.0 (`/openmp`; `/openmp:llvm` if AMGCL needs newer). C# → native OpenMP DLL via P/Invoke verified, 8 threads. |
| Hardware | i5-8350U 4 cores · 8 GB RAM · Intel UHD 620 (DX11 OK) |

**Headless slicing works** with system preset paths passed directly. The CLI resolves the
`inherits` chain itself. A 20 mm cube slices in 4 s with exit code 0:

```
set BBL=C:\Program Files\Bambu Studio\resources\profiles\BBL
bambu-studio.exe --debug 2 --arrange 1 ^
  --load-settings "%BBL%\machine\Bambu Lab A1 mini 0.4 nozzle.json;%BBL%\process\0.20mm Standard @BBL A1M.json" ^
  --load-filaments "%BBL%\filament\Bambu PLA Basic @BBL A1M.json" ^
  --slice 0 --outputdir <dir> --export-3mf out.gcode.3mf model.stl
```

Outputs: `out.gcode.3mf`, `plate_1.gcode`, `result.json`. The `result.json` holds the
`return_code`, the placed object's bbox, per-feature print times and the totals.
Fixtures from this run are in [samples/](samples/).

Gotchas:
- The exe is a GUI-subsystem binary, so `--help` prints nothing. Diagnose through the exit
  code, `result.json` and `%APPDATA%\BambuStudio\log`.
- `--arrange 1` placed the 20 mm cube at bed (80, 80, 0). The bed→part translation comes
  from that bbox.

**What the G-code gives us**:

| Marker | Use |
| --- | --- |
| `; CHANGE_LAYER`, `; Z_HEIGHT:`, `; LAYER_HEIGHT:` | layer index and thickness → cell Z boundaries |
| `; FEATURE: <type>` (emitted only on change) | line type per segment |
| `; LINE_WIDTH:` | bead width per segment |
| `M104` / `M109 S<t>` | nozzle temperature |
| `M106 S<0–255>` | part-cooling fan |
| `G1 X Y E F` with `M83` (relative E) | geometry, deposited volume, speed → deposition time |
| settings block at file end | `line_width`, `layer_height`, `filament_type`, `filament_settings_id`, `sparse_infill_pattern`, `sparse_infill_density`, `wall_loops`, … |

Feature types seen so far: Outer wall, Inner wall, Sparse infill, Internal solid infill,
Top surface, Bottom surface, Bridge, Floating vertical shell, Skirt, Custom. Expect also
Support, Support interface, Overhang wall, Gap infill. The parser must accept unknown
types.

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

### Size and cost
A cell holds about 0.032 mm³, so **~25k cells per gram of PLA**, plus 20–30 % for
face-connected diagonals. Storing them is cheap: 4 bytes per cell, so 50 g is about 6 MB.

Solving every cell at once is what's expensive: about 150k DOF per gram, which tops out
around 15 g on 8 GB. The octree solve (§7.2) only goes down to bead resolution where
stress is high, so the cost follows the size of the hotspots, not the size of the part.

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
- **In:** CSR matrix, node coordinates, right-hand side, tolerance.
- **Inside:** builds `rigid_body_modes` from the coordinates and solves with CG +
  smoothed aggregation (`as_scalar`, float preconditioner).
- **Out:** displacements and iteration info.
- Builds once with CMake + MSVC. AMGCL's headers are vendored.

**C# side (Core/Fem):**
- **Element stiffness:** H8 element stiffness for rotated orthotropic material, cached per
  (layer height × 5° angle bin × feature class), then scaled by φ per cell.
- **Assembly and BCs:** parallel CSR assembly. Fixed DOFs are eliminated.
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
- The app suggests L from free RAM and part size, and the user can override it. A coarse
  pass at L = 3 on a 50 g part is a few thousand elements, under a second to solve.

**Coarse elements get their stiffness from their own bead cells** (Galerkin coarsening):

  K_parent = Σᵢ Pᵢᵀ K_childᵢ Pᵢ  over the 8 children

- Pᵢ is the fixed trilinear interpolation from the parent's corners to child i's corners.
- It's built bottom-up once per orientation, so each coarse element already reflects its
  walls, infill lines and gaps, with no separate homogenisation model.
- P is sparse: each child corner depends on 1, 2, 4 or 8 parent corners. That makes the
  products ~7× cheaper than dense ones, about a second for 1.5 M cells.
- Bias: interpolation forces the inside of a coarse element to deform trilinearly, so
  coarse elements come out **stiffer** than reality. The bias is largest where an element
  contains thin members that would bend (an infill line, or a thin rib with one element
  through its thickness). That's acceptable for *finding* hotspots, because the refined
  passes produce the numbers. M3 measures the bias against a full bead-resolution solve.

**Adaptive loop:**
1. **Coarse solve** with every root block as one element. Seconds, even for big parts.
2. **Downscale:** interpolate the coarse displacements to every bead cell, and evaluate
   that cell's stress and failure index with its own material. This is cheap and already
   separates walls from infill inside one coarse element.
3. **Mark** elements for refinement:
   - estimated SF < k × current min SF (default k = 2)
   - the top strain-energy elements
   - always: elements touching mount and load interfaces, where stress concentrates
4. **Refine** marked elements by one level (split into 8). Keep the tree 2:1 balanced
   (neighbours differ by at most one level) and add a one-element buffer around marked
   regions.
5. **Re-solve.** Hanging nodes on 2:1 faces and edges are tied to the coarse side by
   (bi)linear interpolation and eliminated during assembly (u = T û). The previous
   solution is the starting guess (warm start).
6. **Stop** when min SF changes by less than 3 % between passes and stays in the same
   place, when the hotspot reaches bead level, or when the DOF budget is used up. The UI
   shows the last change as a convergence indicator.

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

Validation:
- Cantilever vs Euler–Bernoulli (δ = FL³ / 3EI).
- Uniform-tension patch test.
- A bar printed flat vs upright reproduces the TDS XY/Z ratio.
- CalculiX on the same small mesh.
- Adaptive octree vs full bead resolution on parts small enough for both (M3).
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

- **Printer + process are fixed** to whatever Bambu Studio has selected: read
  `%APPDATA%\BambuStudio\BambuStudio.conf`, default A1 mini 0.4 / 0.20mm Standard. Bambu
  printers behave alike, so they can be changed in settings but are not part of the loop.
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

---

## 13. Milestones

Each milestone ends with something runnable. The slicer risk is retired (§2). The biggest
remaining unknown is **whether bead-resolution solves are fast enough on this laptop**, so
that comes early.

| # | Milestone | Done when |
| --- | --- | --- |
| M0 | **Scaffold**: slnx, Core / App / Cli / Tests, Helix viewport shows an STL. Install the C++ build tools. | `dotnet build` + `dotnet test` green, window shows `samples/cube20.stl` |
| M1 | **Slice & parse**: read the current Bambu selection, filament list, CLI runner with timeout and log capture, rotated-STL writer, `result.json` → placement, G-code parser incl. per-segment deposition time | `gcodefem slice model.stl --rot 0,90,0 --filament "Bambu PETG HF @BBL A1M"` prints layer and feature stats; parser tests run on the cube fixture |
| M2 | **Solver core**: block-sparse bead-cell voxelizer (φ only, isotropic), H8 assembly, C# PCG + AmgclBridge DLL, uniform bead-resolution solve | cantilever within a few % of beam theory; DOF vs time vs memory measured; this solve becomes the **reference** for M3 |
| M3 | **Octree adaptivity**: forest of root blocks, Galerkin coarsening, 2:1 balance, hanging-node constraints, downscaling, mark/refine/re-solve loop, RAM-based L suggestion | adaptive min SF (isotropic von Mises for now) within 5 % of the M2 reference using a fraction of its DOF; time per pass and coarse-level bias measured; defaults for L and k chosen |
| M4 | **Viewer**: model, toolpaths with layer slider, cells, octree overlay, result colouring | cube toolpaths coloured by feature; cantilever result and refinement shown |
| M5 | **Interfaces & loads editor**: picking, region growing, glyphs, load cases, project save/load | define a bracket's bolt holes + force in the UI and solve |
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
  thin members that bend, so a real hotspot there can look harmless. Mitigations:
  - a generous threshold k
  - interfaces are always refined
  - per-cell downscaled stresses
  - a check against full bead resolution on small parts (M3)
  - a "verify" option that refines everything when the part is small enough
- **Face-connected rasterisation of diagonal lines** adds a little extra material and
  stiffness. φ-scaling and 0°/45° coupons correct it.
- **Native dependency:** the AMGCL bridge needs MSVC to build. The C# fallback keeps
  everything working without it, just slower.
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
