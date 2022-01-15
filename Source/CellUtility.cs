using System.Collections.Generic;
using Verse;

namespace TemperaturesPlus
{
    static class CellUtility
    {
        public static IEnumerable<IntVec3> AdjacentCells(this IntVec3 cell)
        {
            yield return cell + IntVec3.North;
            yield return cell + IntVec3.South;
            yield return cell + IntVec3.West;
            yield return cell + IntVec3.East;
        }
    }
}
