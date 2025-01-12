using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using RimWorld;
using Verse;

namespace Celsius
{
    public class TemperatureInfo : MapComponent
    {
        // Number of full map temperature updates between rare updates and between resets of min/max temperature
        public const int RareUpdateInterval = 4;
        public const int MinMaxTemperatureResetInterval = 20;

        // Minimum possible temperature of autoignition
        const float MinIgnitionTemperature = 100;

        bool initialized;
        int tick;
        int updateCounter;

        EventWaitHandle temperatureUpdateHandle = new ManualResetEvent(false);
        Thread temperatureUpdateThread;
        bool updated = true;

        float[] temperatures;
        float[] worksetTemperatures;
        float[] terrainTemperatures;
        ThermalProps[] thermalProperties;
        Dictionary<int, float> roomTemperatures = new Dictionary<int, float>();
        int outdoorSnowMeltRate;

        static float minComfortableTemperature = TemperatureTuning.DefaultTemperature - 5, maxComfortableTemperature = TemperatureTuning.DefaultTemperature + 5;
        static readonly Color minColor = Color.blue;
        static readonly Color minComfortableColor = new Color(0, 1, 0.25f);
        static readonly Color maxComfortableColor = new Color(0.25f, 1, 0);
        static readonly Color maxColor = Color.red;

        bool minMaxTemperaturesUpdated;
        float minTemperature, maxTemperature;
        CellBoolDrawer overlayDrawer;

        Stopwatch updateStopwatch = new Stopwatch();

        public TemperatureInfo(Map map)
            : base(map)
        { }

        public override void FinalizeInit()
        {
            thermalProperties = new ThermalProps[map.Size.x * map.Size.z];
            worksetTemperatures = new float[map.Size.x * map.Size.z];

            // Initializing for the first run
            if (temperatures == null)
            {
                LogUtility.Log($"Initializing temperatures for {map} for the first time.", LogLevel.Important);
                temperatures = new float[map.Size.x * map.Size.z];
                float outdoorTemperature = map.mapTemperature.OutdoorTemp, annualAverageTemperature = Find.WorldGrid[map.Tile].temperature;
                for (int i = 0; i < temperatures.Length; i++)
                {
                    IntVec3 cell = map.cellIndices.IndexToCell(i);
                    Room room = cell.GetRoomOrAdjacent(map);
                    if (room != null)
                        roomTemperatures[room.ID] = temperatures[i] = room.TempTracker.Temperature;
                    {
                        RoofDef roof = map.roofGrid.RoofAt(i);
                        temperatures[i] = roof != null && roof.isThickRoof ? annualAverageTemperature : outdoorTemperature;
                    }
                }
                InitializeTerrainTemperatures();
            }

            ResetSnowMeltRate();
            minComfortableTemperature = ThingDefOf.Human.GetStatValueAbstract(StatDefOf.ComfyTemperatureMin);
            maxComfortableTemperature = ThingDefOf.Human.GetStatValueAbstract(StatDefOf.ComfyTemperatureMax);
            ResetMinMaxTemperature();

            overlayDrawer = new CellBoolDrawer(
                index => !map.fogGrid.IsFogged(index),
                () => Color.white,
                index => TemperatureColorForCell(index),
                map.Size.x,
                map.Size.z);

            tick = (Find.TickManager.TicksGame - map.generationTick) % Settings.TicksPerUpdate;
            temperatureUpdateThread = new Thread(TemperatureUpdateThreadMethod);
            temperatureUpdateThread.Start();
            initialized = true;
            LogUtility.Log($"TemperatureInfo initialized for {map}.");
        }

