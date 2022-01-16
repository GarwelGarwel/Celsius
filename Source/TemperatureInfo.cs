﻿using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace Celsius
{
    public class TemperatureInfo : MapComponent
    {
        public const int TicksPerUpdate = 250;
        const int UpdateTickOffset = 18;
        public const int SecondsPerUpdate = 3600 * TicksPerUpdate / 2500;
        const float MinIgnitionTemperature = 100;

        float[,] temperatures;
        float[,] terrainTemperatures;

        float minTemperature = TemperatureTuning.DefaultTemperature - 20, maxTemperature = TemperatureTuning.DefaultTemperature + 20;
        CellBoolDrawer overlayDrawer;
        Color minComfortableColor = new Color(0, 0.5f, 0.5f);
        Color maxComfortableColor = new Color(0.5f, 0.5f, 0);

        System.Diagnostics.Stopwatch tickStopwatch = new System.Diagnostics.Stopwatch();
        int tickIterations;

        public TemperatureInfo(Map map)
            : base(map)
        {
            temperatures = new float[map.Size.x, map.Size.z];
            terrainTemperatures = new float[map.Size.x, map.Size.z];
            overlayDrawer = new CellBoolDrawer(index => !map.fogGrid.IsFogged(index), () => Color.white, index => TemperatureColorForCell(index), map.Size.x, map.Size.z);
        }

        public override void FinalizeInit()
        {
            for (int i = 0; i < temperatures.GetLength(0); i++)
                for (int j = 0; j < temperatures.GetLength(1); j++)
                {
                    IntVec3 cell = new IntVec3(i, 0, j);
                    Room room = cell.GetRoom(map);
                    if (room != null)
                        temperatures[i, j] = room.TempTracker.Temperature;
                    else TryGetEnvironmentTemperatureForCell(cell, out temperatures[i, j]);
                    if (cell.HasTerrainTemperature(map))
                        terrainTemperatures[i, j] = temperatures[i, j];
                }
            LogUtility.Log($"TemperatureInfo initialized for {map}.");
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref temperatures, "temperatures");
            Scribe_Values.Look(ref terrainTemperatures, "terrainTemperatures");
        }

        Color TemperatureColorForCell(int index)
        {
            float temperature = GetTemperatureForCell(CellIndicesUtility.IndexToCell(index, map.Size.x));
            if (temperature < TemperatureTuning.DefaultTemperature - 5)
                return Color.Lerp(Color.blue, minComfortableColor, (temperature - minTemperature) / (TemperatureTuning.DefaultTemperature - 5 - minTemperature));
            if (temperature < TemperatureTuning.DefaultTemperature + 5)
                return Color.Lerp(minComfortableColor, maxComfortableColor, (temperature - TemperatureTuning.DefaultTemperature + 5) / 10);
            return Color.Lerp(maxComfortableColor, Color.red, (maxTemperature - temperature) / (maxTemperature - TemperatureTuning.DefaultTemperature - 5));
        }

        public override void MapComponentUpdate()
        {
            base.MapComponentUpdate();
            if (Settings.ShowTemperatureMap)
                overlayDrawer.MarkForDraw();
            overlayDrawer.CellBoolDrawerUpdate();
        }

        public override void MapComponentOnGUI()
        {
            if (!Settings.ShowTemperatureMap)
                return;
            IntVec3 cell = UI.MouseCell();
            if (cell.InBounds(map) && (!cell.Fogged(map) || Prefs.DevMode))
            {
                Text.Font = GameFont.Tiny;
                string tooltip = $"Cell: {GetTemperatureForCell(cell).ToStringTemperature()}";
                if (cell.HasTerrainTemperature(map))
                    tooltip += $"\nTerrain: {GetTerrainTemperature(cell).ToStringTemperature()}";
                Widgets.Label(new Rect(UI.MousePositionOnUIInverted.x + 20, UI.MousePositionOnUIInverted.y + 20, 100, 40), tooltip);
            }
        }

        public override void MapComponentTick()
        {
            if (Find.TickManager.TicksGame % TicksPerUpdate != UpdateTickOffset)
                return;

            if (Prefs.DevMode)
                tickStopwatch.Start();

            IntVec3 mouseCell = UI.MouseCell();
            bool log;
            float[,] newTemperatures = (float[,])temperatures.Clone();
            minTemperature = TemperatureTuning.DefaultTemperature - 20;
            maxTemperature = TemperatureTuning.DefaultTemperature + 20;
            for (int x = 0; x < map.Size.x; x++)
                for (int z = 0; z < map.Size.z; z++)
                {
                    IntVec3 cell = new IntVec3(x, 0, z);
                    log = false;// Prefs.DevMode && cell == mouseCell;
                    ThingThermalProperties cellProps = cell.GetThermalProperties(map);
                    if (log)
                        LogUtility.Log($"Cell {cell}. Temperature: {GetTemperatureForCell(cell):F1}C. Capacity: {cell.GetHeatCapacity(map)}. Conductivity: {cell.GetHeatConductivity(map)}.");

                    // Diffusion & convection
                    void DiffusionWithNeighbour(IntVec3 neighbour)
                    {
                        (float, float) changes = TemperatureUtility.DiffusionTemperatureChangeMutual(
                            temperatures[x, z],
                            cellProps,
                            GetTemperatureForCell(neighbour),
                            neighbour.GetThermalProperties(map));
                        newTemperatures[x, z] += changes.Item1;
                        if (neighbour.InBounds(map))
                            newTemperatures[neighbour.x, neighbour.z] += changes.Item2;
                    }

                    DiffusionWithNeighbour(cell + IntVec3.East);
                    DiffusionWithNeighbour(cell + IntVec3.North);
                    if (x == 0)
                        DiffusionWithNeighbour(cell + IntVec3.West);
                    if (z == 0)
                        DiffusionWithNeighbour(cell + IntVec3.South);

                    // Terrain temperature
                    if (Settings.FreezingAndMeltingEnabled)
                    {
                        TerrainDef terrain = cell.GetTerrain(map);
                        ThingThermalProperties terrainProps = terrain.GetModExtension<ThingThermalProperties>();
                        if (terrainProps != null && terrainProps.heatCapacity > 0)
                        {
                            (float, float) tempChange = TemperatureUtility.DiffusionTemperatureChangeMutual(GetTerrainTemperature(cell), terrainProps, temperatures[x, z], cellProps);
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

                    bool canIgnite = true;
                    float fireSize = 0;

                    // Things in cell
                    for (int i = 0; i < cell.GetThingList(map).Count; i++)
                    {
                        Thing thing = cell.GetThingList(map)[i];
                        float temperature = newTemperatures[x, z];

                        // Updating temperature of fully simulated things
                        CompThermal compThermal = thing.TryGetComp<CompThermal>();
                        if (compThermal != null && compThermal.HasTemperature)
                        {
                            (float, float) tempChange = TemperatureUtility.DiffusionTemperatureChangeMutual(compThermal.temperature, compThermal.ThermalProperties, temperature, cellProps, log);
                            if (log)
                                LogUtility.Log($"{thing} has temperature {compThermal.temperature:F1}C and heat capacity {compThermal.ThermalProperties.heatCapacity}. Thing temp change: {tempChange.Item1:F1}C. Cell temp change: {tempChange.Item2:F1}C.");
                            temperature = compThermal.temperature;
                            compThermal.temperature += tempChange.Item1;
                            newTemperatures[x, z] += tempChange.Item2;
                        }

                        // Autoignition
                        if (!Settings.AutoignitionEnabled || thing.FireBulwark)
                            canIgnite = false;
                        else if (temperature > MinIgnitionTemperature)
                        {
                            float ignitionTemp = thing.GetStatValue(DefOf.IgnitionTemperature);
                            if (canIgnite && compThermal != null && ignitionTemp > MinIgnitionTemperature && temperature >= ignitionTemp)
                            {
                                LogUtility.Log($"{thing} spontaneously ignites at {temperature:F1}C! Autoignition temperature is {ignitionTemp:F0}C.");
                                fireSize += 0.1f * thing.GetStatValue(StatDefOf.Flammability);
                            }
                        }
                    }

                    if (canIgnite && fireSize > 0)
                        FireUtility.TryStartFireIn(cell, map, fireSize);

                    // Default environment temperature
                    if (TryGetEnvironmentTemperatureForCell(cell, out float environmentTemperature))
                        newTemperatures[x, z] += TemperatureUtility.DiffusionTemperatureChangeSingle(newTemperatures[x, z], environmentTemperature, cellProps, log);

                    if (newTemperatures[x, z] < minTemperature)
                        minTemperature = newTemperatures[x, z];
                    else if (newTemperatures[x, z] > maxTemperature)
                        maxTemperature = newTemperatures[x, z];
                }

            temperatures = newTemperatures;
            overlayDrawer.SetDirty();

            if (Prefs.DevMode)
            {
                tickStopwatch.Stop();
                LogUtility.Log($"Updated temperatures for {map} on tick {Find.TickManager.TicksGame} in {tickStopwatch.Elapsed.TotalMilliseconds / ++tickIterations:N0} ms.");
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

        public float GetTemperatureForCell(IntVec3 cell) =>
            cell.InBounds(map) && temperatures != null ? temperatures[cell.x, cell.z] : map.mapTemperature.OutdoorTemp;

        public float GetTerrainTemperature(IntVec3 cell) =>
            cell.InBounds(map) && terrainTemperatures != null && cell.HasTerrainTemperature(map) ? terrainTemperatures[cell.x, cell.z] : GetTemperatureForCell(cell);

        public void SetTempteratureForCell(IntVec3 cell, float temperature)
        {
            if (cell.InBounds(map) && temperatures != null)
                temperatures[cell.x, cell.z] = Mathf.Max(temperature, -273);
        }

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
