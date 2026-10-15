# Celsius thermal worker review and benchmark

Date: 2026-10-01. Candidate assembly version: 2.7.0.0.

## Outcome

The final reviewed build sustained 882.8 TPS at a 900 TPS target with one-tick integration and eight workers. Thermal backlog peaked at three ticks and ended at one tick. It passed the live save/load and thermal checks below. Subsequent manual testing of the deployed build reported excellent performance and no observed regressions. Occasional benchmark frame hitches and long-term compatibility remain limits of the evidence.

The source changes are based on upstream `master` at `1f43400`. Existing reference/output paths and the Harmony package version are unchanged. Tracked DLLs are not part of the source diff.

## What changed and why

The original solver spent roughly 8.65 ms per completed solve in the matched baseline, traversed live game objects, repeatedly discarded its property cache, and could lose elapsed simulation time when busy. The refactor caches numeric coefficients, refreshes affected cells only when inputs change, uses persistent bounded workers, and publishes a completed temperature bitmap by swapping references. Heat additions are sparse and applied exactly once; elapsed ticks are retained. Room means are computed on workers.

Reciprocal exchanges now conserve sensible heat across differing cell capacities. The old double heat application, thermostat units, effective-insulation calculation, initialization, and terrain serialization issues are corrected. Heterogeneous materials intentionally behave differently from the old nonconservative solver. The phase model remains zero-latent-heat because the definitions contain no latent-heat values.

Workers calculate temperature, snow melt, and terrain/fire candidates from numeric state. The game thread performs the narrow mutations that touch shared game/renderer state. Removing worker access to shared Rand addresses a plausible cause of unstable filth geometry. Snow opacity changes skip unnecessary Things-layer invalidation while retaining hide/show and building-neighbour redraws. Vanilla section meshes remain in use.

The final review avoided a coefficient-struct copy per cell, skipped empty heat-batch work on temperature reads, and skipped clean coefficient refreshes. Full-map numerical comparisons found bit-for-bit identical output before and after the kernel change. No extra FPS/TPS gain is attributed to these small changes without a controlled comparison.

## Measurements

Copied 350 x 350 colony (122,500 cells), copied active settings, 248 normal mods plus an isolated benchmark harness, RimWorld 1.6.4871, i9-13900K. Each measured run followed about 60 seconds of warmup and measured about 40 seconds at a 900 TPS target. Four/eight workers include the coordinator and are allocated per map. No Odyssey is active.

| Build | TPS | Median frame | p95 | p99 | Worst | Max thermal backlog |
|---|---:|---:|---:|---:|---:|---:|
| Original, interval 10 | 668.6 | 45.0 ms | 54.9 ms | 58.4 ms | 265.4 ms | Not measured |
| Refactor, interval 1 / 4 workers | 893.8 | 18.3 ms | 27.7 ms | 33.6 ms | 70.2 ms | 25 |
| Refactor, interval 1 / 4 workers, repeat | 870.9 | 28.4 ms | 35.5 ms | 42.7 ms | 320.8 ms | 25 |
| Refactor, interval 1 / 8 workers | 896.7 | 14.5 ms | 24.4 ms | 30.3 ms | 88.8 ms | 4 |
| Final reviewed build, interval 1 / 8 workers | 882.8 | 18.1 ms | 27.8 ms | 35.0 ms | 320.4 ms | 3 |

Both the original baseline and final reviewed run kept the application focused throughout their sampled frames. Frame times measure intervals between Map.Update hooks, not GPU presentation latency. The final run recorded 31 of 2,146 frames above 33.3 ms (1.4%); the baseline recorded 827 of 901. A 320 ms worst frame means pacing is improved in these samples, not hitch-free.

The final run integrated 35,336 thermal ticks during 35,334 game ticks; the small difference reflects backlog at measurement boundaries. Its 19,873 jobs used 22,904 ms of worker elapsed time (about 0.648 ms per numeric step). This timer is wall time, not summed CPU time across workers. Measured coordinator tick work was 123.1 ms total and snow publication was 28.8 ms total over 40 seconds. These exclude other Celsius getter/hook/rendering costs and do not measure total main-thread cost.

The original baseline completed 2,565 solves and skipped 110 busy due-updates out of 2,675 due-updates. Its completed-solves × interval proxy represents only about 95.9% of measured game ticks; unlike the new implementation, it has no direct backlog counter.

These are short sequential runs of an evolving colony, not randomized controlled trials. Fixed wall-clock warmup leads to different game times. Earlier runs also precede small correctness/review changes. The table establishes achieved performance and variability, not an exact isolated speedup for each individual optimization. An earlier 2-worker trial and initial 4-worker trial used unintended default settings due to an isolated mod-folder naming mismatch; they are excluded.

## Validation

Temporary scripts and harnesses remain outside the repository. The numerical checks passed for unequal-capacity conservation, equilibrium, reciprocal terrain exchange, interval scaling, reservoir/source inputs, sparse heat ordering, snow accumulation, scalar/tiled equivalence, full-map pre/post-review kernel equivalence, and exact legacy save encoding/roundtrip (including malformed-length rejection).

The final live run passed:

- Pending heat is visible immediately and retained exactly once after synchronization.
- Room mean agrees with the temperature bitmap; a roof edit refreshes its coefficient.
- Enabling phase changes performs 4,843 terrain transitions with initialized terrain and finite air temperatures.
- A warm frozen cell melts through the normal worker/publication path.
- An isolated checkpoint saves and reloads the air grid exactly.
- A disabled terrain grid survives save/load, and the old map worker terminates on reload.

Filth fingerprints changed zero times at unchanged tile/thickness in both the original baseline and candidate samples. The code removes the identified thread/Rand hazard, but these runs did not reproduce the original glitch and therefore do not prove it is eliminated visually. Existing missing terrain short-hash warnings and world-pawn exceptions also occurred in the baseline; no Celsius-specific failure was found in the valid final run.

## Remaining manual checks

Watch snow-covered filth over several weather changes; inspect heating/cooling, open doors/vents, the overlay/tooltips, and autoignition. Longer sessions, several simultaneous maps, other threading mods, and all mod interactions have not been established safe by this short test. Mods changing backing arrays without normal notifications must invalidate Celsius inputs. Per-cell worker debug tracing was removed; overlay and temperature tooltips remain.

Interval settings now range from 1 to 12 (default 6); worker settings range from 1 to 8 (default 8). The thermal calibration remains a separate 120-tick constant. The tested local build uses installed Harmony 2.3.6 through an external build wrapper; the source project retains upstream Harmony 2.4.2, which is not the exact live-tested dependency combination.
