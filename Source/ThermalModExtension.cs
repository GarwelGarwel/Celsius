using Verse;

namespace TemperaturesPlus
{
    public enum ThingCellInteraction
    {
        //None = 0,
        Separate,
        Integrated
    }

    public class ThermalModExtension : DefModExtension
    {
        public ThingCellInteraction cellInteraction = ThingCellInteraction.Separate;
        public bool replacesAirProperties;
        public float specificHeatCapacity;
        public float heatCapacity;
        public float conductivity = 1;
    }
}
