using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HotSwap;
using UnityEngine;
using Verse;
using Object = System.Object;

namespace Celsius
{
    [HotSwappable]
    public class TemperatureInfo : MapComponent
    {
        // Normal full updates between rare updates
        public const int RareUpdateInterval = 4;

        // Minimum allowed temperature for autoignition
        const float MinIgnitionTemperature = 100;

        // How quickly min & max temperatures for temperature overlay adjust
        const float MinMaxTemperatureAdjustmentStep = 1;

        int zonesPerThread;
        Task[] tasks, bufferTasks;

        //Which ticking strategy to use
        public Action TickStrategy;
        //The thread columns for this map
        Tuple<int, int>[] columnsRegular, columnsBuffer;
        private object mutex = new object();

        int tick;
        int slice;
        int rareUpdateCounter;

        float[] temperatures;
        float[] terrainTemperatures;
        ThermalProps[] thermalProperties;
        Dictionary<int, float> roomTemperatures = new Dictionary<int, float>();
        float mountainTemperature;
        float outdoorSnowMeltRate;

        static float minComfortableTemperature = TemperatureTuning.DefaultTemperature - 5, maxComfortableTemperature = TemperatureTuning.DefaultTemperature + 5;
        static readonly Color minColor = Color.blue;
        static readonly Color minComfortableColor = new Color(0, 1, 0.5f);
        static readonly Color maxComfortableColor = new Color(0.5f, 1, 0);
        static readonly Color maxColor = Color.red;

        float[] minTemperatures = new float[Settings.SliceCount];
        float[] maxTemperatures = new float[Settings.SliceCount];
        float minTemperature = minComfortableTemperature - 10;
        float maxTemperature = maxComfortableTemperature + 10;
        CellBoolDrawer overlayDrawer;

#if DEBUG
        Stopwatch updateStopwatch = new Stopwatch(), totalStopwatch = new Stopwatch();
        int tickIterations, totalTicks;
#endif

        public TemperatureInfo(Map map)
            : base(map)
        { }

        public override void FinalizeInit()
        {
            SetupStrategy();

            thermalProperties = new ThermalProps[map.Size.x * map.Size.z];
            mountainTemperature = GetMountainTemperatureFor(Settings.MountainTemperatureMode);

            // Setting up min & max temperatures (for overlay)
            minComfortableTemperature = ThingDefOf.Human.GetStatValueAbstract(StatDefOf.ComfyTemperatureMin);
            maxComfortableTemperature = ThingDefOf.Human.GetStatValueAbstract(StatDefOf.ComfyTemperatureMax);
            for (int i = 0; i < Settings.SliceCount; i++)
            {
                minTemperatures[i] = minComfortableTemperature - 10;
                maxTemperatures[i] = maxComfortableTemperature + 10;
            }

            // Initializing for the first run
            if (temperatures == null)
            {
                LogUtility.Log($"Initializing temperatures for {map} for the first time.", LogLevel.Important);
                temperatures = new float[map.Size.x * map.Size.z];
                float outdoorTemperature = map.mapTemperature.OutdoorTemp;
                for (int i = 0; i < temperatures.Length; i++)
                {
                    IntVec3 cell = map.cellIndices.IndexToCell(i);
                    Room room = cell.GetRoomOrAdjacent(map);
                    if (room != null)
                        roomTemperatures[room.ID] = temperatures[i] = room.TempTracker.Temperature;
                    else temperatures[i] = GetEnvironmentTemperature(cell.GetRoof(map));
                    if (temperatures[i] < minTemperature)
                        minTemperature = temperatures[i];
                    else if (temperatures[i] > maxTemperature)
                        maxTemperature = temperatures[i];
                }
                InitializeTerrainTemperatures();
            }
            else
            {
                minTemperature = Mathf.Min(temperatures);
                maxTemperature = Mathf.Max(temperatures);
            }

            overlayDrawer = new CellBoolDrawer(
                index => !map.fogGrid.IsFogged(index),
                () => Color.white,
                index => TemperatureColorForCell(index),
                map.Size.x,
                map.Size.z);

            tick = (Find.TickManager.TicksGame - map.generationTick) % Settings.TicksPerSlice;
            slice = (Find.TickManager.TicksGame - map.generationTick) / Settings.TicksPerSlice % Settings.SliceCount;
            LogUtility.Log($"TemperatureInfo initialized for {map}.");
        }

