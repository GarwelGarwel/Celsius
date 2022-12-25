using RimWorld;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;
using Verse;

namespace Celsius
{
    public class TemperatureInfo : MapComponent
    {
        // Ticks between full map updates
        public const int TicksPerUpdate = 120;

        // Number of "slices": parts the full update is divided into
        public const int SliceCount = 4;

        // Ticks between partial updates (slices)
        public const int TicksPerSlice = TicksPerUpdate / SliceCount;

        // Normal full updates between rare updates
        public const int RareUpdateInterval = 4;

        // Amount of snow to be melted each update (to be similar to vanilla)
        const float SnowMeltCoefficient = TicksPerUpdate * 0.0006f;

        // How quickly snow melts under rain
        const float SnowMeltCoefficientRain = SnowMeltCoefficient * 2;

        // Minimum allowed temperature for autoignition
        const float MinIgnitionTemperature = 100;

        // How quickly min & max temperatures for temperature overlay adjust
        const float MinMaxTemperatureAdjustmentStep = 1;

        bool initialized;
        int updateTickOffset;
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

        float[] minTemperatures = new float[SliceCount];
        float[] maxTemperatures = new float[SliceCount];
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
            thermalProperties = new ThermalProps[map.Size.x * map.Size.z];
            mountainTemperature = GetMountainTemperatureFor(Settings.MountainTemperatureMode);

            // Setting up min & max temperatures (for overlay)
            minComfortableTemperature = ThingDefOf.Human.GetStatValueAbstract(StatDefOf.ComfyTemperatureMin);
            maxComfortableTemperature = ThingDefOf.Human.GetStatValueAbstract(StatDefOf.ComfyTemperatureMax);
            for (int i = 0; i < SliceCount; i++)
            {
                minTemperatures[i] = minComfortableTemperature - 10;
                maxTemperatures[i] = maxComfortableTemperature + 10;
            }

