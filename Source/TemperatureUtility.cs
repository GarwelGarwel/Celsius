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

        public static TemperatureInfo TemperatureInfo(this Map map) => map.GetComponent<TemperatureInfo>();

        #region TEMPERATURE

        public static float GetTemperatureForCell(this IntVec3 cell, Map map)
        {
            TemperatureInfo tempInfo = map.TemperatureInfo();
            if (tempInfo == null)
                return map.mapTemperature.OutdoorTemp;
            return tempInfo.GetTemperatureForCell(cell);
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

        static float airConductivity;

        static float airLerpFactor;

        internal static void RecalculateAirProperties()
        {
            ThingThermalProperties.Air.heatCapacity = Settings.AirHeatCapacity;
            airConductivity = ThingThermalProperties.Air.conductivity * Settings.HeatConductivityFactor * Settings.ConvectionConductivityEffect;
            airLerpFactor = Mathf.Min(1 - Mathf.Pow(1 - airConductivity / ThingThermalProperties.Air.heatCapacity, Celsius.TemperatureInfo.SecondsPerUpdate), 0.25f);
            LogUtility.Log($"Air conductivity: {airConductivity:F2}. Air lerp factor: {airLerpFactor:P1}.");
        }

        internal static float DiffusionTemperatureChangeSingle(float oldTemp, float neighbourTemp, ThingThermalProperties thermalProps, bool log = false)
        {
            if (Mathf.Abs(oldTemp - neighbourTemp) < TemperatureChangePrecision)
                return 0;
            float finalTemp = (oldTemp + neighbourTemp) / 2;
            float conductivity, lerpFactor;
            if (thermalProps == ThingThermalProperties.Air)
            {
                conductivity = airConductivity;
                lerpFactor = airLerpFactor;
            }
            else
            {
                conductivity = thermalProps.conductivity * Settings.HeatConductivityFactor;
                lerpFactor = Mathf.Min(1 - Mathf.Pow(1 - conductivity / thermalProps.heatCapacity, Celsius.TemperatureInfo.SecondsPerUpdate), 0.25f);
            }

            if (log)
            {
                LogUtility.Log($"Old temperature: {oldTemp:F1}C. Neighbour temperature: {neighbourTemp:F1}C. Heat capacity: {thermalProps.heatCapacity}. Conductivity: {conductivity}.");
                LogUtility.Log($"Final temperature: {finalTemp:F1}C. Lerp factor: {lerpFactor:P1}.");
            }

            return lerpFactor * (finalTemp - oldTemp);
        }

        internal static (float, float) DiffusionTemperatureChangeMutual(float temp1, ThingThermalProperties props1, float temp2, ThingThermalProperties props2, bool log = false)
        {
            if (Mathf.Abs(temp1 - temp2) < TemperatureChangePrecision)
                return (0, 0);
            float finalTemp, conductivity, lerpFactor1, lerpFactor2;

            if (props1 == ThingThermalProperties.Air && props2 == ThingThermalProperties.Air)
            {
                finalTemp = (temp1 + temp2) / 2;
                conductivity = airConductivity;
                lerpFactor1 = lerpFactor2 = airLerpFactor;
            }

            else if (props1 == props2)
            {
                LogUtility.Log($"Both objects have the same thermal props: {props1}");
                finalTemp = (temp1 + temp2) / 2;
                conductivity = props1.conductivity * Settings.HeatConductivityFactor;
                lerpFactor1 = Mathf.Min(lerpFactor2 = 1 - Mathf.Pow(1 - conductivity / props1.heatCapacity, Celsius.TemperatureInfo.SecondsPerUpdate), 0.25f);
            }

            else
            {
                finalTemp = GenMath.WeightedAverage(temp1, props1.heatCapacity, temp2, props2.heatCapacity);
                conductivity = Mathf.Sqrt(props1.conductivity * props2.conductivity) * Settings.HeatConductivityFactor;
                lerpFactor1 = Mathf.Min(1 - Mathf.Pow(1 - conductivity / props1.heatCapacity, Celsius.TemperatureInfo.SecondsPerUpdate), 0.25f);
                lerpFactor2 = Mathf.Min(1 - Mathf.Pow(1 - conductivity / props2.heatCapacity, Celsius.TemperatureInfo.SecondsPerUpdate), 0.25f);
            }

            if (log)
            {
                LogUtility.Log($"Object 1: t = {temp1:F1}C, capacity = {props1.heatCapacity}, conductivity = {props1.conductivity}");
                LogUtility.Log($"Object 2: t = {temp2:F1}C, capacity = {props2.heatCapacity}, conductivity = {props2.conductivity}");
                LogUtility.Log($"Final temperature: {finalTemp:F1}C. Overall conductivity: {conductivity:F1}. Lerp factor 1: {lerpFactor1:P1}. Lerp factor 2: {lerpFactor2:P1}.");
            }

            return (lerpFactor1 * (finalTemp - temp1), lerpFactor2 * (finalTemp - temp2));
        }

        #endregion DIFFUSION

        #region THERMAL PROPERTIES

        public static ThingThermalProperties GetThermalProperties(this IntVec3 cell, Map map)
        {
            if (cell.InBounds(map))
                foreach (Thing thing in cell.GetThingList(map))
                {
                    ThingThermalProperties thermalProps = thing.TryGetComp<CompThermal>()?.ThermalProperties;
                    if (thermalProps != null && thermalProps.heatCapacity > 0)
                        return thermalProps;
                }
            return ThingThermalProperties.Air;
        }

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
            if (UI.MouseCell() == cell || energy < 0)
                LogUtility.Log($"Pushing {energy} heat at {cell}.");
            TemperatureInfo temperatureInfo = map.TemperatureInfo();
            if (temperatureInfo == null)
            {
                LogUtility.Log($"TemperatureInfo for {map} unavailable!");
                return false;
            }
            temperatureInfo.SetTempteratureForCell(cell, temperatureInfo.GetTemperatureForCell(cell) + energy * GenTicks.TicksPerRealSecond * Settings.HeatPushEffect / cell.GetHeatCapacity(map));
            return true;
        }

        #endregion HEAT PUSH
    }
}