        public void SetupStrategy()
        {
            if (Settings.Threading)
            {
                if (Settings.UseComplexThreading)
                {
                    SetupThreadZonesExperimental();
                    TickStrategy = TickStrategyMultiThreadedExperimental;
                    return;
                }
                SetupThreadZones();
                TickStrategy = TickStrategyMultiThreaded;
                return;
            }
            TickStrategy = TickStrategySingleThreaded;
        }

        public void InitializeTerrainTemperatures()
        {
            if (!Settings.FreezingAndMeltingEnabled)
                return;
            LogUtility.Log($"Initializing terrain temperatures for {map}.");
            float snowDepth = map.GetAverageSnowDepth();
            if (terrainTemperatures == null)
                terrainTemperatures = new float[temperatures.Length];
            bool hasTerrainTemperatures = false;
            for (int i = 0; i < terrainTemperatures.Length; i++)
            {
                IntVec3 cell = map.cellIndices.IndexToCell(i);
                TerrainDef terrain = cell.GetTerrain(map);
                if (terrain.HasTemperature())
                {
                    hasTerrainTemperatures = true;
                    terrainTemperatures[i] = map.mapTemperature.SeasonalTemp;
                    if (terrain.ShouldFreeze(terrainTemperatures[i]))
                    {
                        cell.FreezeTerrain(map);
                        if (snowDepth > 0.0001f && !cell.Roofed(map))
                            map.steadyEnvironmentEffects.AddFallenSnowAt(cell, snowDepth);
                    }
                    else if (terrain.ShouldMelt(terrainTemperatures[i]))
                        cell.MeltTerrain(map);
                }
                else terrainTemperatures[i] = float.NaN;
            }
            if (!hasTerrainTemperatures)
            {
                LogUtility.Log("The map has no terrain temperatures.");
                terrainTemperatures = null;
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            string version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString(3);
            Scribe_Values.Look(ref version, "version");

            string str = DataUtility.ArrayToString(temperatures);
            Scribe_Values.Look(ref str, "temperatures");
            if (str != null)
                temperatures = DataUtility.StringToArray(str);

            if (Settings.FreezingAndMeltingEnabled)
            {
                str = DataUtility.ArrayToString(terrainTemperatures);
                Scribe_Values.Look(ref str, "terrainTemperatures");
                if (str != null)
                    terrainTemperatures = DataUtility.StringToArray(str);
            }

            // Transpose temperature arrays from pre-2.0 Celsius to adapt to the new format
            if (version == null && Scribe.mode == LoadSaveMode.LoadingVars)
            {
                LogUtility.Log($"Loaded map {map} is from pre-2.0 Celsius. Converting it to the new data format.");
                temperatures = DataUtility.Transpose(temperatures, map.Size.x);
                if (terrainTemperatures != null)
                    terrainTemperatures = DataUtility.Transpose(terrainTemperatures, map.Size.x);
            }
        }

        public override void MapRemoved()
        {
            base.MapRemoved();
            TemperatureUtility.temperatureInfos.Remove(map);
            LogUtility.Log($"Map {map} removed. TemperatureInfo cache now contains {TemperatureUtility.temperatureInfos.Count} records.");
        }

        public void ResetAllThings()
        {
            List<Thing> things = map.listerThings.AllThings;
            for (int i = 0; i < things.Count; i++)
                things[i].TryGetComp<CompThermal>()?.Reset();
        }

        Color TemperatureColorForCell(int index)
        {
            if (Settings.UseVanillaTemperatureColors)
                return map.mapTemperature.GetCellExtraColor(index);
            float temperature = GetTemperatureForCell(index);
            if (temperature < minComfortableTemperature)
                return Color.Lerp(minColor, minComfortableColor, (temperature - minTemperature) / (minComfortableTemperature - minTemperature));
            if (temperature < maxComfortableTemperature)
                return Color.Lerp(minComfortableColor, maxComfortableColor, (temperature - minComfortableTemperature) / (maxComfortableTemperature - minComfortableTemperature));
            return Color.Lerp(maxComfortableColor, maxColor, (temperature - maxComfortableTemperature) / (maxTemperature - maxComfortableTemperature));
        }

        public override void MapComponentUpdate()
        {
            if (Find.PlaySettings.showTemperatureOverlay && Find.CurrentMap == map)
                overlayDrawer.MarkForDraw();
            overlayDrawer.CellBoolDrawerUpdate();
        }

        public override void MapComponentOnGUI()
        {
#if DEBUG
            if (Prefs.DevMode && Settings.DebugMode && Find.TickManager.CurTimeSpeed != TimeSpeed.Ultrafast && totalStopwatch.IsRunning)
                totalStopwatch.Stop();
#endif
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == DefOf.Celsius_SwitchTemperatureMap.MainKey && Find.MainTabsRoot.OpenTab == null)
                Find.PlaySettings.showTemperatureOverlay = !Find.PlaySettings.showTemperatureOverlay;
            if (!Find.PlaySettings.showTemperatureOverlay || !Settings.ShowTemperatureTooltip)
                return;
            IntVec3 cell = UI.MouseCell();
            if (cell.InBounds(map) && (!cell.Fogged(map) || Prefs.DevMode))
            {
                GameFont font = Text.Font;
                Text.Font = GameFont.Tiny;
                string tooltip = "Celsius_MapTempOverlay_Cell".Translate(GetTemperatureForCell(cell).ToStringTemperature(Settings.TemperatureDisplayFormatString));
                if (Settings.FreezingAndMeltingEnabled && HasTerrainTemperatures)
                {
                    float terrainTemperature = GetTerrainTemperature(cell);
                    if (!float.IsNaN(terrainTemperature))
                        tooltip += "\n" + "Celsius_MapTempOverlay_Terrain".Translate(terrainTemperature.ToStringTemperature(Settings.TemperatureDisplayFormatString));
                }
#if DEBUG
                Widgets.Label(new Rect(UI.MousePositionOnUIInverted.x + 20, UI.MousePositionOnUIInverted.y + 20, 150, 100), $"{tooltip}\n{GetThermalPropertiesAt(map.cellIndices.CellToIndex(cell))}");

#else
                Widgets.Label(new Rect(UI.MousePositionOnUIInverted.x + 20, UI.MousePositionOnUIInverted.y + 20, 100, 40), tooltip);
#endif
                Text.Font = font;
            }
        }