            // Initializing for the first run
            if (temperatures == null)
            {
                LogUtility.Log($"Initializing temperatures for {map} for the first time.", LogLevel.Important);
                temperatures = new float[map.Size.x * map.Size.z];
                terrainTemperatures = new float[map.Size.x * map.Size.z];
                bool hasTerrainTemperatures = false;
                for (int i = 0; i < temperatures.Length; i++)
                {
                    IntVec3 cell = map.cellIndices.IndexToCell(i);
                    Room room = cell.GetRoomOrAdjacent(map);
                    if (room != null)
                    {
                        temperatures[i] = room.TempTracker.Temperature;
                        roomTemperatures[room.ID] = temperatures[i];
                    }
                    else if (!TryGetEnvironmentTemperatureForCell(cell, out temperatures[i]))
                        temperatures[i] = map.mapTemperature.OutdoorTemp;
                    if (temperatures[i] < minTemperature)
                        minTemperature = temperatures[i];
                    else if (temperatures[i] > maxTemperature)
                        maxTemperature = temperatures[i];
                    TerrainDef terrain = cell.GetTerrain(map);
                    if (terrain.HasTemperature())
                    {
                        hasTerrainTemperatures = true;
                        terrainTemperatures[i] = map.mapTemperature.SeasonalTemp;
                        if (terrain.ShouldFreeze(terrainTemperatures[i]))
                            cell.FreezeTerrain(map);
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

            updateTickOffset = map.generationTick % TicksPerSlice;
            slice = Find.TickManager.TicksGame / TicksPerSlice % SliceCount;
            initialized = true;
            LogUtility.Log($"TemperatureInfo initialized for {map}.");
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

            str = DataUtility.ArrayToString(terrainTemperatures);
            Scribe_Values.Look(ref str, "terrainTemperatures");
            if (str != null)
                terrainTemperatures = DataUtility.StringToArray(str);

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
                // Localization key: Celsius_MapTempOverlay_Cell - Cell: {GetTemperatureForCell(cell).ToStringTemperature(Settings.TemperatureDisplayFormatString)}
                string tooltip = "Celsius_MapTempOverlay_Cell".Translate(GetTemperatureForCell(cell).ToStringTemperature(Settings.TemperatureDisplayFormatString));
                if (Settings.FreezingAndMeltingEnabled && HasTerrainTemperatures)
                {
                    float terrainTemperature = GetTerrainTemperature(cell);
                    if (!float.IsNaN(terrainTemperature))
                        // Localization key: Celsius_MapTempOverlay_Terrain - Terrain: {terrainTemperature.ToStringTemperature(Settings.TemperatureDisplayFormatString)}
                        tooltip += "\n" + "Celsius_MapTempOverlay_Terrain".Translate(terrainTemperature.ToStringTemperature(Settings.TemperatureDisplayFormatString));
                }
                Widgets.Label(new Rect(UI.MousePositionOnUIInverted.x + 20, UI.MousePositionOnUIInverted.y + 20, 100, 40), tooltip);
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

            if (!initialized)
                FinalizeInit();

            if (Find.TickManager.TicksGame % TicksPerSlice != updateTickOffset)
                return;

#if DEBUG
            updateStopwatch.Start();
#endif

            int mouseCell = Prefs.DevMode && Settings.DebugMode && Find.PlaySettings.showTemperatureOverlay ? map.cellIndices.CellToIndex(UI.MouseCell()) : -1;
            bool log;

            if (slice == 0)
            {
                roomTemperatures.Clear();
                if (rareUpdateCounter == 0)
                {
                    mountainTemperature = GetMountainTemperatureFor(Settings.MountainTemperatureMode);
                    outdoorSnowMeltRate = map.weatherManager.RainRate > 0 ? SnowMeltCoefficientRain : SnowMeltCoefficient;
                    thermalProperties = new ThermalProps[map.Size.x * map.Size.z];
                }
            }

            if (minTemperatures[slice] < minComfortableTemperature + 10)
                minTemperatures[slice] += MinMaxTemperatureAdjustmentStep;
            if (maxTemperatures[slice] > maxComfortableTemperature - 10)
                maxTemperatures[slice] -= MinMaxTemperatureAdjustmentStep;

            // Main loop
            for (int j = slice; j < temperatures.Length; j += SliceCount)
            {
                IntVec3 cell = map.cellsInRandomOrder.Get(j);
                int i = map.cellIndices.CellToIndex(cell);
                log = i == mouseCell;
                float temperature = temperatures[i];
                ThermalProps cellProps = GetThermalPropertiesAt(i);
                if (log)
                    LogUtility.Log($"Cell {cell}. Temperature: {temperature:F1}C. Capacity: {cellProps.heatCapacity}. Insulation: {cellProps.insulation}. Conductivity: {cellProps.Conductivity:P0}.");

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
                            TemperatureUtility.CalculateHeatTransfer(temperature, terrainTemperature, terrainProps, 0, ref energy, ref heatFlow, log);
                            float terrainTempChange = (temperature - terrainTemperature) * cellProps.HeatFlow / heatFlow;
                            if (log)
                                LogUtility.Log($"Terrain temperature: {terrainTemperature:F1}C. Terrain heat capacity: {terrainProps.heatCapacity}. Terrain heatflow: {terrainProps.HeatFlow:P0}. Equilibrium temperature: {terrainTemperature + terrainTempChange:F1}C.");
                            terrainTemperature += terrainTempChange * terrainProps.Conductivity;
                            terrainTemperatures[i] = terrainTemperature;

                            // Freezing and melting (rarely)
                            if (i % RareUpdateInterval == rareUpdateCounter)
                                if (terrain.ShouldFreeze(terrainTemperature))
                                    cell.FreezeTerrain(map, log);
                                else if (terrain.ShouldMelt(terrainTemperature))
                                    cell.MeltTerrain(map, log);
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
                        TemperatureUtility.CalculateHeatTransfer(temperature, temperatures[index], GetThermalPropertiesAt(index), cellProps.airflow, ref energy, ref heatFlow, log);
                    }
                }

                ProcessNeighbour(cell + IntVec3.North);
                ProcessNeighbour(cell + IntVec3.East);
                ProcessNeighbour(cell + IntVec3.South);
                ProcessNeighbour(cell + IntVec3.West);

                // Default environment temperature
                if (TryGetEnvironmentTemperatureForCell(cell, out float environmentTemperature))
                    TemperatureUtility.CalculateHeatTransferEnvironment(temperature, environmentTemperature, cellProps, ref energy, ref heatFlow, log);

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
                        LogUtility.Log($"Snow: {cell.GetSnowDepth(map):F4}. {(cell.Roofed(map) ? "Roofed." : "Unroofed.")} Melting: {FreezeMeltUtility.SnowMeltAmountAt(temperature) * (cell.Roofed(map) ? SnowMeltCoefficient : SnowMeltCoefficientRain):F4}.");
                    map.snowGrid.AddDepth(cell, -FreezeMeltUtility.SnowMeltAmountAt(temperature) * (cell.Roofed(map) ? SnowMeltCoefficient : outdoorSnowMeltRate));
                }

                // Autoignition
                if (Settings.AutoignitionEnabled && rareUpdateCounter == 0 && temperature > MinIgnitionTemperature)
                {
                    float fireSize = 0;
                    List<Thing> things = map.thingGrid.ThingsListAtFast(cell);
                    for (int k = 0; k < things.Count; k++)
                    {
                        if (things[k].FireBulwark || things[k] is Fire)
                        {
                            fireSize = 0;
                            break;
                        }
                        float ignitionTemp = things[k].GetStatValue(DefOf.IgnitionTemperature);
                        if (ignitionTemp >= MinIgnitionTemperature && temperature >= ignitionTemp)
                        {
                            LogUtility.Log($"{things[k]} spontaneously ignites at {temperature:F1}C! Autoignition temperature is {ignitionTemp:F0}C.");
                            fireSize += 0.1f * things[k].GetStatValue(StatDefOf.Flammability);
                        }
                    }

                    if (fireSize > 0)
                        FireUtility.TryStartFireIn(cell, map, fireSize);
                }

                if (!Settings.UseVanillaTemperatureColors)
                    if (temperature < minTemperatures[slice])
                        minTemperatures[slice] = temperature;
                    else if (temperature > maxTemperatures[slice])
                        maxTemperatures[slice] = temperature;
            }

