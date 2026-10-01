# Thermal worker refactor

This refactor targets upstream `master` at `1f43400`. The implementation and validation boundaries are described below; measurements are in `THERMAL-BENCHMARK.md`.

No changes to game definitions or default game balance settings are bundled here.

## Why this changes more than the renderer

The previous solver traversed live map objects, rebuilt its property cache every four requested updates, allocated parallel work, and called snow, terrain and fire mutation APIs from workers. Its busy-update branch toggled its completion flag and reset its event, losing elapsed simulation time. The published temperature array could also be modified by heat injections while a worker was reading or swapping it.

The new solver has a compact numeric input snapshot and a persistent bounded worker team. A game-thread coordinator captures inputs only when they change, sends elapsed ticks and heat edits to the worker, and publishes completed temperature arrays by swapping references. The worker never reads Map, Thing, Room, Unity, renderer, pathfinder, or global random state.

## Data and ownership

- `ThermalKernel.cs`: allocation-free finite-difference update over temperature arrays and `CellCoefficients` (four neighbour rates, environment rate, reciprocal terrain rates, source rate, two flags).

- `ThermalSnapshot.cs`: coefficients captured from game definitions and cells. Building registration/removal, door/vent state, roof, terrain, and settings changes invalidate inputs. A changed input affects its neighbouring denominators and incident edges, so the numeric refresh extends at most two cardinal steps. No periodic full-grid property-cache clear.

- `ThermalStepPool.cs`: one coordinator plus a configurable number of persistent tile workers, counting the coordinator in the setting. Each owns a contiguous row strip and writes disjoint output cells. The previous temperature bitmap is read-only for the duration of a step.

- `ThermalWorker.cs`: elapsed-time accounting, worker lifecycle, sparse heat batches, worker-computed room means, temperature publication, and game-side effects.

- `ThermalInvalidation.cs`: small Harmony hooks for changed inputs and worker shutdown when maps/game data are removed.

Three temperature arrays allow the game to retain its published bitmap while a worker integrates multiple substeps in two scratch arrays. One input copy is made on the worker per job so sparse heat edits can be applied safely. Publication does not copy the map. Terrain arrays are allocated only when that feature is enabled. Buffers and input coefficients are reused.

Snow melt is integrated on workers into cumulative per-cell counters, with dirty block notifications. The main thread consumes changed blocks once per rendered frame and calls `SnowGrid.SetDepth`, preserving tiny accumulated changes that `AddDepth` can discard. Celsius-originated snow opacity changes avoid unnecessary Things-layer invalidation; actual hide/show threshold crossings and nearby building graphics retain conservative redraws. Vanilla still owns section mesh regeneration; this does not implement a new per-tile renderer.

Terrain phase changes and fire candidates are detected on the worker. Current terrain/objects are validated and mutated on the game thread. This removes the worker's use of shared Rand, mesh dirtiness, pathing, object destruction and fire creation APIs. It also removes the pathfinder busy-spin. These handoff costs must be measured rather than assumed negligible.

## Thermal accounting

For each cell, the old model supplied directional relaxation weights. The refactor derives a directional conductance from each endpoint and uses their harmonic mean as the shared conductance. For cells i and j:

`Q(i <- j) = Gij * (Tj - Ti) * dt`

`delta Ti = Q / Ci`, `delta Tj = -Q / Cj`.

The same equal-and-opposite rule applies to air/terrain exchange. Identical cells retain the existing reference-interval calibration. Heterogeneous cells now conserve sensible heat (within floating-point rounding), rather than each applying an unrelated weighted relaxation. This intentionally changes some wall/air/terrain behaviour relative to upstream.

All rates use game ticks and the 120-tick reference interval exactly once. The requested interval controls integration resolution, not the physical conductivity. A stability bound further splits steps so a source-free update is a convex combination and cannot overshoot equilibrium. Busy workers retain all elapsed ticks; later jobs integrate those ticks with substeps no larger than the requested interval. Catch-up jobs have bounded sizes so an undersized worker team cannot monopolize its input snapshot for seconds; sustained backlog is reported. High game TPS alone therefore does not prove the thermal solver is keeping up: inspect completed thermal ticks and pending backlog too.

External heat sources and ambient temperature are explicit reservoirs. Sparse heat additions are applied once; absolute temperature writes remain absolute when queued. Live queries immediately include in-flight and pending heat. The `GenTemperature.PushHeat` prefix no longer also executes vanilla after handling the heat itself. Thermostat hooks return a temperature difference rather than energy units. Open doors/vents use their effective insulation when calculating conductivity.

New map initialization retains available room temperatures (the previous missing `else` overwrote them). Terrain temperatures are retained across settings changes and saves even when the feature is disabled. Saving synchronizes outstanding work before serializing a consistent checkpoint; loading alone assigns deserialized arrays. The existing temperature-array save format is retained.

Phase changes preserve sensible heat relative to the transition temperature when the resulting terrain has a heat capacity. Definitions supply no latent heat, so this remains an explicit zero-latent-heat phase model, not a physically complete freezing simulation. Replacing/removing a material through construction is treated as exchanging material with the external world, not a closed-system energy-conservation experiment.

## Settings and build

The interval UI exposes integer values from 1 through 12 ticks, default 6. The physical reference interval remains a separate 120-tick constant. The worker setting exposes 1 through 8, default 8. Existing valid interval values are preserved; legacy out-of-range settings clamp to the supported range. The user's requested setting is 1 tick.

Build with the .NET SDK and .NET Framework 4.8 reference assemblies, using the project's existing relative game-reference layout:

```powershell
dotnet build Source/Celsius.csproj -c Release
```

The maintainer's relative game/Unity HintPaths, output directory, and Harmony 2.4.2 dependency are unchanged. Upstream already marks game/Unity references `<Private>False</Private>`; the clean base retains this. Harmony now excludes runtime assets from the build output. The project generates assembly version 2.7.0.0 and enables the pinned-array numeric kernel. No tracked DLL changes are included. Upstream's existing tracked Harmony binary is left as-is; a local deployment of this change replaces only Celsius.dll.

The tested local DLL was compiled through an external SDK wrapper referencing the installed game and Harmony 2.3.6. That wrapper and its machine-specific paths are outside this repository. The upstream package configuration is retained for the maintainer, but that separate dependency combination has not been live-tested here.

The final review added three small fast paths: access coefficients through a pinned pointer instead of copying their struct per cell, return immediately for empty heat batches, and skip coefficient refresh when no inputs are dirty. The numeric kernel remained bit-for-bit identical to the pre-review kernel in the full-map comparison.

## Validation and boundaries

Temporary benchmark and numerical test scripts live outside this repository. Numerical checks cover unequal-capacity conservation, equilibrium, reciprocal terrain exchange, interval scaling, reservoir/source input, sparse heat batch ordering, snow accumulation and scalar/tiled equivalence.

Live measurements use a copy of the user's 350 x 350 colony, copied settings and an isolated game launcher/mod directory. Benchmarks do not write the original saves, Workshop DLLs, or normal active mod list. Deployment is a separate explicit action after validation. See `THERMAL-BENCHMARK.md` for measured results, errors and limits.

Mods that bypass the patched terrain/roof/thing APIs and mutate backing arrays directly must explicitly invalidate affected cells or call `ResetAllThings`. Short benchmarks do not establish compatibility with every mod, long-term thermal balance, or Odyssey. Odyssey is not active in the validation colony.