        public override void MapComponentTick()
        {
#if DEBUG
            if (Settings.DebugMode && Find.TickManager.CurTimeSpeed == TimeSpeed.Ultrafast)
            {
                if (++totalTicks % 500 == 0)
                    LogUtility.Log($"Total ultrafast ticks: {totalTicks}. Average time/1000 ticks: {1000 * totalStopwatch.ElapsedMilliseconds / totalTicks} ms.");
                totalStopwatch.Start();
            }
#endif

            if (++tick < Settings.TicksPerSlice)
                return;
#if DEBUG
            updateStopwatch.Start();
#endif

            if (slice == 0)
            {
                roomTemperatures.Clear();
                if (rareUpdateCounter == 0)
                {
                    mountainTemperature = GetMountainTemperatureFor(Settings.MountainTemperatureMode);
                    outdoorSnowMeltRate = map.weatherManager.RainRate > 0 ? Settings.SnowMeltCoefficientRain : Settings.SnowMeltCoefficient;
                    thermalProperties = new ThermalProps[map.Size.x * map.Size.z];
                }
            }

            if (minTemperatures[slice] < minComfortableTemperature + 10)
                minTemperatures[slice] += MinMaxTemperatureAdjustmentStep;
            if (maxTemperatures[slice] > maxComfortableTemperature - 10)
                maxTemperatures[slice] -= MinMaxTemperatureAdjustmentStep;

            // Main loop
            TickStrategy();

            tick = 0;
            if (slice == 0)
            {
                rareUpdateCounter = (rareUpdateCounter + 1) % RareUpdateInterval;
                minTemperature = Mathf.Min(minTemperatures);
                maxTemperature = Mathf.Max(maxTemperatures);
                overlayDrawer.SetDirty();
            }
            slice = (slice + 1) % Settings.SliceCount;
#if DEBUG
            if (Settings.DebugMode)
            {
                updateStopwatch.Stop();
                if (slice == 0 && ++tickIterations % 10 == 0)
                    LogUtility.Log($"Updated temperatures for {map} on tick {Find.TickManager.TicksGame} in {updateStopwatch.Elapsed.TotalMilliseconds / tickIterations:F1} ms.");
            }
#endif
        }