            if (slice == 0)
            {
                rareUpdateCounter = (rareUpdateCounter + 1) % RareUpdateInterval;
                minTemperature = Mathf.Min(minTemperatures);
                maxTemperature = Mathf.Max(maxTemperatures);
                overlayDrawer.SetDirty();
            }
            slice = (slice + 1) % SliceCount;

#if DEBUG
            if (Settings.DebugMode)
            {
                updateStopwatch.Stop();
                if (slice == 0)
                    LogUtility.Log($"Updated temperatures for {map} on tick {Find.TickManager.TicksGame} in {updateStopwatch.Elapsed.TotalMilliseconds / ++tickIterations:N0} ms.");
            }
#endif
        }

        public float GetMountainTemperatureFor(MountainTemperatureMode mode)
        {
            switch (mode)
            {
                case MountainTemperatureMode.Vanilla:
                    return TemperatureTuning.DeepUndergroundTemperature;

                case MountainTemperatureMode.AnnualAverage:
                    return Find.WorldGrid[map.Tile].temperature;

                case MountainTemperatureMode.SeasonAverage:
                    return GenTemperature.AverageTemperatureAtTileForTwelfth(map.Tile, GenLocalDate.Twelfth(map).PreviousTwelfth());

                case MountainTemperatureMode.AmbientAir:
                    return map.mapTemperature.OutdoorTemp;

                case MountainTemperatureMode.Manual:
                    return Settings.MountainTemperature;
            }
            return TemperatureTuning.DeepUndergroundTemperature;
        }

        public bool TryGetEnvironmentTemperatureForCell(IntVec3 cell, out float temperature)
        {
            RoofDef roof = cell.GetRoof(map);
            if ((roof == RoofDefOf.RoofRockThick || roof == RoofDefOf.RoofRockThin) && cell.GetFirstMineable(map) != null)
            {
                temperature = mountainTemperature;
                return true;
            }
            temperature = map.mapTemperature.OutdoorTemp;
            return roof == null;
        }

        public float GetTemperatureForCell(int index) => temperatures != null ? temperatures[index] : TemperatureTuning.DefaultTemperature;

        public float GetTemperatureForCell(IntVec3 cell) => GetTemperatureForCell(map.cellIndices.CellToIndex(cell));

        public float GetRoomTemperature(Room room)
        {
            if (room == null || room.ID == -1 || roomTemperatures == null)
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
            for (int i = 0; i < thingsList.Count; i++)
                if (CompThermal.ShouldApplyTo(thingsList[i].def))
                {
                    ThermalProps thermalProps = thingsList[i].TryGetComp<CompThermal>()?.ThermalProperties;
                    if (thermalProps != null)
                        return thermalProperties[index] = thermalProps;
                }
            return thermalProperties[index] = ThermalProps.Air;
        }

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
                    float ignitionTemperature = things[i].GetStatValue(DefOf.IgnitionTemperature);
                    if (ignitionTemperature >= MinIgnitionTemperature)
                        min = Mathf.Min(min, ignitionTemperature);
                }
            }
            return min;
        }
    }
}
