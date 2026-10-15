using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using RimWorld;
using Verse;

namespace Celsius
{
    public partial class TemperatureInfo
    {
        readonly AutoResetEvent wake = new AutoResetEvent(false);
        readonly ManualResetEvent done = new ManualResetEvent(true);
        Thread worker;
        ThermalStepPool pool;
        int poolWorkers;
        volatile bool stopping;
        int state; // 0 idle, 1 running, 2 complete, 3 faulted; release/acquire via Volatile.
        Exception workerError;
        float[] bufferA, bufferB, groundA, groundB, result, groundResult;
        HeatBatch pending, flight;
        int elapsedTicks, jobTicks;
        float jobOutdoor, jobRain, jobStep;
        double[] snowMelt, snowApplied;
        int[] snowBlocks;
        readonly List<int> phaseCells = new List<int>(), ignitionCells = new List<int>();
        internal long CompletedJobs, IntegratedTicks, NumericSteps, WorkerTimestampTicks;
        internal int PendingTicks => elapsedTicks + (Volatile.Read(ref state) == 0 ? 0 : jobTicks);
        internal long MainTimestampTicks, SnowTimestampTicks;

        sealed class RoomSample
        {
            internal int[] Cells;
            internal float Temperature;
            internal int Generation;
        }
        int publishedGeneration;
        readonly Dictionary<int, RoomSample> rooms = new Dictionary<int, RoomSample>();
        RoomSample[] roomOwners;
        RoomSample[] jobRooms = new RoomSample[0];
        float[] roomResults = new float[0];
        bool roomsDirty, roomListDirty;

        void StartWorker()
        {
            int n = temperatures.Length;
            bufferA = new float[n]; bufferB = new float[n];
            pending = new HeatBatch(n); flight = new HeatBatch(n);
            snowMelt = new double[n]; snowApplied = new double[n]; snowBlocks = new int[(n + 255) >> 8];
            AllocateSnapshot(n);
            RefreshSnapshot();
            poolWorkers = Settings.WorkerThreads;
            pool = new ThermalStepPool(mapWidth, map.Size.z, poolWorkers);
            worker = new Thread(TemperatureUpdateThreadMethod) { IsBackground = true, Name = "Celsius numeric solver" };
            worker.Start();
        }

        void TemperatureUpdateThreadMethod()
        {
            while (true)
            {
                wake.WaitOne();
                if (stopping) return;
                try
                {
                    long start = Stopwatch.GetTimestamp();
                    Array.Copy(temperatures, bufferA, temperatures.Length);
                    flight.Apply(bufferA);
                    float[] a = bufferA, b = bufferB, ga = groundA, gb = groundB;
                    if (ga != null) Array.Copy(terrainTemperatures, ga, ga.Length);
                    float remaining = jobTicks;
                    long steps = 0;
                    while (remaining > 0)
                    {
                        if (stopping) return;
                        float dt = Math.Min(remaining, jobStep);
                        pool.Step(a, b, ga, gb, coefficients, dt, jobOutdoor, jobRain, snowMelt, snowBlocks);
                        float[] swap = a; a = b; b = swap;
                        swap = ga; ga = gb; gb = swap;
                        remaining -= dt; steps++;
                    }
                    result = a; bufferB = b; groundResult = ga; groundB = gb;
                    for (int r = 0; r < jobRooms.Length; r++)
                    {
                        int[] cells = jobRooms[r].Cells; double sum = 0;
                        for (int k = 0; k < cells.Length; k++) sum += result[cells[k]];
                        roomResults[r] = (float)(sum / cells.Length);
                    }
                    phaseCells.Clear(); ignitionCells.Clear();
                    // Candidate detection is numeric. Recheck the current definitions on the main thread.
                    if (jobPhases || jobIgnition)
                        for (int i = 0; i < result.Length; i++)
                        {
                            if (jobPhases && coefficients[i].Terrain > 0 &&
                                ((phaseDirection[i] == 1 && groundResult[i] < phaseThreshold[i]) ||
                                 (phaseDirection[i] == 2 && groundResult[i] > phaseThreshold[i]))) phaseCells.Add(i);
                            if (jobIgnition && result[i] > MinIgnitionTemperature) ignitionCells.Add(i);
                        }
                    Interlocked.Add(ref WorkerTimestampTicks, Stopwatch.GetTimestamp() - start);
                    Interlocked.Add(ref NumericSteps, steps);
                    Volatile.Write(ref state, 2);
                }
                catch (Exception e) { workerError = e; Volatile.Write(ref state, 3); }
                finally { done.Set(); }
            }
        }
        int mapWidth;
        bool jobPhases, jobIgnition;
        byte[] phaseDirection;
        float[] phaseThreshold;