        public void TickStrategySingleThreaded()
        {
            bool log;
            int mouseCell = Prefs.DevMode && Settings.DebugMode && Find.PlaySettings.showTemperatureOverlay ? map.cellIndices.CellToIndex(UI.MouseCell()) : -1;
            for (int j = slice; j < temperatures.Length; j += Settings.SliceCount)
            {
                IntVec3 cell = map.cellsInRandomOrder.Get(j);
                int i = map.cellIndices.CellToIndex(cell);
                log = i == mouseCell;
                float temperature = temperatures[i];
                ThermalProps cellProps = GetThermalPropertiesAt(i);
                float energy = 0;
                float heatFlow = cellProps.HeatFlow;

                // Terrain temperature
                if (Settings.FreezingAndMeltingEnabled && HasTerrainTemperatures)
                {
                    float terrainTemperature = terrainTemperatures[i];
                    if (!float.IsNaN(terrainTemperature))
                    {
                        TerrainDef terrain = cell.GetTerrain(map);
                        ThermalProps terrainProps = terrain?.GetModExtension<ThingThermalProperties>()?.GetThermalProps();
                        if (terrainProps != null && terrainProps.heatCapacity > 0)
                        {
                            // Thermal exchange with terrain
                            TemperatureUtility.CalculateHeatTransferTerrain(temperature, terrainTemperature, terrainProps, ref energy, ref heatFlow);
                            float terrainTempChange = (temperature - terrainTemperature) * cellProps.HeatFlow / heatFlow;
                            if (log)
                                LogUtility.Log($"Terrain temperature: {terrainTemperature:F1}C. Terrain heat capacity: {terrainProps.heatCapacity}. Terrain heatflow: {terrainProps.HeatFlow:P0}. Equilibrium temperature: {terrainTemperature + terrainTempChange:F1}C.");
                            terrainTemperature += terrainTempChange * terrainProps.Conductivity;

                            // Melting or freezing if terrain temperature has crossed respective melt/freeze points (upwards or downwards)
                            if (terrainTemperatures[i] < FreezeMeltUtility.MeltTemperature && terrain.ShouldMelt(terrainTemperature))
                                cell.MeltTerrain(map, log);
                            else if (terrainTemperatures[i] > FreezeMeltUtility.FreezeTemperature && terrain.ShouldFreeze(terrainTemperature))
                                cell.FreezeTerrain(map, log);

                            terrainTemperatures[i] = terrainTemperature;
                        }
                        else terrainTemperatures[i] = float.NaN;
                    }
                    // Rarely checking if a cell now has terrain temperature (e.g. when a bridge has been removed)
                    else if (rareUpdateCounter == 0 && cell.GetTerrain(map).HasTemperature())
                        terrainTemperatures[i] = temperature;
                }

                // Diffusion & convection
                void ProcessNeighbour(IntVec3 neighbour)
                {
                    if (neighbour.InBounds(map))
                    {
                        int index = map.cellIndices.CellToIndex(neighbour);
                        TemperatureUtility.CalculateHeatTransferCells(temperature, temperatures[index], GetThermalPropertiesAt(index), cellProps.airflow, ref energy, ref heatFlow, log);
                    }
                }

                ProcessNeighbour(cell + IntVec3.North);
                ProcessNeighbour(cell + IntVec3.East);
                ProcessNeighbour(cell + IntVec3.South);
                ProcessNeighbour(cell + IntVec3.West);

                // Thermal exchange with the environment
                RoofDef roof = cell.GetRoof(map);
                TemperatureUtility.CalculateHeatTransferEnvironment(temperature, GetEnvironmentTemperature(roof), cellProps, roof != null, ref energy, ref heatFlow);

                // Applying heat transfer
                float equilibriumDifference = energy / heatFlow;
                if (log)
                    LogUtility.Log($"Total cell + neighbours energy: {energy:F4}. Total heat flow rate: {heatFlow:F4}. Equilibrium temperature: {temperature + equilibriumDifference:F1}C.");

                temperature += equilibriumDifference * cellProps.Conductivity;
                temperatures[i] = temperature;

                // Snow melting
                if (temperature > 0 && cell.GetSnowDepth(map) > 0)
                {
                    if (log)
                        LogUtility.Log($"Snow: {cell.GetSnowDepth(map):F4}. {(cell.Roofed(map) ? "Roofed." : "Unroofed.")} Melting: {FreezeMeltUtility.SnowMeltAmountAt(temperature) * (cell.Roofed(map) ? Settings.SnowMeltCoefficient : Settings.SnowMeltCoefficientRain):F4}.");
                    map.snowGrid.AddDepth(cell, -FreezeMeltUtility.SnowMeltAmountAt(temperature) * (cell.Roofed(map) ? Settings.SnowMeltCoefficient : outdoorSnowMeltRate));
                }

                // Autoignition
                if (temperature > MinIgnitionTemperature && Settings.AutoignitionEnabled)
                {
                    Fire existingFire = null;
                    float fireSize = 0;
                    List<Thing> things = map.thingGrid.ThingsListAtFast(cell);
                    for (int k = 0; k < things.Count; k++)
                    {
                        if (things[k].FireBulwark)
                        {
                            fireSize = 0;
                            break;
                        }
                        if (things[k] is Fire fire)
                        {
                            fireSize -= fire.fireSize;
                            existingFire = fire;
                            continue;
                        }
                        float ignitionTemp = things[k].GetStatValue(DefOf.Celsius_IgnitionTemperature);
                        if (ignitionTemp >= MinIgnitionTemperature && temperature >= ignitionTemp)
                            fireSize += Fire.MinFireSize * things[k].GetStatValue(StatDefOf.Flammability);
                    }

                    if (fireSize > 0)
                        if (existingFire == null)
                        {
                            LogUtility.Log($"{things[0]} (total {things.Count.ToStringCached()} things in the cell) spontaneously ignites at {temperature:F1}C! Fire size: {fireSize:F2}.");
                            FireUtility.TryStartFireIn(cell, map, fireSize, null);
                        }
                        else existingFire.fireSize += fireSize;
                }

                if (!Settings.UseVanillaTemperatureColors)
                    if (temperature < minTemperatures[slice])
                        minTemperatures[slice] = temperature;
                    else if (temperature > maxTemperatures[slice])
                        maxTemperatures[slice] = temperature;
            }
        }

