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
        static float GetTemperatureForCell(IntVec3 cell, Map map)
        {
            return GenTemperature.GetTemperatureForCell(cell, map);
        }
    }
}