        void Dispatch()
        {
            if (state != 0 || elapsedTicks <= 0 || stopping) return;
            RefreshSnapshot();
            if (poolWorkers != Settings.WorkerThreads)
            {
                pool.Dispose(); poolWorkers = Settings.WorkerThreads;
                pool = new ThermalStepPool(mapWidth, map.Size.z, poolWorkers);
            }
            if (roomsDirty) { rooms.Clear(); Array.Clear(roomOwners, 0, roomOwners.Length); roomListDirty = true; roomsDirty = false; }
            if (roomListDirty)
            {
                jobRooms = rooms.Values.ToArray(); roomResults = new float[jobRooms.Length]; roomListDirty = false;
            }
            if (Settings.FreezingAndMeltingEnabled && terrainTemperatures != null)
            {
                if (groundA == null) { groundA = new float[temperatures.Length]; groundB = new float[temperatures.Length]; }
            }
            else groundA = groundB = null;
            int interval = Math.Max(1, Settings.TicksPerUpdate);
            int limit = interval * Math.Max(1, 16 / interval);
            jobTicks = Math.Min(elapsedTicks, limit); elapsedTicks -= jobTicks;
            jobOutdoor = map.mapTemperature.OutdoorTemp; jobRain = map.weatherManager.RainRate > 0 ? 2 : 1;
            // Keep the requested resolution even when behind; never hide dropped physics behind high TPS.
            jobStep = Math.Min(Math.Max(1, Settings.TicksPerUpdate), maximumRate > 0 ? 0.9f / maximumRate : 1000);
            jobPhases = Settings.FreezingAndMeltingEnabled && terrainTemperatures != null;
            jobIgnition = Settings.AutoignitionEnabled;
            HeatBatch swap = flight; flight = pending; pending = swap; pending.Clear();
            done.Reset(); Volatile.Write(ref state, 1); wake.Set();
        }

        void Publish()
        {
            int status = Volatile.Read(ref state);
            if (status == 3)
            {
                // Fail visibly once. Keep the last complete state and all pending heat; do not run a
                // silently broken solver or retry a deterministic exception on every tick.
                if (!stopping) Log.Error("[Celsius] Thermal worker stopped: " + workerError);
                stopping = true;
                return;
            }
            if (status != 2) return;
            done.WaitOne(); // Completion includes the worker's finally block before reusing the event.
            // The worker no longer owns any of these arrays. Swap references; no full-map copy here.
            float[] old = temperatures; temperatures = result; bufferA = old;
            if (groundResult != null) { old = terrainTemperatures; terrainTemperatures = groundResult; groundA = old; }
            flight.Clear();
            CompletedJobs++; IntegratedTicks += jobTicks; jobTicks = 0;
            publishedGeneration++;
            for (int r = 0; r < jobRooms.Length; r++)
            {
                jobRooms[r].Temperature = roomResults[r]; jobRooms[r].Generation = publishedGeneration;
            }
            // Queries must immediately include heat that arrived while the solve was running.
            for (int k = 0; k < pending.Count; k++)
            {
                int i = pending.Cells[k]; RoomSample room = roomOwners[i];
                if (room != null && room.Generation == publishedGeneration)
                    room.Temperature += (pending.At(i, temperatures[i]) - temperatures[i]) / room.Cells.Length;
            }
            minMaxTemperaturesUpdated = false;
            if (Find.PlaySettings.showTemperatureOverlay && Find.CurrentMap == map)
            {
                if (CompletedJobs % MinMaxTemperatureResetInterval == 0) ResetMinMaxTemperature();
                overlayDrawer.SetDirty();
            }
            ApplyWorldEffects();
            Volatile.Write(ref state, 0);
        }
        void Synchronize()
        {
            if (!initialized) return;
            if (Volatile.Read(ref state) == 1) done.WaitOne();
            Publish();
            while (elapsedTicks > 0 && !stopping) { Dispatch(); done.WaitOne(); Publish(); }
            if (state == 3) throw new InvalidOperationException("Cannot save a consistent Celsius checkpoint after a thermal worker failure.", workerError);
            // Safe checkpoint: no worker owns this array.
            if (state == 0) { pending.Apply(temperatures); pending.Clear(); }
            FlushSnow();
        }
        internal void StopWorker()
        {
            stopping = true; wake.Set();
            if (worker != null && worker != Thread.CurrentThread) worker.Join();
            pool?.Dispose(); pool = null;
        }

