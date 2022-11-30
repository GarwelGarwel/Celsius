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

        // Updates happen when tick % TicksPerUpdate = this value
        const int UpdateTickOffset = 18;

        // How many in-game seconds take place between updates (172.8)
        public const float SecondsPerUpdate = 3600 * TicksPerUpdate / GenDate.TicksPerHour;

        // Relative amount of snow to be melted each update compared to vanilla (0.072)
        const float SnowMeltCoefficient = TicksPerUpdate * 0.0006f;

        // How quickly snow melts under rain
        const float SnowMeltCoefficientRain = SnowMeltCoefficient * 2;

        // Minimum allowed temperature for autoignition
        const float MinIgnitionTemperature = 100;

        const float MinMaxTemperatureAdjustmentStep = 1;

        bool initialized;
        float[,] temperatures;
        float[,] terrainTemperatures;
        Dictionary<int, float> roomTemperatures = new Dictionary<int, float>();
        float mountainTemperature;

        static float minComfortableTemperature = TemperatureTuning.DefaultTemperature - 5, maxComfortableTemperature = TemperatureTuning.DefaultTemperature + 5;
        static readonly Color minColor = Color.blue;
        static readonly Color minComfortableColor = new Color(0, 1, 0.5f);
        static readonly Color maxComfortableColor = new Color(0.5f, 1, 0);
        static readonly Color maxColor = Color.red;

        float minTemperature = minComfortableTemperature - 5, maxTemperature = maxComfortableTemperature + 5;
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
            if (temperatures == null)
            {
                LogUtility.Log($"Initializing temperatures for {map} from vanilla data.", LogLevel.Warning);
                temperatures = new float[map.Size.x, map.Size.z];
                terrainTemperatures = new float[map.Size.x, map.Size.z];
                mountainTemperature = GetMountainTemperatureFor(Settings.MountainTemperatureMode);
                bool hasTerrainTemperatures = false;
                for (int i = 0; i < temperatures.GetLength(0); i++)
                    for (int j = 0; j < temperatures.GetLength(1); j++)
                    {
                        IntVec3 cell = new IntVec3(i, 0, j);
                        Room room = cell.GetRoomOrAdjacent(map);
                        if (room != null)
                        {
                            temperatures[i, j] = room.TempTracker.Temperature;
                            roomTemperatures[room.ID] = temperatures[i, j];
                        }
                        else TryGetEnvironmentTemperatureForCell(cell, out temperatures[i, j]);
                        if (cell.HasTerrainTemperature(map))
                        {
                            terrainTemperatures[i, j] = map.mapTemperature.SeasonalTemp;
                            hasTerrainTemperatures = true;
                        }
                    }
                if (!hasTerrainTemperatures)
                    terrainTemperatures = null;
            }

            minComfortableTemperature = ThingDefOf.Human.GetStatValueAbstract(StatDefOf.ComfyTemperatureMin);
            maxComfortableTemperature = ThingDefOf.Human.GetStatValueAbstract(StatDefOf.ComfyTemperatureMax);
            overlayDrawer = new CellBoolDrawer(
                index => !map.fogGrid.IsFogged(index),
                () => Color.white,
                index => TemperatureColorForCell(index),
                map.Size.x,
                map.Size.z);
            initialized = true;
            LogUtility.Log($"TemperatureInfo initialized for {map}.");
        }

        public override void ExposeData()
        {
            base.ExposeData();
            string str = DataUtility.ArrayToString(temperatures);
            Scribe_Values.Look(ref str, "temperatures");
            if (str != null)
                temperatures = DataUtility.StringToArray(str, map.Size.x);
            str = DataUtility.ArrayToString(terrainTemperatures);
            Scribe_Values.Look(ref str, "terrainTemperatures");
            if (str != null)
                terrainTemperatures = DataUtility.StringToArray(str, map.Size.x);
        }

        Color TemperatureColorForCell(int index)
        {
            if (Settings.UseVanillaTemperatureColors)
                return map.mapTemperature.GetCellExtraColor(index);
            float temperature = GetTemperatureForCell(CellIndicesUtility.IndexToCell(index, map.Size.x));
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
                string tooltip = $"Cell: {GetTemperatureForCell(cell).ToStringTemperature()}";
                if (Settings.FreezingAndMeltingEnabled && HasTerrainTemperatures && cell.HasTerrainTemperature(map))
                    tooltip += $"\nTerrain: {GetTerrainTemperature(cell).ToStringTemperature()}";
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

            if (Find.TickManager.TicksGame % TicksPerUpdate != UpdateTickOffset)
                return;

#if DEBUG
            updateStopwatch.Start();
#endif

            IntVec3 mouseCell = UI.MouseCell();
            bool log;
            float[,] newTemperatures = (float[,])temperatures.Clone();
            roomTemperatures.Clear();
            mountainTemperature = GetMountainTemperatureFor(Settings.MountainTemperatureMode);
            float outdoorSnowMeltRate = map.weatherManager.RainRate > 0 ? SnowMeltCoefficientRain : SnowMeltCoefficient;
            if (minTemperature < minComfortableTemperature + 5)
                minTemperature += MinMaxTemperatureAdjustmentStep;
            if (maxTemperature > maxComfortableTemperature - 5)
                maxTemperature -= MinMaxTemperatureAdjustmentStep;

            // Main loop
            for (int x = 0; x < map.Size.x; x++)
                for (int z = 0; z < map.Size.z; z++)
                {
                    IntVec3 cell = new IntVec3(x, 0, z);
                    log = Prefs.DevMode && Settings.DebugMode && cell == mouseCell && Find.PlaySettings.showTemperatureOverlay;
                    float temperature = temperatures[x, z];
                    ThermalProps cellProps = cell.GetThermalProperties(map);
                    if (log)
                        LogUtility.Log($"Cell {cell}. Temperature: {temperature:F1}C. Capacity: {cellProps.heatCapacity}. Isolation: {cellProps.isolation}. Conductivity: {cellProps.Conductivity:P0}.");

                    float energy = 0;
                    float heatFlow = cellProps.HeatFlow;

                    // Terrain temperature
                    if (Settings.FreezingAndMeltingEnabled && HasTerrainTemperatures)
                    {
                        TerrainDef terrain = cell.GetTerrain(map);
                        ThermalProps terrainProps = terrain?.GetModExtension<ThingThermalProperties>()?.GetThermalProps();
                        if (terrainProps != null && terrainProps.heatCapacity > 0)
                        {
                            float terrainTemperature = GetTerrainTemperature(cell);
                            TemperatureUtility.CalculateHeatTransfer(temperature, terrainTemperature, terrainProps, 0, ref energy, ref heatFlow, log);
                            float terrainTempChange = (temperature - terrainTemperature) * cellProps.HeatFlow / heatFlow;
                            if (log)
                                LogUtility.Log($"Terrain temperature: {terrainTemperature:F1}C. Terrain heat capacity: {terrainProps.heatCapacity}. Terrain cellHeatFlow: {terrainProps.HeatFlow:P0}. Equilibrium temperature: {GetTerrainTemperature(cell) + terrainTempChange:F1}C.");
                            terrainTemperatures[x, z] += terrainTempChange * terrainProps.Conductivity;

                            // Freezing and melting
                            if (terrain.IsWater && terrainTemperatures[x, z] < terrain.FreezingPoint())
                                cell.FreezeTerrain(map, log);
                            else if (terrainTemperatures[x, z] > TemperatureUtility.MinFreezingTemperature && terrain == TerrainDefOf.Ice)
                            {
                                TerrainDef meltedTerrain = cell.BestUnderIceTerrain(map);
                                if (terrainTemperatures[x, z] > meltedTerrain.FreezingPoint())
                                    cell.MeltTerrain(map, meltedTerrain, log);
                            }
                        }
                    }

                    // Diffusion & convection
                    foreach (IntVec3 neighbour in cell.AdjacentCells())
                        if (neighbour.InBounds(map))
                            TemperatureUtility.CalculateHeatTransfer(temperature, GetTemperatureForCell(neighbour), neighbour.GetThermalProperties(map), cellProps.airflow, ref energy, ref heatFlow, log);

                    // Default environment temperature
                    if (TryGetEnvironmentTemperatureForCell(cell, out float environmentTemperature))
                        TemperatureUtility.CalculateHeatTransferEnvironment(temperature, environmentTemperature, cellProps, ref energy, ref heatFlow, log);

                    // Applying heat transfer
                    float equilibriumDifference = energy / heatFlow;
                    if (log)
                        LogUtility.Log($"Total cell + neighbours energy: {energy:F4}. Total heat flow rate: {heatFlow:F4}. Equilibrium temperature: {newTemperatures[x, z] + equilibriumDifference:F1}C.");
                    newTemperatures[x, z] += equilibriumDifference * cellProps.Conductivity;

                    // Snow melting
                    if (temperature > 0 && cell.GetSnowDepth(map) > 0)
                    {
                        if (log)
                            LogUtility.Log($"Snow: {cell.GetSnowDepth(map):F4}. {(cell.Roofed(map) ? "Roofed." : "Unroofed.")} Melting: {TemperatureUtility.SnowMeltAmountAt(temperature) * (cell.Roofed(map) ? SnowMeltCoefficient : SnowMeltCoefficientRain):F4}.");
                        map.snowGrid.AddDepth(cell, -TemperatureUtility.SnowMeltAmountAt(temperature) * (cell.Roofed(map) ? SnowMeltCoefficient : SnowMeltCoefficientRain));
                    }

                    // Autoignition
                    if (Settings.AutoignitionEnabled && temperature > MinIgnitionTemperature)
                    {
                        float fireSize = 0;
                        for (int i = 0; i < cell.GetThingList(map).Count; i++)
                        {
                            Thing thing = cell.GetThingList(map)[i];
                            if (thing is Fire || (thing.FireBulwark && thing.Spawned))
                            {
                                fireSize = 0;
                                break;
                            }
                            float ignitionTemp = thing.GetStatValue(DefOf.IgnitionTemperature);
                            if (ignitionTemp >= MinIgnitionTemperature && temperature >= ignitionTemp)
                            {
                                LogUtility.Log($"{thing} spontaneously ignites at {temperature:F1}C! Autoignition temperature is {ignitionTemp:F0}C.");
                                fireSize += 0.1f * thing.GetStatValue(StatDefOf.Flammability);
                            }
                        }

                        if (fireSize > 0)
                            FireUtility.TryStartFireIn(cell, map, fireSize);
                    }

                    if (!Settings.UseVanillaTemperatureColors)
                        if (newTemperatures[x, z] < minTemperature)
                            minTemperature = newTemperatures[x, z];
                        else if (newTemperatures[x, z] > maxTemperature)
                            maxTemperature = newTemperatures[x, z];
                }

            temperatures = newTemperatures;
            overlayDrawer.SetDirty();

#if DEBUG
            if (Settings.DebugMode)
            {
                updateStopwatch.Stop();
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

        public float MountainTemperature => mountainTemperature;

        public bool TryGetEnvironmentTemperatureForCell(IntVec3 cell, out float temperature)
        {
            RoofDef roof = cell.GetRoof(map);
            if (cell.GetFirstMineable(map) != null && (roof == RoofDefOf.RoofRockThick || roof == RoofDefOf.RoofRockThin))
            {
                temperature = MountainTemperature;
                return true;
            }
            temperature = map.mapTemperature.OutdoorTemp;
            return roof == null;
        }

        public float GetTemperatureForCell(IntVec3 cell) => temperatures != null ? temperatures[cell.x, cell.z] : TemperatureTuning.DefaultTemperature;

        public float GetRoomTemperature(Room room)
        {
            if (room == null || room.ID == -1 || roomTemperatures == null)
            {
                LogUtility.Log($"Could not get temperature for room {room?.ToString() ?? "null"}.", LogLevel.Error);
                return TemperatureTuning.DefaultTemperature;
            }
            float temperature;
            if (roomTemperatures.TryGetValue(room.ID, out temperature))
                return temperature;
            temperature = room.Cells.Average(cell => GetTemperatureForCell(cell));
            return roomTemperatures[room.ID] = temperature;
        }

        public bool HasTerrainTemperatures => terrainTemperatures != null;

        public float GetTerrainTemperature(IntVec3 cell) => terrainTemperatures[cell.x, cell.z];

        public void SetTemperatureForCell(IntVec3 cell, float temperature) => temperatures[cell.x, cell.z] = Mathf.Max(temperature, -273);

        public float GetIgnitionTemperatureForCell(IntVec3 cell)
        {
            float min = 10000;
            foreach (Thing thing in cell.GetThingList(map))
            {
                if (thing.FireBulwark)
                    return 10000;
                if (thing.GetStatValue(StatDefOf.Flammability) > 0)
                {
                    float ignitionTemperature = thing.GetStatValue(DefOf.IgnitionTemperature);
                    if (ignitionTemperature > MinIgnitionTemperature)
                        min = Mathf.Min(min, ignitionTemperature);
                }
            }
            return min;
        }
    }
}
