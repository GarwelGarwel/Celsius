using Verse;

namespace Celsius
{
    public class ThingThermalProperties : DefModExtension
    {
        public static readonly ThingThermalProperties Empty = new ThingThermalProperties();

        public static readonly ThingThermalProperties Air = new ThingThermalProperties()
        {
            heatCapacity = Settings.AirHeatCapacity,
            conductivity = 0.03f
        };

        public static readonly ThingThermalProperties Meat = new ThingThermalProperties()
        {
            heatCapacity = 100,
            conductivity = 0.5f
        };

        public float mass;
        public float heatCapacity;
        public float conductivity = 1;

        public ThingThermalProperties()
        { }

        public ThingThermalProperties(ThingThermalProperties copyFrom)
        {
            if (copyFrom == null)
                return;
            mass = copyFrom.mass;
            heatCapacity = copyFrom.heatCapacity;
            conductivity = copyFrom.conductivity;
        }

        public override string ToString() => $"Mass: {mass.ToStringMass()}. Heat capacity: {heatCapacity} J/C. Conductivity: {conductivity} W/C.";
    }
}
