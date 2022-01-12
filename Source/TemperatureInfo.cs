using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace TemperaturesPlus
{
    public class TemperatureInfo : MapComponent
    {
        const int ticksPerUpdate = 250;
        const int secondsPerUpdate = 3600 * ticksPerUpdate / 2500;
        public const float convectionConductivityEffect = 100;
        const float heatPushEffect = 10;
        const float defaultTempEffect = 0.1f;
        const float thingTempEffect = 1;
        const float minTempDifferenceForUpdate = 0.05f;

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

        static float GetConvectionFactor(bool convection) => convection ? convectionConductivityEffect : 1;

        float TemperatureChange(float oldTemp, ThingThermalProperties targetProps, float neighbourTemp, ThingThermalProperties neighbourProps, bool convection, bool log = false)
        {
            float finalTemp = GenMath.WeightedAverage(oldTemp, targetProps.heatCapacity, neighbourTemp, neighbourProps.heatCapacity);
            float conductivity = Mathf.Sqrt(targetProps.conductivity * neighbourProps.conductivity * GetConvectionFactor(convection));
            float lerpFactor = 1 - Mathf.Pow(1 - conductivity / targetProps.heatCapacity, secondsPerUpdate);
            if (log)
            {
                LogUtility.Log($"- Neighbour: t = {neighbourTemp:F1}C, capacity = {neighbourProps.heatCapacity}, conductivity = {neighbourProps.conductivity}, convection = {convection}");
                LogUtility.Log($"- Final temperature: {finalTemp:F1}C. Overall conductivity: {conductivity:F1}. Lerp factor: {lerpFactor:P1}.");
            }
            return lerpFactor * (finalTemp - oldTemp);
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
                    ThingThermalProperties cellThermalProperties = cell.GetThermalProperties(map);
                    float change = 0;
                    //float capacity = cellThermalProperties.heatCapacity;
                    //float conductivity = cellThermalProperties.conductivity;

                    // Diffusion & convection
                    bool isAir = cell.IsAir(map);
                    //List<IntVec3> neighbours = cell.AdjacentCells().ToList();

                    if (log)
                        LogUtility.Log(CellInfo(cell));
                    foreach (IntVec3 neighbour in cell.AdjacentCells())
                    {
                        if (log)
                            LogUtility.Log($"Calculating for neighbour {neighbour}");
                        float resultTemp = TemperatureChange(newTemperatures[i, j], cellThermalProperties, GetTemperatureForCell(neighbour), neighbour.GetThermalProperties(map), isAir && neighbour.IsAir(map), log);
                        change += resultTemp;
                        if (log)
                            LogUtility.Log($"Temperature change: {resultTemp:F2}C");
                    }

                    // Default environment temperature
                    if (TryGetEnvironmentTemperatureForCell(cell, out float environmentTemperature))
                    {
                        if (log)
                            LogUtility.Log($"Cell's environment temperature is {environmentTemperature:F1}C.");
                        change += TemperatureChange(newTemperatures[i, j], cellThermalProperties, environmentTemperature, cellThermalProperties, isAir, log);
                    }

                    // Things in cell
                    foreach (ThingWithComps thing in cell.GetThingList(map).OfType<ThingWithComps>())
                    {
                        CompHeatPusher heatPusher = thing.GetComp<CompHeatPusher>();
                        if (heatPusher != null)
                        {
                            float heatPush = heatPusher.Props.heatPerSecond;
                            change += heatPush * heatPushEffect * ticksPerUpdate / cellThermalProperties.heatCapacity;
                            if (log && heatPush != 0)
                                LogUtility.Log($"Heat push: {heatPush}.");
                        }

                        CompThermal compThermal = thing.GetComp<CompThermal>();
                        if (compThermal != null && compThermal.HasTemperature)
                        {
                            float resultTemp = TemperatureChange(compThermal.temperature, compThermal.ThermalProperties, newTemperatures[i, j], cellThermalProperties, false, log);
                            float resultTemp2 = TemperatureChange(newTemperatures[i, j], cellThermalProperties, compThermal.temperature, compThermal.ThermalProperties, false, log);
                            if (log)
                                LogUtility.Log($"{thing.def.defName} has temperature {compThermal.temperature:F1}C and heat capacity {compThermal.ThermalProperties.heatCapacity}. Thing temp change: {resultTemp:F1}C. Cell temp change: {resultTemp2:F1}C.");
                            compThermal.temperature += resultTemp;
                            newTemperatures[i, j] += resultTemp2;
                        }
                    }

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
    }
}
