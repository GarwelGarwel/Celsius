using Verse;

namespace TemperaturesPlus
{
    public class ThingThermalProperties : DefModExtension
    {
        public static readonly ThingThermalProperties Empty = new ThingThermalProperties();

        public static readonly ThingThermalProperties Air = new ThingThermalProperties()
        {
            replacesAirProperties = true,
            heatCapacity = 1200,
            conductivity = 0.03f
        };

        public static readonly ThingThermalProperties Meat = new ThingThermalProperties()
        {
            heatCapacity = 100,
            conductivity = 0.5f
        };

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

        public override string ToString() =>
            $"Mass: {mass.ToStringMass()}. Heat capacity: {heatCapacity} J/C. Conductivity: {conductivity} J/s.{(replacesAirProperties ? " Replaces air properties." : "")}";
    }
}
