using RimWorld;
using System.Linq;
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
        public static TemperatureInfo TemperatureInfo(this Map map) => map.GetComponent<TemperatureInfo>();

        public static ThingThermalProperties AirProperties = new ThingThermalProperties()
        {
            replacesAirProperties = true,
            heatCapacity = 1200,
            conductivity = 0.03f
        };

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
            return thermalProps ?? AirProperties;
        }

        public static ThingThermalProperties GetThermalProperties(Thing thing) => thing.TryGetComp<CompThermal>()?.ThermalProperties ?? new ThingThermalProperties();

        internal static bool IsAir(this IntVec3 cell, Map map) => cell.GetThermalProperties(map) == AirProperties;

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

        public static ThingDef GetUnderlyingStuff(this Thing thing) =>
            thing.def.IsStuff ? thing.def : thing.Stuff ?? thing.def.defaultStuff;
    }
}
