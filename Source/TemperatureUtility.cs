using RimWorld;
using System.Linq;
using UnityEngine;
using Verse;

namespace TemperaturesPlus
{
    enum CellMaterialType
    {
        Air = 0,
        Rock,
        Structure
    };

    static class TemperatureUtility
    {
        const float FireHeatPush = 15;
        const float PawnHeatPush = 0.2f;
        const float TemperatureChangePrecision = 0.01f;
        public const float MinFreezingTemperature = -3;

        public static TemperatureInfo TemperatureInfo(this Map map) => map.GetComponent<TemperatureInfo>();

        public static float GetTemperatureForCell(this IntVec3 cell, Map map)
        {
            TemperatureInfo tempInfo = map.TemperatureInfo();
            if (tempInfo == null)
                return map.mapTemperature.OutdoorTemp;
            return tempInfo.GetTemperatureForCell(cell);
        }

        public static ThingThermalProperties GetThermalProperties(this IntVec3 cell, Map map)
        {
            ThingThermalProperties thermalProps = null;
            if (cell.InBounds(map))
                thermalProps = cell.GetThingList(map)
                    .Select(thing => thing.TryGetComp<CompThermal>()?.ThermalProperties)
                    .FirstOrDefault(props => props != null && props.replacesAirProperties);
            return thermalProps ?? ThingThermalProperties.Air;
        }

        internal static bool IsAir(this IntVec3 cell, Map map) => cell.GetThermalProperties(map) == ThingThermalProperties.Air;

        public static ThingThermalProperties GetTerrainThermalProperties(this IntVec3 cell, Map map) =>
            cell.GetTerrain(map).GetModExtension<ThingThermalProperties>() ?? ThingThermalProperties.Empty;

        public static bool HasTerrainTemperature(this IntVec3 cell, Map map) => cell.GetTerrainThermalProperties(map).heatCapacity > 0;

        public static float FreezingPoint(this TerrainDef water)
        {
            switch (water.defName)
            {
                case "WaterOceanDeep":
                case "WaterOceanShallow":
                    return -2;

                case "WaterMovingChestDeep":
                    return -3;

                case "WaterMovingShallow":
                    return -2;
            }
            return 0;
        }

        /// <summary>
        /// Returns heat capacity for a cell
        /// </summary>
        public static float GetHeatCapacity(this IntVec3 cell, Map map) => cell.GetThermalProperties(map).heatCapacity;

        public static float GetHeatConductivity(this IntVec3 cell, Map map, bool convection = false)
        {
            ThingThermalProperties modEx = cell.GetThermalProperties(map);
            return convection ? modEx.conductivity * TemperaturesPlus.TemperatureInfo.convectionConductivityEffect : modEx.conductivity;
        }

        public static float GetTemperature(this Thing thing)
        {
            CompThermal comp = thing.TryGetComp<CompThermal>();
            return comp != null && comp.HasTemperature ? comp.temperature : thing.Position.GetTemperatureForCell(thing.Map);
        }

        public static float GetHeatPush(this Thing thing)
        {
            CompHeatPusher heatPusher = thing.TryGetComp<CompHeatPusher>();
            if (heatPusher != null)
                return heatPusher.Props.heatPerSecond;
            if (thing is Pawn pawn && !pawn.Dead && !pawn.RaceProps.Insect)
                return pawn.BodySize * PawnHeatPush;
            if (thing is Fire)
                return FireHeatPush;
            return 0;
        }

        public static ThingDef GetUnderlyingStuff(this Thing thing) => thing.Stuff ?? thing.def.defaultStuff;

        internal static float TemperatureChange(float oldTemp, ThingThermalProperties targetProps, float neighbourTemp, ThingThermalProperties neighbourProps, float convectionFactor, bool log = false)
        {
            if (Mathf.Abs(oldTemp - neighbourTemp) < TemperatureChangePrecision)
                return 0;
            float finalTemp = GenMath.WeightedAverage(oldTemp, targetProps.heatCapacity, neighbourTemp, neighbourProps.heatCapacity);
            float conductivity = Mathf.Sqrt(targetProps.conductivity * neighbourProps.conductivity * convectionFactor);
            float lerpFactor = 1 - Mathf.Pow(1 - conductivity / targetProps.heatCapacity, TemperaturesPlus.TemperatureInfo.secondsPerUpdate);
            if (log)
            {
                LogUtility.Log($"- Neighbour: t = {neighbourTemp:F1}C, capacity = {neighbourProps.heatCapacity}, conductivity = {neighbourProps.conductivity}, convection factor = {convectionFactor:F1}");
                LogUtility.Log($"- Final temperature: {finalTemp:F1}C. Overall conductivity: {conductivity:F1}. Lerp factor: {lerpFactor:P1}.");
            }
            return lerpFactor * (finalTemp - oldTemp);
        }

        internal static (float, float) TemperatureChangeMutual(float temp1, ThingThermalProperties props1, float temp2, ThingThermalProperties props2, float convectionFactor, bool log = false)
        {
            if (Mathf.Abs(temp1 - temp2) < TemperatureChangePrecision)
                return (0, 0);
            float finalTemp = GenMath.WeightedAverage(temp1, props1.heatCapacity, temp2, props2.heatCapacity);
            float conductivity = Mathf.Sqrt(props1.conductivity * props2.conductivity * convectionFactor);
            float lerpFactor1 = 1 - Mathf.Pow(1 - conductivity / props1.heatCapacity, TemperaturesPlus.TemperatureInfo.secondsPerUpdate);
            float lerpFactor2 = 1 - Mathf.Pow(1 - conductivity / props2.heatCapacity, TemperaturesPlus.TemperatureInfo.secondsPerUpdate);
            if (log)
            {
                LogUtility.Log($"- Object 1: t = {temp1:F1}C, capacity = {props1.heatCapacity}, conductivity = {props1.conductivity}, convection = {convectionFactor:F1}");
                LogUtility.Log($"- Object 2: t = {temp2:F1}C, capacity = {props2.heatCapacity}, conductivity = {props2.conductivity}");
                LogUtility.Log($"- Final temperature: {finalTemp:F1}C. Overall conductivity: {conductivity:F1}. Lerp factor 1: {lerpFactor1:P1}. Lerp factor 2: {lerpFactor2:P1}.");
            }
            return (lerpFactor1 * (finalTemp - temp1), lerpFactor2 * (finalTemp - temp2));
        }
    }
}
