using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace TemperaturesPlus
{
    public class TemperatureInfo : MapComponent
    {
        public const int ticksPerUpdate = 250;
        public const int secondsPerUpdate = 3600 * ticksPerUpdate / 2500;
        public const float convectionConductivityEffect = 100;
        public const float HeatPushEffect = 5;
        const float defaultTempEffect = 0.1f;
        const float thingTempEffect = 1;
        const float minIgnitionTemperature = 100;

        float[,] temperatures;
        float[,] terrainTemperatures;

        System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
        int iterations;

        public TemperatureInfo(Map map)
            :base(map)
        {
            temperatures = new float[map.Size.x, map.Size.z];
            terrainTemperatures = new float[map.Size.x, map.Size.z];
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
                }
            LogUtility.Log($"TemperatureInfo initialized for {map}.");
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref temperatures, "temperatures");
            Scribe_Values.Look(ref terrainTemperatures, "terrainTemperatures");
        }

        public override void MapComponentOnGUI()
        {
            if (!Prefs.DevMode)
                return;
            IntVec3 cell = UI.MouseCell();
            if (!cell.InBounds(map))
                return;
            Text.Font = GameFont.Tiny;
            string tooltip = $"Air: {GetTemperatureForCell(cell).ToStringTemperature()}";
            if (cell.HasTerrainTemperature(map))
                tooltip += $"\nTerrain: {GetTerrainTemperature(cell).ToStringTemperature()}";
            Widgets.Label(new Rect(UI.MousePositionOnUIInverted.x + 20, UI.MousePositionOnUIInverted.y + 20, 100, 40), tooltip);
        }

        string CellInfo(IntVec3 cell) =>
            $"Cell {cell}. Temperature: {GetTemperatureForCell(cell):F1}C. Capacity: {cell.GetHeatCapacity(map)}. Conductivity: {cell.GetHeatConductivity(map)}.";

        ThingThermalProperties NeighboursProps(IntVec3 cell)
        {
            ThingThermalProperties props = new ThingThermalProperties() { replacesAirProperties = true };
            bool isAir = cell.IsAir(map);
            List<IntVec3> neighbours = cell.AdjacentCells().ToList();
            props.heatCapacity = neighbours.Sum(c => c.GetHeatCapacity(map));
            props.conductivity = neighbours.Sum(c => c.GetHeatConductivity(map, isAir && c.IsAir(map)));
            return props;
        }

        public override void MapComponentTick()
        {
            if (Find.TickManager.TicksGame % ticksPerUpdate != 0)
                return;

            if (Prefs.DevMode)
                stopwatch.Start();

            IntVec3 mouseCell = UI.MouseCell();
            bool log;
            float[,] newTemperatures = (float[,])temperatures.Clone();
            for (int x = 0; x < map.Size.x; x++)
                for (int z = 0; z < map.Size.z; z++)
                {
                    IntVec3 cell = new IntVec3(x, 0, z);
                    log = Prefs.DevMode && cell == mouseCell;
                    ThingThermalProperties cellProps = cell.GetThermalProperties(map);
                    bool isAir = cell.IsAir(map);
                    float change = 0;
                    if (log)
                        LogUtility.Log(CellInfo(cell));

                    // Diffusion & convection
                    foreach (IntVec3 neighbour in cell.AdjacentCells())
                        change += TemperatureUtility.TemperatureChange(
                            newTemperatures[x, z],
                            cellProps,
                            GetTemperatureForCell(neighbour),
                            neighbour.GetThermalProperties(map),
                            isAir && neighbour.IsAir(map) ? convectionConductivityEffect : 1,
                            log);

                    // Default environment temperature
                    if (TryGetEnvironmentTemperatureForCell(cell, out float environmentTemperature))
                        change += TemperatureUtility.TemperatureChange(newTemperatures[x, z], cellProps, environmentTemperature, cellProps, isAir ? convectionConductivityEffect : 1, log);

                    // Terrain temperature
                    TerrainDef terrain = cell.GetTerrain(map);
                    ThingThermalProperties terrainProps = terrain.GetModExtension<ThingThermalProperties>();
                    if (terrainProps != null && terrainProps.heatCapacity > 0)
                    {
                        if (log)
                            LogUtility.Log($"Terrain {terrain}. {terrainProps} Temperature: {GetTerrainTemperature(cell):F1}C.");
                        (float, float) tempChange = TemperatureUtility.TemperatureChangeMutual(GetTerrainTemperature(cell), terrainProps, newTemperatures[x, z], cellProps, 1, log);
                        if (log)
                            LogUtility.Log($"Terrain temp change: {tempChange.Item1:F1}C. Cell temp change: {tempChange.Item2:F1}C.");
                        terrainTemperatures[x, z] += tempChange.Item1;
                        newTemperatures[x, z] += tempChange.Item2;

                        // Freezing and melting
                        if (terrain.IsWater && terrainTemperatures[x, z] < terrain.FreezingPoint())
                        {
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
                                LogUtility.Log($"Ice melts at {cell} into {map.terrainGrid.UnderTerrainAt(cell)?.defName} (t = {terrainTemperatures[x, z]:F1}C)");
                                map.terrainGrid.RemoveTopLayer(cell, false);
                            }
                        }
                    }

                    bool canIgnite = true;
                    float fireSize = 0;

                    // Things in cell
                    for (int i = 0; i < cell.GetThingList(map).Count; i++)
                    {
                        Thing thing = cell.GetThingList(map)[i];
                        float temperature = temperatures[x, z];

                        // Updating temperature of fully simulated things
                        CompThermal compThermal = thing.TryGetComp<CompThermal>();
                        if (compThermal != null && compThermal.HasTemperature)
                        {
                            temperature = compThermal.temperature;
                            (float, float) tempChange = TemperatureUtility.TemperatureChangeMutual(compThermal.temperature, compThermal.ThermalProperties, newTemperatures[x, z], cellProps, 1, log);
                            if (log)
                                LogUtility.Log($"{thing} has temperature {compThermal.temperature:F1}C and heat capacity {compThermal.ThermalProperties.heatCapacity}. Thing temp change: {tempChange.Item1:F1}C. Cell temp change: {tempChange.Item2:F1}C.");
                            compThermal.temperature += tempChange.Item1;
                            newTemperatures[x, z] += tempChange.Item2;
                        }

                        // Autoignition
                        if (thing.FireBulwark)
                            canIgnite = false;
                        else if (temperature > minIgnitionTemperature)
                        {
                            float ignitionTemp = thing.GetStatValue(DefOf.IgnitionTemperature);
                            if (canIgnite && compThermal != null && ignitionTemp > minIgnitionTemperature && temperature >= ignitionTemp)
                            {
                                LogUtility.Log($"{thing} spontaneously ignites at {temperature:F1}C! Autoignition temperature is {ignitionTemp:F0}C.");
                                fireSize += 0.1f * thing.GetStatValue(StatDefOf.Flammability);
                            }
                        }
                    }

                    if (canIgnite && fireSize > 0)
                        FireUtility.TryStartFireIn(cell, map, fireSize);

                    newTemperatures[x, z] += change;
                }

            temperatures = newTemperatures;

            if (Prefs.DevMode)
            {
                stopwatch.Stop();
                LogUtility.Log($"Updated temperatures for {map} on tick {Find.TickManager.TicksGame} in {stopwatch.Elapsed.TotalMilliseconds / ++iterations:N0} ms.");
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
            return roof == null && cell.IsAir(map);
        }

        public float GetTemperatureForCell(IntVec3 cell) =>
            cell.InBounds(map) && temperatures != null ? temperatures[cell.x, cell.z] : map.mapTemperature.OutdoorTemp;

        public float GetTerrainTemperature(IntVec3 cell) =>
          cell.InBounds(map) && terrainTemperatures != null && cell.HasTerrainTemperature(map) ? terrainTemperatures[cell.x, cell.z] : GetTemperatureForCell(cell);

        public void SetTempteratureForCell(IntVec3 cell, float temperature)
        {
            if (cell.InBounds(map) && temperatures != null)
                temperatures[cell.x, cell.z] = temperature;
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
                    if (ignitionTemperature > minIgnitionTemperature)
                        min = Mathf.Min(min, ignitionTemperature);
                }
            }
            return min;
        }
    }
}
