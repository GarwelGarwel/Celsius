using Verse;

namespace TemperaturesPlus
{
    public class ThingThermalProperties : DefModExtension
    {
        public static readonly ThingThermalProperties Empty = new ThingThermalProperties();

        public bool replacesAirProperties;
        public float mass;
        public float heatCapacity;
        public float conductivity = 1;

        public ThingThermalProperties()
        { }

        public ThingThermalProperties(ThingThermalProperties copyFrom)
        {
            if (copyFrom == null)
                return;
            replacesAirProperties = copyFrom.replacesAirProperties;
            mass = copyFrom.mass;
            heatCapacity = copyFrom.heatCapacity;
            conductivity = copyFrom.conductivity;
        }

        public override string ToString() => $"Mass: {mass}. Heat capacity: {heatCapacity}. Conductivity: {conductivity}.{(replacesAirProperties ? " Replaces air properties." : "")}";
    }
}