        public override void MapComponentTick()
        {
            long start = Stopwatch.GetTimestamp();
            if (!initialized) FinalizeInit();
            Publish();
            elapsedTicks++;
            if (elapsedTicks >= Math.Max(1, Settings.TicksPerUpdate)) Dispatch();
            if (PendingTicks > 1800 && --lagWarningCountdown <= 0)
            {
                Log.Warning("[Celsius] Thermal simulation is " + PendingTicks + " ticks behind. Increase the update interval or thermal worker count.");
                lagWarningCountdown = 3600;
            }
            MainTimestampTicks += Stopwatch.GetTimestamp() - start;
        }
        int lagWarningCountdown;

        void FlushSnow()
        {
            if (snowBlocks == null) return;
            long start = Stopwatch.GetTimestamp();
            for (int block = 0; block < snowBlocks.Length; block++)
            {
                if (Interlocked.Exchange(ref snowBlocks[block], 0) == 0) continue;
                int end = Math.Min(snowMelt.Length, (block + 1) << 8);
                for (int i = block << 8; i < end; i++)
                {
                    double cumulative = Volatile.Read(ref snowMelt[i]);
                    double amount = cumulative - snowApplied[i];
                    if (amount <= 0) continue;
                    IntVec3 cell = map.cellIndices.IndexToCell(i);
                    float old = map.snowGrid.GetDepth(cell);
                    if (old <= 0) { snowApplied[i] = cumulative; continue; }
                    float depth = (float)Math.Max(0, old - amount);
                    if (depth == old) continue; // Retain sub-ULP melt until it can be represented.
                    SnowMeshUpdates.CelsiusPublication = true;
                    try { map.snowGrid.SetDepth(cell, depth); } // Game thread only: Rand/renderer/pathing are safe.
                    finally { SnowMeshUpdates.CelsiusPublication = false; }
                    snowApplied[i] += old - depth;
                    if (depth == 0) snowApplied[i] = cumulative;
                }
            }
            SnowTimestampTicks += Stopwatch.GetTimestamp() - start;
        }

        void ApplyWorldEffects()
        {
            if (!isPathfinderUpdating && Settings.FreezingAndMeltingEnabled && groundResult != null)
                foreach (int i in phaseCells)
                {
                    IntVec3 cell = map.cellIndices.IndexToCell(i);
                    TerrainDef before = map.terrainGrid.TerrainAt(i);
                    TerrainThermalProperties p = before.GetTerrainThermalProperties();
                    float t = terrainTemperatures[i];
                    if (p == null) continue;
                    float oldCapacity = p.GetThermalProps().heatCapacity;
                    if (p.FreezesAt(t)) cell.FreezeTerrain(map);
                    else if (p.MeltsAt(t)) cell.MeltTerrain(map);
                    else continue;
                    TerrainDef after = map.terrainGrid.TerrainAt(i);
                    float newCapacity = after.GetTerrainThermalProperties()?.GetThermalProps().heatCapacity ?? 0;
                    if (before != after && newCapacity > 0)
                        terrainTemperatures[i] = p.transitionTemperature + (t - p.transitionTemperature) * oldCapacity / newCapacity;
                    InvalidateCell(i);
                }
            if (Settings.AutoignitionEnabled) foreach (int i in ignitionCells) ApplyIgnition(i);
        }

        void ApplyIgnition(int i)
        {
            if (!map.terrainGrid.TerrainAt(i).Flammable()) return;
            float temperature = GetTemperatureForCell(i), fireSize = 0;
            Fire existing = null;
            List<Thing> things = map.thingGrid.ThingsListAtFast(i);
            foreach (Thing thing in things)
            {
                if (thing.FireBulwark) return;
                if (thing is Fire fire) { existing = fire; fireSize -= fire.fireSize; continue; }
                float ignition = thing.GetStatValue(DefOf.Celsius_IgnitionTemperature);
                if (ignition >= MinIgnitionTemperature && temperature >= ignition)
                    fireSize += Fire.MinFireSize * thing.GetStatValue(StatDefOf.Flammability);
            }
            if (fireSize <= 0) return;
            if (existing != null) existing.fireSize += fireSize;
            else FireUtility.TryStartFireIn(map.cellIndices.IndexToCell(i), map, fireSize, null);
        }
    }
}
