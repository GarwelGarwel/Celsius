using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace TemperaturesPlus
{
    static class TemperatureUtility
    {
        public static TemperatureInfo TemperatureInfo(this Map map) => map.GetComponent<TemperatureInfo>();

        public static float GetAverageAdjacentTemperatures(this IntVec3 cell, Map map)
        {
            float sum = 0;
            foreach (IntVec3 c in cell.AdjacentCells())
                sum += map.TemperatureInfo().GetTemperatureForCell(c);
            return sum / 4;
        }
    }
}