        //Processes this map's x-columns from start to end
        public void ProcessColumns(int start, int end)
        {
            for (int colIndex = start; colIndex <= end; colIndex++)
            {
                for (int rowIndex = 0; rowIndex < map.Size.z; rowIndex++)
                {
                    IntVec3 cell = new IntVec3(colIndex, 0, rowIndex);
                    int i = map.cellIndices.CellToIndex(cell);
                    float temperature = temperatures[i];
                    ThermalProps cellProps = GetThermalPropertiesAt(i);
                    float energy = 0;
                    float heatFlow = cellProps.HeatFlow;

                    // Terrain temperature
                    if (Settings.FreezingAndMeltingEnabled && HasTerrainTemperatures)
                    {
                        float terrainTemperature = terrainTemperatures[i];
                        if (!float.IsNaN(terrainTemperature))
                        {
                            TerrainDef terrain = cell.GetTerrain(map);
                            ThermalProps terrainProps = terrain?.GetModExtension<ThingThermalProperties>()?.GetThermalProps();
                            if (terrainProps != null && terrainProps.heatCapacity > 0)
                            {
                                // Thermal exchange with terrain
                                TemperatureUtility.CalculateHeatTransferTerrain(temperature, terrainTemperature, terrainProps, ref energy, ref heatFlow);
                                float terrainTempChange = (temperature - terrainTemperature) * cellProps.HeatFlow / heatFlow;
                                terrainTemperature += terrainTempChange * terrainProps.Conductivity;

                                // Melting or freezing if terrain temperature has crossed respective melt/freeze points (upwards or downwards)
                                if (terrainTemperatures[i] < FreezeMeltUtility.MeltTemperature && terrain.ShouldMelt(terrainTemperature))
                                    cell.MeltTerrain(map);
                                else if (terrainTemperatures[i] > FreezeMeltUtility.FreezeTemperature && terrain.ShouldFreeze(terrainTemperature))
                                    cell.FreezeTerrain(map);

                                terrainTemperatures[i] = terrainTemperature;
                            }
                            else terrainTemperatures[i] = float.NaN;
                        }
                        // Rarely checking if a cell now has terrain temperature (e.g. when a bridge has been removed)
                        else if (rareUpdateCounter == 0 && cell.GetTerrain(map).HasTemperature())
                            terrainTemperatures[i] = temperature;
                    }

                    // Diffusion & convection
                    void ProcessNeighbour(IntVec3 neighbour)
                    {
                        if (neighbour.InBounds(map))
                        {
                            int index = map.cellIndices.CellToIndex(neighbour);
                            TemperatureUtility.CalculateHeatTransferCells(temperature, temperatures[index], GetThermalPropertiesAt(index), cellProps.airflow, ref energy, ref heatFlow);
                        }
                    }

                    ProcessNeighbour(cell + IntVec3.North);
                    ProcessNeighbour(cell + IntVec3.East);
                    ProcessNeighbour(cell + IntVec3.South);
                    ProcessNeighbour(cell + IntVec3.West);

                    // Thermal exchange with the environment
                    RoofDef roof = cell.GetRoof(map);
                    TemperatureUtility.CalculateHeatTransferEnvironment(temperature, GetEnvironmentTemperature(roof), cellProps, roof != null, ref energy, ref heatFlow);

                    // Applying heat transfer
                    float equilibriumDifference = energy / heatFlow;

                    temperature += equilibriumDifference * cellProps.Conductivity;
                    temperatures[i] = temperature;

                    // Snow melting
                    if (temperature > 0 && cell.GetSnowDepth(map) > 0)
                    {
                        map.snowGrid.AddDepth(cell, -FreezeMeltUtility.SnowMeltAmountAt(temperature) * (cell.Roofed(map) ? Settings.SnowMeltCoefficient : outdoorSnowMeltRate));
                    }

                    // Autoignition
                    if (temperature > MinIgnitionTemperature && Settings.AutoignitionEnabled)
                    {
                        Fire existingFire = null;
                        float fireSize = 0;
                        List<Thing> things = map.thingGrid.ThingsListAtFast(cell);
                        for (int k = 0; k < things.Count; k++)
                        {
                            if (things[k].FireBulwark)
                            {
                                fireSize = 0;
                                break;
                            }
                            if (things[k] is Fire fire)
                            {
                                fireSize -= fire.fireSize;
                                existingFire = fire;
                                continue;
                            }

                            lock (mutex)
                            {
                                float ignitionTemp = things[k].GetStatValue(DefOf.Celsius_IgnitionTemperature);
                                if (ignitionTemp >= MinIgnitionTemperature && temperature >= ignitionTemp)
                                    fireSize += Fire.MinFireSize * things[k].GetStatValue(StatDefOf.Flammability);
                            }
                            
                        }

                        if (fireSize > 0)
                            if (existingFire == null)
                            {
                                LogUtility.Log($"{things[0]} (total {things.Count.ToStringCached()} things in the cell) spontaneously ignites at {temperature:F1}C! Fire size: {fireSize:F2}.");
                                lock (mutex)
                                {
                                    FireUtility.TryStartFireIn(cell, map, fireSize, null);
                                }
                            }
                            else existingFire.fireSize += fireSize;
                    }

                    if (!Settings.UseVanillaTemperatureColors)
                        if (temperature < minTemperatures[slice])
                            minTemperatures[slice] = temperature;
                        else if (temperature > maxTemperatures[slice])
                            maxTemperatures[slice] = temperature;
                }
            }
        }

