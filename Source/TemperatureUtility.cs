using RimWorld;
using System.Linq;
using UnityEngine;
using Verse;

namespace Celsius
{
    static class TemperatureUtility
    {
        public const float TemperatureChangePrecision = 0.01f;
        public const float MinFreezingTemperature = -3;
        public const float convectionConductivityEffect = 10;

        public static TemperatureInfo TemperatureInfo(this Map map) => map.GetComponent<TemperatureInfo>();

        #region TEMPERATURE

        public static float GetTemperatureForCell(this IntVec3 cell, Map map)
        {
            TemperatureInfo tempInfo = map.TemperatureInfo();
            if (tempInfo == null)
                return map.mapTemperature.OutdoorTemp;
            return tempInfo.GetTemperatureForCell(cell);
        }

        public static float GetTemperature(this Thing thing)
        {
            CompThermal comp = thing.TryGetComp<CompThermal>();
            return comp != null && comp.HasTemperature ? comp.temperature : thing.Position.GetTemperatureForCell(thing.Map);
        }

        public static float GetTemperature(this Room room)
        {
            if (room == null)
            {
                LogUtility.Log($"Trying to GetTemperature for null room!", LogLevel.Error);
                return TemperatureTuning.DefaultTemperature;
            }
            TemperatureInfo temperatureInfo = room.Map?.TemperatureInfo();
            if (temperatureInfo == null)
            {
                LogUtility.Log($"TemperatureInfo unavailable for {room.Map}.", LogLevel.Error);
                return room.Temperature;
            }
            if (room.TouchesMapEdge)
                return room.Map.mapTemperature.OutdoorTemp;
            return room.Cells.Average(cell => temperatureInfo.GetTemperatureForCell(cell));
        }

        #endregion TEMPERATURE

        #region DIFFUSION

        static float airConductivity = ThingThermalProperties.Air.conductivity * convectionConductivityEffect;

        static float airLerpFactor = 1 - Mathf.Pow(1 - airConductivity / ThingThermalProperties.Air.heatCapacity, Celsius.TemperatureInfo.secondsPerUpdate);

        internal static float DiffusionTemperatureChangeSingle(float oldTemp, ThingThermalProperties targetProps, float neighbourTemp, ThingThermalProperties neighbourProps, bool log = false)
        {
            if (Mathf.Abs(oldTemp - neighbourTemp) < TemperatureChangePrecision)
                return 0;
            float finalTemp = GenMath.WeightedAverage(oldTemp, targetProps.heatCapacity, neighbourTemp, neighbourProps.heatCapacity);
            float conductivity, lerpFactor;
            if (targetProps == ThingThermalProperties.Air && neighbourProps == ThingThermalProperties.Air)
            {
                conductivity = airConductivity;
                lerpFactor = airLerpFactor;
            }
            else
            {
                conductivity = Mathf.Sqrt(targetProps.conductivity * neighbourProps.conductivity);
                lerpFactor = 1 - Mathf.Pow(1 - conductivity / targetProps.heatCapacity, Celsius.TemperatureInfo.secondsPerUpdate);
            }

            if (log)
            {
                LogUtility.Log($"- Neighbour: t = {neighbourTemp:F1}C, capacity = {neighbourProps.heatCapacity}, conductivity = {neighbourProps.conductivity}");
                LogUtility.Log($"- Final temperature: {finalTemp:F1}C. Overall conductivity: {conductivity:F1}. Lerp factor: {lerpFactor:P1}.");
            }

            return lerpFactor * (finalTemp - oldTemp);
        }

        internal static (float, float) DiffusionTemperatureChangeMutual(float temp1, ThingThermalProperties props1, float temp2, ThingThermalProperties props2, bool log = false)
        {
            if (Mathf.Abs(temp1 - temp2) < TemperatureChangePrecision)
                return (0, 0);
            float finalTemp = GenMath.WeightedAverage(temp1, props1.heatCapacity, temp2, props2.heatCapacity);
            float conductivity, lerpFactor1, lerpFactor2;
            if (props1 == ThingThermalProperties.Air && props2 == ThingThermalProperties.Air)
            {
                conductivity = airConductivity;
                lerpFactor1 = lerpFactor2 = airLerpFactor;
            }
            else if (props1 == props2)
            {
                LogUtility.Log($"Both objects have the same thermal props: {props1}");
                conductivity = props1.conductivity;
                lerpFactor1 = lerpFactor2 = 1 - Mathf.Pow(1 - conductivity / props1.heatCapacity, Celsius.TemperatureInfo.secondsPerUpdate);
            }
            else
            {
                conductivity = Mathf.Sqrt(props1.conductivity * props2.conductivity);
                lerpFactor1 = 1 - Mathf.Pow(1 - conductivity / props1.heatCapacity, Celsius.TemperatureInfo.secondsPerUpdate);
                lerpFactor2 = 1 - Mathf.Pow(1 - conductivity / props2.heatCapacity, Celsius.TemperatureInfo.secondsPerUpdate);
            }

            if (log)
            {
                LogUtility.Log($"- Object 1: t = {temp1:F1}C, capacity = {props1.heatCapacity}, conductivity = {props1.conductivity}");
                LogUtility.Log($"- Object 2: t = {temp2:F1}C, capacity = {props2.heatCapacity}, conductivity = {props2.conductivity}");
                LogUtility.Log($"- Final temperature: {finalTemp:F1}C. Overall conductivity: {conductivity:F1}. Lerp factor 1: {lerpFactor1:P1}. Lerp factor 2: {lerpFactor2:P1}.");
            }
            return (lerpFactor1 * (finalTemp - temp1), lerpFactor2 * (finalTemp - temp2));
        }

        #endregion DIFFUSION

        #region THERMAL PROPERTIES

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

        /// <summary>
        /// Returns heat capacity for a cell
        /// </summary>
        public static float GetHeatCapacity(this IntVec3 cell, Map map) => cell.GetThermalProperties(map).heatCapacity;

        public static float GetHeatConductivity(this IntVec3 cell, Map map) => cell.GetThermalProperties(map).conductivity;

        public static ThingDef GetUnderlyingStuff(this Thing thing) => thing.Stuff ?? thing.def.defaultStuff;

        #endregion THERMAL PROPERTIES

        #region TERRAIN

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

        #endregion TERRAIN

        #region HEAT PUSH

        public static bool TryPushHeat(IntVec3 cell, Map map, float energy)
        {
            if (UI.MouseCell() == cell)
                LogUtility.Log($"Pushing {energy} heat at {cell}.");
            TemperatureInfo temperatureInfo = map.TemperatureInfo();
            if (temperatureInfo == null)
            {
                LogUtility.Log($"TemperatureInfo for {map} unavailable!");
                return false;
            }
            temperatureInfo.SetTempteratureForCell(cell, temperatureInfo.GetTemperatureForCell(cell) + energy * GenTicks.TicksPerRealSecond * Celsius.TemperatureInfo.HeatPushEffect / cell.GetHeatCapacity(map));
            return true;
        }

        #endregion HEAT PUSH
    }
}
