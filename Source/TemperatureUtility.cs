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

        public static float GetTemperatureForCell(this IntVec3 cell, Map map)
        {
            TemperatureInfo tempInfo = map.TemperatureInfo();
            if (tempInfo == null)
                return map.mapTemperature.OutdoorTemp;
            return tempInfo.GetTemperatureForCell(cell);
        }

        internal static CellMaterialType GetMaterialType(this IntVec3 cell, Map map)
        {
            if (!cell.InBounds(map))
                return CellMaterialType.Air;
            if (cell.GetFirstMineable(map) != null)
                return CellMaterialType.Rock;
            Building building;
            if ((building = cell.GetFirstBuilding(map)) != null && building.def.holdsRoof)
                return CellMaterialType.Structure;
            return CellMaterialType.Air;
        }

        public static float GetHeatConductivity(this IntVec3 cell, Map map, bool convection = false)
        {
            switch (cell.GetMaterialType(map))
            {
                case CellMaterialType.Air:
                    return convection ? 0.3f : 0.03f;

                case CellMaterialType.Rock:
                    return 2;

                case CellMaterialType.Structure:
                    return 0.1f;

                default:
                    return 1;
            }
        }

        /// <summary>
        /// Returns heat capacity for a cell
        /// </summary>
        public static float GetHeatCapacity(this IntVec3 cell, Map map)
        {
            switch (cell.GetMaterialType(map))
            {
                case CellMaterialType.Rock:
                    return 3000000;

                case CellMaterialType.Structure:
                    return 100000;

                default:
                    return 10000;
            }
        }

        public static float GetTemperature(this Thing thing)
        {
            CompThermal comp = thing.TryGetComp<CompThermal>();
            return comp != null ? comp.temperature : thing.Position.GetTemperatureForCell(thing.Map);
        }

        public static float GetSpecificHeatCapacity(this Thing thing)
        {
            return 1000;
        }
    }
}