        //Calculates and prepares the thread zones for the simple multithreading function
        public void SetupThreadZones()
        {
            
            //We want to perform two disjoint passes to avoid synchronization issues, so we need two columns for every thread
            int numCols = Settings.NumThreadsWorkers * 2;
            if ((map.Size.x - numCols) / Settings.NumThreadsWorkers <= 1)
            {
                //A column of size 1 isn't enough to avoid synchronization issues. Tell the user that they fucked up and get out
                Log.Message("Either your map is too small or you're using too many threads, switching to singlethreaded...");
                TickStrategy = TickStrategySingleThreaded;
                return;
            }

            tasks = new Task[Settings.NumThreadsWorkers];
            columnsRegular = new Tuple<int, int>[numCols];
            //How many columns we have left to distribute
            int colsLeft = map.Size.x - numCols;
            //Which column we're at
            int columnIndex = 0;
            //How many unallocated threads are left
            int unallocatedThreads = Settings.NumThreadsWorkers;
            for (int i = 0; i < numCols; i += 2) //Increment by 2 since we're allocating two values per iteration
            {
                //We round down if it's not an even result, worst case is that the last thread has to process a few extra columns
                int colsToAllocate = colsLeft / unallocatedThreads;
                //Allocate this slice
                columnsRegular[i] = new Tuple<int, int>(columnIndex, columnIndex + colsToAllocate - 1);
                columnIndex += colsToAllocate;
                //Allocate the buffer
                columnsRegular[i + 1] = new Tuple<int, int>(columnIndex, columnIndex + 1);
                columnIndex += 2;
                //We don't need to subtract the buffer because we already did that at the start
                colsLeft -= colsToAllocate;
                unallocatedThreads--;
            }
        }


        

