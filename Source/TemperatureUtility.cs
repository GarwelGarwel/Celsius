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

        public static ThingThermalProperties GetThermalProperties(Thing thing) => thing.TryGetComp<CompThermal>()?.ThermalProperties ?? new ThingThermalProperties();

        internal static bool IsAir(this IntVec3 cell, Map map) => cell.GetThermalProperties(map) == ThingThermalProperties.Air;

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
            return comp != null && comp.ThermalProperties.heatCapacity > 0 ? comp.temperature : thing.Position.GetTemperatureForCell(thing.Map);
        }

        public static float GetHeatPush(this Thing thing)
        {
            CompHeatPusher heatPusher = thing.TryGetComp<CompHeatPusher>();
            if (heatPusher != null)
                return heatPusher.Props.heatPerSecond;
            if (thing is Fire)
                return FireHeatPush;
            return 0;
        }

        public static ThingDef GetUnderlyingStuff(this Thing thing) => thing.Stuff ?? thing.def.defaultStuff;

        internal static float GetConvectionFactor(bool convection) => convection ? TemperaturesPlus.TemperatureInfo.convectionConductivityEffect : 1;

        internal static float TemperatureChange(float oldTemp, ThingThermalProperties targetProps, float neighbourTemp, ThingThermalProperties neighbourProps, bool convection, bool log = false)
        {
            float finalTemp = GenMath.WeightedAverage(oldTemp, targetProps.heatCapacity, neighbourTemp, neighbourProps.heatCapacity);
            float conductivity = Mathf.Sqrt(targetProps.conductivity * neighbourProps.conductivity * GetConvectionFactor(convection));
            float lerpFactor = 1 - Mathf.Pow(1 - conductivity / targetProps.heatCapacity, TemperaturesPlus.TemperatureInfo.secondsPerUpdate);
            if (log)
            {
                LogUtility.Log($"- Neighbour: t = {neighbourTemp:F1}C, capacity = {neighbourProps.heatCapacity}, conductivity = {neighbourProps.conductivity}, convection = {convection}");
                LogUtility.Log($"- Final temperature: {finalTemp:F1}C. Overall conductivity: {conductivity:F1}. Lerp factor: {lerpFactor:P1}.");
            }
            return lerpFactor * (finalTemp - oldTemp);
        }

        internal static (float, float) TemperatureChangeMutual(float temp1, ThingThermalProperties props1, float temp2, ThingThermalProperties props2, bool convection, bool log = false)
        {
            float finalTemp = GenMath.WeightedAverage(temp1, props1.heatCapacity, temp2, props2.heatCapacity);
            float conductivity = Mathf.Sqrt(props1.conductivity * props2.conductivity * GetConvectionFactor(convection));
            float lerpFactor1 = 1 - Mathf.Pow(1 - conductivity / props1.heatCapacity, TemperaturesPlus.TemperatureInfo.secondsPerUpdate);
            float lerpFactor2 = 1 - Mathf.Pow(1 - conductivity / props2.heatCapacity, TemperaturesPlus.TemperatureInfo.secondsPerUpdate);
            if (log)
            {
                LogUtility.Log($"- Object 1: t = {temp1:F1}C, capacity = {props1.heatCapacity}, conductivity = {props1.conductivity}, convection = {convection}");
                LogUtility.Log($"- Object 2: t = {temp2:F1}C, capacity = {props2.heatCapacity}, conductivity = {props2.conductivity}");
                LogUtility.Log($"- Final temperature: {finalTemp:F1}C. Overall conductivity: {conductivity:F1}. Lerp factor 1: {lerpFactor1:P1}. Lerp factor 2: {lerpFactor2:P1}.");
            }
            return (lerpFactor1 * (finalTemp - temp1), lerpFactor2 * (finalTemp - temp2));
        }
    }
}
