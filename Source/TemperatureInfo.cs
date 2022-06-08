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
        public const int TicksPerUpdate = 120;
        const int UpdateTickOffset = 18;
        public const float SecondsPerUpdate = 3600 * TicksPerUpdate / 2500;
        const float MinIgnitionTemperature = 100;

        float[,] temperatures;
        float[,] terrainTemperatures;
        Dictionary<int, float> roomTemperatures;

        float minTemperature = TemperatureTuning.DefaultTemperature - 20, maxTemperature = TemperatureTuning.DefaultTemperature + 20;
        CellBoolDrawer overlayDrawer;
        readonly Color minComfortableColor = new Color(0, 0.5f, 0.5f);
        readonly Color maxComfortableColor = new Color(0.5f, 0.5f, 0);

        Stopwatch updateStopwatch = new Stopwatch(), totalStopwatch = new Stopwatch();
        int tickIterations, totalTicks;

        public TemperatureInfo(Map map)
            : base(map)
        { }

        public override void FinalizeInit()
        {
            roomTemperatures = new Dictionary<int, float>();
            if (temperatures == null)
            {
                LogUtility.Log($"Initializing temperatures from vanilla data.");
                temperatures = new float[map.Size.x, map.Size.z];
                terrainTemperatures = new float[map.Size.x, map.Size.z];
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
            overlayDrawer = new CellBoolDrawer(index => !map.fogGrid.IsFogged(index), () => Color.white, index => TemperatureColorForCell(index), map.Size.x, map.Size.z);
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
            float temperature = GetTemperatureForCell(CellIndicesUtility.IndexToCell(index, map.Size.x));
            if (temperature < TemperatureTuning.DefaultTemperature - 5)
                return Color.Lerp(Color.blue, minComfortableColor, (temperature - minTemperature) / (TemperatureTuning.DefaultTemperature - 5 - minTemperature));
            if (temperature < TemperatureTuning.DefaultTemperature + 5)
                return Color.Lerp(minComfortableColor, maxComfortableColor, (temperature - TemperatureTuning.DefaultTemperature + 5) / 10);
            return Color.Lerp(maxComfortableColor, Color.red, (temperature - maxTemperature) / (maxTemperature - TemperatureTuning.DefaultTemperature - 5));
        }

        public override void MapComponentUpdate()
        {
            if (Settings.ShowTemperatureMap && Find.CurrentMap == map)
                overlayDrawer.MarkForDraw();
            overlayDrawer.CellBoolDrawerUpdate();
        }

        public override void MapComponentOnGUI()
        {
            if (Prefs.DevMode && Settings.DebugMode && Find.TickManager.CurTimeSpeed != TimeSpeed.Ultrafast && totalStopwatch.IsRunning)
                totalStopwatch.Stop();

            if (!Settings.ShowTemperatureMap)
                return;
            IntVec3 cell = UI.MouseCell();
            if (cell.InBounds(map) && (!cell.Fogged(map) || Prefs.DevMode))
            {
                Text.Font = GameFont.Tiny;
                string tooltip = $"Cell: {GetTemperatureForCell(cell).ToStringTemperature()}";
                if (Settings.FreezingAndMeltingEnabled && HasTerrainTemperatures && cell.HasTerrainTemperature(map))
                    tooltip += $"\nTerrain: {GetTerrainTemperature(cell).ToStringTemperature()}";
                Widgets.Label(new Rect(UI.MousePositionOnUIInverted.x + 20, UI.MousePositionOnUIInverted.y + 20, 100, 40), tooltip);
            }
        }

        public override void MapComponentTick()
        {
            if (Prefs.DevMode && Settings.DebugMode && Find.TickManager.CurTimeSpeed == TimeSpeed.Ultrafast)
            {
                if (++totalTicks % 500 == 0)
                    LogUtility.Log($"Total ultrafast ticks: {totalTicks}. Average time/1000 ticks: {1000 * totalStopwatch.ElapsedMilliseconds / totalTicks} ms.");
                totalStopwatch.Start();
            }

            if (Find.TickManager.TicksGame % TicksPerUpdate != UpdateTickOffset)
                return;

            if (Settings.DebugMode)
                updateStopwatch.Start();

            IntVec3 mouseCell = UI.MouseCell();
            bool log;
            float[,] newTemperatures = (float[,])temperatures.Clone();
            roomTemperatures.Clear();
            minTemperature = TemperatureTuning.DefaultTemperature - 20;
            maxTemperature = TemperatureTuning.DefaultTemperature + 20;

            // Main loop
            for (int x = 0; x < map.Size.x; x++)
                for (int z = 0; z < map.Size.z; z++)
                {
                    IntVec3 cell = new IntVec3(x, 0, z);
                    log = Prefs.DevMode && Settings.DebugMode && cell == mouseCell;
                    ThingThermalProperties cellProps = cell.GetThermalProperties(map);
                    if (log)
                        LogUtility.Log($"Cell {cell}. Temperature: {GetTemperatureForCell(cell):F1}C. Capacity: {cell.GetHeatCapacity(map)}. Conductivity: {cell.GetHeatConductivity(map)}.");

                    // Diffusion & convection
                    void DiffusionWithNeighbour(IntVec3 neighbour)
                    {
                        if (!neighbour.InBounds(map))
                            return;
                        (float, float) changes = TemperatureUtility.DiffusionTemperatureChange(
                            temperatures[x, z],
                            cellProps,
                            GetTemperatureForCell(neighbour),
                            neighbour.GetThermalProperties(map));
                        newTemperatures[x, z] += changes.Item1;
                        newTemperatures[neighbour.x, neighbour.z] += changes.Item2;
                    }

                    DiffusionWithNeighbour(cell + IntVec3.East);
                    DiffusionWithNeighbour(cell + IntVec3.North);

                    // Terrain temperature
                    if (Settings.FreezingAndMeltingEnabled && HasTerrainTemperatures)
                    {
                        TerrainDef terrain = cell.GetTerrain(map);
                        ThingThermalProperties terrainProps = terrain?.GetModExtension<ThingThermalProperties>();
                        if (terrainProps != null && terrainProps.heatCapacity > 0)
                        {
                            (float, float) tempChange = TemperatureUtility.DiffusionTemperatureChange(GetTerrainTemperature(cell), terrainProps, temperatures[x, z], cellProps);
                            if (log)
                                LogUtility.Log($"Terrain temp change: {tempChange.Item1:F1}C. Cell temp change: {tempChange.Item2:F1}C.");
                            terrainTemperatures[x, z] += tempChange.Item1;
                            newTemperatures[x, z] += tempChange.Item2;

                            // Freezing and melting
                            if (terrain.IsWater && terrainTemperatures[x, z] < terrain.FreezingPoint())
                            {
                                if (log)
                                    LogUtility.Log($"{terrain} freezes at {cell} (t = {terrainTemperatures[x, z]:F1}C)");
                                map.terrainGrid.SetTerrain(cell, TerrainDefOf.Ice);
                                map.terrainGrid.SetUnderTerrain(cell, terrain);
                            }
                            else if (terrainTemperatures[x, z] > TemperatureUtility.MinFreezingTemperature && terrain == TerrainDefOf.Ice)
                            {
                                TerrainDef meltedTerrain = cell.BestWaterTerrain(map);
                                if (terrainTemperatures[x, z] > meltedTerrain.FreezingPoint())
                                {
                                    if (map.terrainGrid.UnderTerrainAt(cell) == null)
                                        map.terrainGrid.SetUnderTerrain(cell, meltedTerrain);
                                    if (log)
                                        LogUtility.Log($"Ice melts at {cell} into {map.terrainGrid.UnderTerrainAt(cell)?.defName} (t = {terrainTemperatures[x, z]:F1}C)");
                                    map.terrainGrid.RemoveTopLayer(cell, false);
                                }
                            }
                        }
                    }

                    // Default environment temperature
                    if (TryGetEnvironmentTemperatureForCell(cell, out float environmentTemperature))
                        newTemperatures[x, z] += TemperatureUtility.EnvironmentDiffusionTemperatureChange(newTemperatures[x, z], environmentTemperature, cellProps, log);

                    // Snow melting
                    if (cell.GetSnowDepth(map) > 0)
                        map.snowGrid.AddDepth(cell, -TemperatureUtility.MeltAmountAt(temperatures[x, z]));

                    // Autoignition
                    if (Settings.AutoignitionEnabled && temperatures[x, z] > MinIgnitionTemperature)
                    {
                        float fireSize = 0;
                        for (int i = 0; i < cell.GetThingList(map).Count; i++)
                        {
                            Thing thing = cell.GetThingList(map)[i];
                            if (thing.FireBulwark)
                            {
                                fireSize = 0;
                                break;
                            }
                            float ignitionTemp = thing.GetStatValue(DefOf.IgnitionTemperature);
                            if (ignitionTemp >= MinIgnitionTemperature && temperatures[x, z] >= ignitionTemp)
                            {
                                LogUtility.Log($"{thing} spontaneously ignites at {temperatures[x, z]:F1}C! Autoignition temperature is {ignitionTemp:F0}C.");
                                fireSize += 0.1f * thing.GetStatValue(StatDefOf.Flammability);
                            }
                        }

                        if (fireSize > 0)
                            FireUtility.TryStartFireIn(cell, map, fireSize);
                    }

                    if (Settings.ShowTemperatureMap)
                        if (newTemperatures[x, z] < minTemperature)
                            minTemperature = newTemperatures[x, z];
                        else if (newTemperatures[x, z] > maxTemperature)
                            maxTemperature = newTemperatures[x, z];
                }

            temperatures = newTemperatures;
            overlayDrawer.SetDirty();

            if (Settings.DebugMode)
            {
                updateStopwatch.Stop();
                LogUtility.Log($"Updated temperatures for {map} on tick {Find.TickManager.TicksGame} in {updateStopwatch.Elapsed.TotalMilliseconds / ++tickIterations:N0} ms.");
            }
        }

        public bool TryGetEnvironmentTemperatureForCell(IntVec3 cell, out float temperature)
        {
            RoofDef roof = cell.GetRoof(map);
            if (cell.GetFirstMineable(map) != null && (roof == RoofDefOf.RoofRockThick || roof == RoofDefOf.RoofRockThin))
            {
                temperature = TemperatureTuning.DeepUndergroundTemperature;
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

        public void SetTempteratureForCell(IntVec3 cell, float temperature) => temperatures[cell.x, cell.z] = Mathf.Max(temperature, -273);

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