        //Calculates and prepares the thread zones for the more complex multithreading function
        public void SetupThreadZonesExperimental()
        {
            //We want to perform two disjoint passes to avoid synchronization issues, so we need two columns for every thread
            int numCols = Settings.NumThreadsWorkers * 2;
            if ((map.Size.x - numCols) / Settings.NumThreadsWorkers <= 1)
            {
                //A column of size 1 isn't enough to avoid synchronization issues. Tell the user that they fucked up and get out
                LogUtility.Log("Either your map is too small or you're using too many threads, switching to single threaded...");
                TickStrategy = TickStrategySingleThreaded;
                return;
            }

            columnsRegular = new Tuple<int, int>[Settings.NumThreadsWorkers];
            columnsBuffer = new Tuple<int, int>[Settings.NumThreadsWorkers];
            //How many buffer zones we should merge and process per buffer thread.
            //TODO allocate more zones to other threads if the last thread needs to process more than two extra zones
            zonesPerThread = columnsBuffer.Length / Settings.NumThreadsBuffer;
            bufferTasks = new Task[Settings.NumThreadsBuffer];
            tasks = new Task[Settings.NumThreadsWorkers];
            //How many work zones we have left to distribute
            int colsLeft = map.Size.x - numCols;
            //Which column we're at
            int columnIndex = 0;
            //How many unallocated threads are left
            int unallocatedThreads = Settings.NumThreadsWorkers;
            for (int i = 0; i < Settings.NumThreadsWorkers; i++)
            {
                //We round down if it's not an even result, worst case is that the last thread has to process a few extra zones
                //TODO This is actually bad for buffer zones if there are too many. Performance tanks when bufferThreads can't divide workThreads
                int colsToAllocate = colsLeft / unallocatedThreads;
                //Allocate this work zone
                columnsRegular[i] = new Tuple<int, int>(columnIndex, columnIndex + colsToAllocate - 1);
                columnIndex += colsToAllocate;
                //Allocate the buffer
                columnsBuffer[i] = new Tuple<int, int>(columnIndex, columnIndex + 1);
                columnIndex += 2;
                //We don't need to subtract the buffer because we already did that at the start
                colsLeft -= colsToAllocate;
                unallocatedThreads--;
            }
        }
        
        //A simpler multithreading strategy that uses the same amount of threads for the buffer zones
        public void TickStrategyMultiThreaded()
        {
            //Run first halves
            int taskIndex = 0;
            for (int i = 0; i < columnsRegular.Length; i+=2)
            {
                var tuple = columnsRegular[i];
                tasks[taskIndex] = Task.Run(() => ProcessColumns(tuple.Item1, tuple.Item2));
                taskIndex++;
            }
            Task.WaitAll(tasks);
            taskIndex = 0;

            //Run second halves
            for (int i = 1; i < columnsRegular.Length; i += 2)
            {
                var tuple = columnsRegular[i];
                tasks[taskIndex] = Task.Run(() => ProcessColumns(tuple.Item1, tuple.Item2));
                taskIndex++;
            }
            Task.WaitAll(tasks);
        }

        //A more complex multithreading method that uses a different number of threads for the buffer zones
        public void TickStrategyMultiThreadedExperimental()
        {

            //Run first halves
            for (int i = columnsRegular.Length - 1; i >= 0; i--)
            {
                var tuple = columnsRegular[i];
                tasks[i] = Task.Run(() => ProcessColumns(tuple.Item1, tuple.Item2));
            }
            Task.WaitAll(tasks);
            
            //Process the last buffer zone first since it might be larger
            bufferTasks[bufferTasks.Length - 1] = Task.Run(() =>
            {
                for (int j = zonesPerThread * (Settings.NumThreadsBuffer - 1); j < columnsBuffer.Length; j++)
                {
                    var tuple = columnsBuffer[j];
                    ProcessColumns(tuple.Item1, tuple.Item2);
                }
            });
            for (int i = 0; i < Settings.NumThreadsBuffer - 1; i++)
            {
                bufferTasks[i] = Task.Run(() =>
                {
                    for (int j = i*zonesPerThread; j < zonesPerThread*(i+1); j++)
                    {
                        var tuple = columnsBuffer[j];
                        ProcessColumns(tuple.Item1, tuple.Item2);
                    }
                });
            }
            
            Task.WaitAll(bufferTasks);
        }

