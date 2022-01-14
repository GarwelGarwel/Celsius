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
        const float heatPushEffect = 10;
        const float defaultTempEffect = 0.1f;
        const float thingTempEffect = 1;
        const float minTempDifferenceForUpdate = 0.05f;
        const float minIgnitionTemperature = 0;

        float[,] temperatures;

        System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
        int iterations;

        public TemperatureInfo(Map map)
            :base(map)
        {
            temperatures = new float[map.Size.x, map.Size.z];
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
        }

        public override void MapComponentOnGUI()
        {
            if (!Prefs.DevMode)
                return;
            IntVec3 cell = UI.MouseCell();
            if (!cell.InBounds(map))
                return;
            Text.Font = GameFont.Tiny;
            Widgets.Label(new Rect(UI.MousePositionOnUIInverted.x + 20, UI.MousePositionOnUIInverted.y + 20, 100, 40), $"Temp: {GetTemperatureForCell(cell).ToStringTemperature()}");
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
            bool log;

            LogUtility.Log($"Updating temperatures for {map} on tick {Find.TickManager.TicksGame}.");
            float[,] newTemperatures = (float[,])temperatures.Clone();
            for (int i = 0; i < map.Size.x; i++)
                for (int j = 0; j < map.Size.z; j++)
                {
                    IntVec3 cell = new IntVec3(i, 0, j);
                    log = Prefs.DevMode && cell == UI.MouseCell();
                    ThingThermalProperties cellProps = cell.GetThermalProperties(map);
                    bool isAir = cell.IsAir(map);
                    float change = 0;

                    // Diffusion & convection

                    if (log)
                        LogUtility.Log(CellInfo(cell));
                    foreach (IntVec3 neighbour in cell.AdjacentCells())
                    {
                        if (log)
                            LogUtility.Log($"Calculating for neighbour {neighbour}");
                        float resultTemp = TemperatureUtility.TemperatureChange(newTemperatures[i, j], cellProps, GetTemperatureForCell(neighbour), neighbour.GetThermalProperties(map), isAir && neighbour.IsAir(map), log);
                        change += resultTemp;
                        if (log)
                            LogUtility.Log($"Temperature change: {resultTemp:F2}C");
                    }

                    // Default environment temperature
                    if (TryGetEnvironmentTemperatureForCell(cell, out float environmentTemperature))
                    {
                        if (log)
                            LogUtility.Log($"Cell's environment temperature is {environmentTemperature:F1}C.");
                        change += TemperatureUtility.TemperatureChange(newTemperatures[i, j], cellProps, environmentTemperature, cellProps, isAir, log);
                    }

                    bool canIgnite = true;
                    float fireSize = 0;

                    // Things in cell
                    foreach (Thing thing in cell.GetThingList(map))
                    {
                        // Heat pushers (fire, heaters, coolers, geysers etc.)
                        //CompHeatPusher heatPusher = thing.GetComp<CompHeatPusher>();
                        //if (heatPusher != null)
                        //{
                            float heatPush = thing.GetHeatPush();//heatPusher.Props.heatPerSecond;
                        if (heatPush != 0)
                        {
                            change += heatPush * heatPushEffect * ticksPerUpdate / cellProps.heatCapacity;
                            if (log)
                                LogUtility.Log($"Heat push: {heatPush}.");
                        }

                        // Updating temperature of fully simulated things
                        CompThermal compThermal = thing.TryGetComp<CompThermal>();
                        if (compThermal != null && compThermal.HasTemperature)
                        {
                            (float, float) tempChange = TemperatureUtility.TemperatureChangeMutual(compThermal.temperature, compThermal.ThermalProperties, newTemperatures[i, j], cellProps, false, log);
                            if (log)
                                LogUtility.Log($"(Tick {Find.TickManager.TicksGame}) {thing} has temperature {compThermal.temperature:F1}C and heat capacity {compThermal.ThermalProperties.heatCapacity}. Thing temp change: {tempChange.Item1:F1}C. Cell temp change: {tempChange.Item2:F1}C.");
                            compThermal.temperature += tempChange.Item1;
                            newTemperatures[i, j] += tempChange.Item2;
                        }

                        // Autoignition
                        if (thing.FireBulwark)
                            canIgnite = false;
                        else
                        {
                            float ignitionTemp = thing.GetStatValue(DefOf.IgnitionTemperature);
                            if (canIgnite && ignitionTemp > minIgnitionTemperature && thing.GetTemperature() >= thing.GetStatValue(DefOf.IgnitionTemperature))
                            {
                                LogUtility.Log($"{thing} spontaneously ignites at {thing.GetTemperature():F1}C! Ignition temperature is {ignitionTemp:F0}C.");
                                fireSize += 0.1f * thing.GetStatValue(StatDefOf.Flammability);
                            }
                        }
                    }

                    if (canIgnite && fireSize > 0)
                        FireUtility.TryStartFireIn(cell, map, fireSize);

                    newTemperatures[i, j] += change;
                }

            temperatures = newTemperatures;

            if (Prefs.DevMode)
            {
                stopwatch.Stop();
                LogUtility.Log($"{stopwatch.Elapsed.TotalMilliseconds / ++iterations:N0} ms per update.");
            }
        }

        public bool TryGetEnvironmentTemperatureForCell(IntVec3 cell, out float temperature)
        {
            if (cell.GetFirstMineable(map) != null && (cell.GetRoof(map) == RoofDefOf.RoofRockThick || cell.GetRoof(map) == RoofDefOf.RoofRockThin))
            {
                temperature = TemperatureTuning.DeepUndergroundTemperature;
                return true;
            }
            temperature = map.mapTemperature.OutdoorTemp;
            return !cell.InBounds(map) || (!cell.Roofed(map) && cell.IsAir(map));
        }

        public float GetTemperatureForCell(IntVec3 cell) =>
            cell.InBounds(map) && temperatures != null ? temperatures[cell.x, cell.z] : map.mapTemperature.OutdoorTemp;

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
