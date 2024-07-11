using RimWorld;
using UnityEngine;
using Verse;

namespace Celsius
{
    public static class MiscExtensions
    {
        public static float RoundWithPrecision(this float value, float precision = 1) => Mathf.Round(value / precision) * precision;

        public static ThingDef GetStuff(this Thing thing) => thing.def.IsStuff ? thing.def : thing.Stuff ?? GenStuff.DefaultStuffFor(thing.def) ?? thing.def;

        public static float GetAverageSnowDepth(this Map map)
        {
            float snowDepth = map.snowGrid.TotalDepth;
            if (snowDepth <= 0)
                return 0;
            int possibleSnowCells = 0;
            for (int i = 0; i < map.Size.x * map.Size.z; i++)
            {
                TerrainDef terrain = map.terrainGrid.TerrainAt(i);
                if (terrain != null && !terrain.holdSnow)
                    continue;
                Building building = map.edificeGrid[i];
                if (building != null && !SnowGrid.CanCoexistWithSnow(building.def))
                    continue;
                if (map.roofGrid.Roofed(i))
                    continue;
                possibleSnowCells++;
            }
            if (possibleSnowCells == 0)
                return 0;
            snowDepth /= possibleSnowCells;
            LogUtility.Log($"Covering frozen terrain with average snow level of {snowDepth:F4}.");
            return snowDepth;
        }
    }
}