        public float GetMountainTemperatureFor(MountainTemperatureMode mode)
        {
            switch (mode)
            {
                case MountainTemperatureMode.Vanilla:
                    return TemperatureTuning.DeepUndergroundTemperature;

                case MountainTemperatureMode.AnnualAverage:
                    return Find.WorldGrid[map.Tile].temperature + Settings.MountainTemperatureOffset;

                case MountainTemperatureMode.SeasonAverage:
                    return GenTemperature.AverageTemperatureAtTileForTwelfth(map.Tile, GenLocalDate.Twelfth(map).PreviousTwelfth()) + Settings.MountainTemperatureOffset;

                case MountainTemperatureMode.AmbientAir:
                    return map.mapTemperature.OutdoorTemp + Settings.MountainTemperatureOffset;

                case MountainTemperatureMode.Manual:
                    return Settings.MountainTemperature;
            }
            return TemperatureTuning.DeepUndergroundTemperature;
        }

        public float GetEnvironmentTemperature(RoofDef roof) => roof != null && roof.isThickRoof ? mountainTemperature : map.mapTemperature.OutdoorTemp;

        public float GetTemperatureForCell(int index) => temperatures != null ? temperatures[index] : TemperatureTuning.DefaultTemperature;

        public float GetTemperatureForCell(IntVec3 cell) => GetTemperatureForCell(map.cellIndices.CellToIndex(cell));

        public float GetRoomAverageTemperature(Room room)
        {
            if (room.ID == -1 || roomTemperatures == null)
            {
                LogUtility.Log($"Could not get temperature for room {room?.ToString() ?? "null"}.", LogLevel.Error);
                return map.mapTemperature.OutdoorTemp;
            }
            if (roomTemperatures.TryGetValue(room.ID, out float temperature))
                return temperature;
            return roomTemperatures[room.ID] = room.Cells.Average(cell => GetTemperatureForCell(cell));
        }

        public bool HasTerrainTemperatures => terrainTemperatures != null;

        public float GetTerrainTemperature(IntVec3 cell) => terrainTemperatures[map.cellIndices.CellToIndex(cell)];

        public void SetTemperatureForCell(int index, float temperature) => temperatures[index] = Mathf.Max(temperature, -273);

        public void SetTemperatureForCell(IntVec3 cell, float temperature) => SetTemperatureForCell(map.cellIndices.CellToIndex(cell), temperature);

        public ThermalProps GetThermalPropertiesAt(int index)
        {
            if (thermalProperties[index] != null)
                return thermalProperties[index];
            List<Thing> thingsList = map.thingGrid.ThingsListAtFast(index);
            for (int i = thingsList.Count - 1; i >= 0; i--)
                if (CompThermal.ShouldApplyTo(thingsList[i].def))
                {
                    ThermalProps thermalProps = thingsList[i].TryGetComp<CompThermal>()?.ThermalProperties;
                    if (thermalProps != null)
                        return thermalProperties[index] = thermalProps;
                }
            return thermalProperties[index] = ThermalProps.Air;
        }

        public void PushHeat(int index, float energy) =>
          SetTemperatureForCell(index, temperatures[index] + energy * Settings.HeatPushEffect / GetThermalPropertiesAt(index).heatCapacity);

        public void PushHeat(IntVec3 cell, float energy) => PushHeat(map.cellIndices.CellToIndex(cell), energy);

        public float GetIgnitionTemperatureForCell(IntVec3 cell)
        {
            float min = 10000;
            List<Thing> things = map.thingGrid.ThingsListAtFast(cell);
            for (int i = 0; i < things.Count; i++)
            {
                if (things[i].FireBulwark)
                    return 10000;
                if (things[i].GetStatValue(StatDefOf.Flammability) > 0)
                {
                    float ignitionTemperature = things[i].GetStatValue(DefOf.Celsius_IgnitionTemperature);
                    if (ignitionTemperature >= MinIgnitionTemperature)
                        min = Mathf.Min(min, ignitionTemperature);
                }
            }
            return min;
        }
    }
}