        public void InitializeTerrainTemperatures()
        {
            if (!Settings.FreezingAndMeltingEnabled)
                return;
            LogUtility.Log($"Initializing terrain temperatures for {map}.");
            float snowDepth = map.GetAverageSnowDepth();
            if (terrainTemperatures == null)
                terrainTemperatures = new float[temperatures.Length];
            int freezes = 0, melts = 0, cellsWithTerrainTemperature = 0;
            for (int i = 0; i < terrainTemperatures.Length; i++)
            {
                IntVec3 cell = map.cellIndices.IndexToCell(i);
                TerrainDef terrain = map.terrainGrid.TerrainAt(i);
                if (terrain.HasTemperature())
                {
                    cellsWithTerrainTemperature++;
                    terrainTemperatures[i] = map.mapTemperature.SeasonalTemp;
                    if (terrain.FreezesAt(terrainTemperatures[i]))
                    {
                        cell.FreezeTerrain(map);
                        freezes++;
                        if (snowDepth > 0.0001f && !cell.Roofed(map))
                            map.steadyEnvironmentEffects.AddFallenSnowAt(cell, snowDepth);
                    }
                    else if (terrain.MeltsAt(terrainTemperatures[i]))
                    {
                        cell.MeltTerrain(map);
                        melts++;
                    }
                }
                else terrainTemperatures[i] = float.NaN;
            }
            LogUtility.Log($"{cellsWithTerrainTemperature} cells have terrain temperatures.");
            if (cellsWithTerrainTemperature == 0)
            {
                LogUtility.Log("The map has no terrain temperatures.");
                terrainTemperatures = null;
            }
            else if (freezes > 0 || melts > 0)
                LogUtility.Log($"Froze {freezes} and melted {melts} cells during map initialization.");

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
            Array.Clear(thermalProperties, 0, thermalProperties.Length);
        }

        public void ResetSnowMeltRate() => outdoorSnowMeltRate = map.weatherManager.RainRate > 0 ? 2 * Settings.TicksPerUpdate : Settings.TicksPerUpdate;

        public void ResetMinMaxTemperature()
        {
            minTemperature = minComfortableTemperature - 10;
            maxTemperature = maxComfortableTemperature + 10;
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
            {
                if (!minMaxTemperaturesUpdated && !Settings.UseVanillaTemperatureColors)
                {
                    for (int i = 0; i < temperatures.Length; i++)
                        if (temperatures[i] < minTemperature)
                            minTemperature = temperatures[i];
                        else if (temperatures[i] > maxTemperature)
                            maxTemperature = temperatures[i];
                    LogUtility.Log($"Color temperatures: {minTemperature.ToStringTemperature()}..{maxTemperature.ToStringTemperature()}");
                    minMaxTemperaturesUpdated = true;
                }
                overlayDrawer.MarkForDraw();
            }
            overlayDrawer.CellBoolDrawerUpdate();
        }

        public override void MapComponentOnGUI()
        {
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == DefOf.Celsius_SwitchTemperatureMap.MainKey && Find.MainTabsRoot.OpenTab == null)
            {
                Find.PlaySettings.showTemperatureOverlay = !Find.PlaySettings.showTemperatureOverlay;
                Event.current.Use();
            }
            if (!Find.PlaySettings.showTemperatureOverlay || !Settings.ShowTemperatureTooltip)
                return;
            int index = map.cellIndices.CellToIndex(UI.MouseCell());
            if (InBounds(index) && (!map.fogGrid.IsFogged(index) || Prefs.DevMode))
            {
                GameFont font = Text.Font;
                Text.Font = GameFont.Tiny;
                string tooltip = "Celsius_MapTempOverlay_Cell".Translate(GetTemperatureForCell(index).ToStringTemperature(Settings.TemperatureDisplayFormatString));
                if (Settings.FreezingAndMeltingEnabled && HasTerrainTemperatures)
                {
                    float terrainTemperature = terrainTemperatures[index];
                    if (!float.IsNaN(terrainTemperature))
                        tooltip += "\n" + "Celsius_MapTempOverlay_Terrain".Translate(terrainTemperature.ToStringTemperature(Settings.TemperatureDisplayFormatString));
                }

                if (Settings.DebugMode)
                    Widgets.Label(new Rect(UI.MousePositionOnUIInverted.x + 20, UI.MousePositionOnUIInverted.y + 20, 150, 100), $"{tooltip}\n{GetThermalPropertiesAt(index)}");
                else Widgets.Label(new Rect(UI.MousePositionOnUIInverted.x + 20, UI.MousePositionOnUIInverted.y + 20, 100, 40), tooltip);

                Text.Font = font;
            }
        }

