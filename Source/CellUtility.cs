using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace TemperaturesPlus
{
    static class CellUtility
    {
        public static IntVec3[] AdjacentCells(this IntVec3 cell) =>
            new IntVec3[4] { cell + IntVec3.North, cell + IntVec3.South, cell + IntVec3.West, cell + IntVec3.East };
    }
}