        void TemperatureUpdateThreadMethod()
        {
            while (true)
            {
                temperatureUpdateHandle.WaitOne();
                
                if (Settings.DebugMode)
                    updateStopwatch.Start();

                float outdoorTemperature = map.mapTemperature.OutdoorTemp;
                int mouseCell = Prefs.DevMode && Settings.DebugMode && Find.PlaySettings.showTemperatureOverlay
                    ? map.cellIndices.CellToIndex(UI.MouseCell())
                    : -1;
                bool updateTerrainTemperature = Settings.FreezingAndMeltingEnabled && HasTerrainTemperatures;
                bool log;

                // Main loop
                var partitioner = Partitioner.Create(0, temperatures.Length);
                var parallelLoopResult = Parallel.ForEach(partitioner, range =>
                {
                    for (int i = range.Item1; i < range.Item2; i++)
                    {
                        IntVec3 cell = map.cellIndices.IndexToCell(i);
                        log = i == mouseCell;
                        float temperature = temperatures[i];
                        ThermalProps cellProps = GetThermalPropertiesAt(i);
                        float heatFlow = cellProps.HeatFlow; // How quickly the system changes its temperature (capacity * conductivity)
                        float energy = temperature * heatFlow; // How much energy is added to the cell (temperature * capacity * conductivity)

                        // Terrain temperature
                        if (updateTerrainTemperature)
                        {
                            float terrainTemperature = terrainTemperatures[i];
                            if (!float.IsNaN(terrainTemperature))
                            {
                                TerrainDef terrain = map.terrainGrid.TerrainAt(i);
                                TerrainThermalProperties terrainProps = terrain?.GetTerrainThermalProperties();
                                if (terrainProps != null && terrainProps.heatCapacity > 0)
                                {
                                    // Thermal exchange with terrain
                                    ThermalProps thermalProps = terrainProps.GetThermalProps();
                                    TemperatureUtility.CalculateHeatTransferTerrain(terrainTemperature, thermalProps, ref energy, ref heatFlow);
                                    float terrainTempChange = (temperature - terrainTemperature) * cellProps.HeatFlow / heatFlow;
                                    if (log)
                                        LogUtility.Log($"Terrain temperature: {terrainTemperature:F1}C. Terrain heat capacity: {thermalProps.heatCapacity}. Terrain heatflow: {thermalProps.HeatFlow:P0}. Equilibrium temperature: {terrainTemperature + terrainTempChange:F1}C.");
                                    terrainTemperature += terrainTempChange * thermalProps.conductivity;

                                    // Melting or freezing if terrain temperature has crossed respective melt/freeze points (upwards or downwards)
                                    if (terrainProps.MeltsAt(terrainTemperature))
                                        cell.MeltTerrain(map, log);
                                    else if (terrainProps.FreezesAt(terrainTemperature))
                                        cell.FreezeTerrain(map, log);

                                    terrainTemperatures[i] = terrainTemperature;
                                }
                                else terrainTemperatures[i] = float.NaN;
                            }
                            // Rarely checking if a cell now has terrain temperature (e.g. when a bridge has been removed)
                            else if (updateCounter % RareUpdateInterval == 2 && map.terrainGrid.TerrainAt(i).HasTemperature())
                                terrainTemperatures[i] = temperature;
                        }

                        // Diffusion & convection
                        void ProcessNeighbour(int neighbourIndex) => TemperatureUtility.CalculateHeatTransferCells(temperatures[neighbourIndex], GetThermalPropertiesAt(neighbourIndex), cellProps.airflow, ref energy, ref heatFlow, log);

                        if (cell.z > 0)
                            ProcessNeighbour(i - map.Size.x);
                        if (cell.z < map.Size.z - 1)
                            ProcessNeighbour(i + map.Size.x);
                        if (cell.x > 0)
                            ProcessNeighbour(i - 1);
                        if (cell.x < map.Size.x - 1)
                            ProcessNeighbour(i + 1);

                        // Thermal exchange with the environment
                        RoofDef roof = map.roofGrid.RoofAt(i);
                        TemperatureUtility.CalculateHeatTransferEnvironment(outdoorTemperature, cellProps, roof, ref energy, ref heatFlow);

                        // Applying heat transfer
                        float equilibriumTemp = energy / heatFlow;
                        if (log)
                            LogUtility.Log($"Total cell + neighbours energy: {energy:F4}. Total heat flow rate: {heatFlow:F4}. Equilibrium temperature: {equilibriumTemp:F1}C.");

                        temperature += (equilibriumTemp - temperature) * cellProps.conductivity;
                        worksetTemperatures[i] = temperature;

                        // Snow melting
                        if (temperature > 0 && cell.GetSnowDepth(map) > 0)
                        {
                            if (log)
                                LogUtility.Log($"Snow: {cell.GetSnowDepth(map):F4}. {(roof != null ? "Roofed." : "Unroofed.")} Melting: {FreezeMeltUtility.SnowChangeAmountAt(temperature) * (roof != null ? Settings.TicksPerUpdate : outdoorSnowMeltRate):F4}/update.");
                            if (roof != null)
                                map.snowGrid.AddDepth(cell, FreezeMeltUtility.SnowChangeAmountAt(temperature) * Settings.TicksPerUpdate);
                            else map.snowGrid.AddDepth(cell, FreezeMeltUtility.SnowChangeAmountAt(temperature) * outdoorSnowMeltRate);
                        }

                        // Autoignition
                        if (Settings.AutoignitionEnabled && temperature > MinIgnitionTemperature && map.terrainGrid.TerrainAt(i).Flammable())
                        {
                            Fire existingFire = null;
                            float fireSize = 0;
                            List<Thing> things = map.thingGrid.ThingsListAtFast(i);
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
                                    try
                                    {
                                        FireUtility.TryStartFireIn(cell, map, fireSize, null);
                                    }
                                    catch (Exception e)
                                    {
                                        LogUtility.Log(e.Message, LogLevel.Error);
                                    }
                                }
                                else existingFire.fireSize += fireSize;
                        }
                    }
                });

                // This should immediately exit anyway, but it's good to check to make sure
                while (!parallelLoopResult.IsCompleted) ;

                // Workset becomes the active temperatures array, and the old temperatures array will be used as the workset at the next update
                Gen.Swap(ref temperatures, ref worksetTemperatures);

                if (updateStopwatch.IsRunning)
                    updateStopwatch.Stop();

                temperatureUpdateHandle.Reset();
                updated = true;
            }
        }

        public override void MapComponentTick()
        {
            if (!initialized)
                FinalizeInit();

            if (++tick < Settings.TicksPerUpdate)
                return;

            if (Settings.DebugMode && updateCounter % 10 == 1)
                LogUtility.Log($"Updated temperatures for {map} {updateCounter} times at {(float)updateStopwatch.ElapsedMilliseconds / updateCounter:F1} ms per update.");

            ResetSnowMeltRate();
            updateCounter++;
            
            // On every update clear the room temperatures and set the temperature overlay to be dirty
            roomTemperatures.Clear();
            minMaxTemperaturesUpdated = false;
            overlayDrawer.SetDirty();
            
            // Rarely (every 4 updates) clearing the thermal properties cache, to take into account new & destroyed buildings
            if (updateCounter % RareUpdateInterval == 0)
                Array.Clear(thermalProperties, 0, thermalProperties.Length);
            
            // Very rarely (every 20 updates) resetting min & max temperatures used in temperature overlay; this is done to prevent flickering when min/max change too often
            if (updateCounter % MinMaxTemperatureResetInterval == 0 && Find.PlaySettings.showTemperatureOverlay && Find.CurrentMap == map)
                ResetMinMaxTemperature();

            if (updated)
            {
                updated = false;
                temperatureUpdateHandle.Set();
            }
            else
            {
                LogUtility.Log("Skipped update of temperatures because previous was still running.", LogLevel.Warning);
                updated = true;
                temperatureUpdateHandle.Reset();
            }

            tick = 0;
        }

        public bool InBounds(int index) => index >= 0 && index < temperatures.Length;

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

        public float GetTerrainTemperature(int index) => terrainTemperatures[index];

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

        public float GetIgnitionTemperatureForCell(int index)
        {
            float min = 9999;
            List<Thing> things = map.thingGrid.ThingsListAtFast(index);
            for (int i = 0; i < things.Count; i++)
            {
                if (things[i].FireBulwark)
                    return 9999;
                if (things[i].GetStatValue(StatDefOf.Flammability) > 0)
                {
                    float ignitionTemperature = things[i].GetStatValue(DefOf.Celsius_IgnitionTemperature);
                    if (ignitionTemperature >= MinIgnitionTemperature && ignitionTemperature < min)
                        min = ignitionTemperature;
                }
            }
            return min;
        }
    }
}
