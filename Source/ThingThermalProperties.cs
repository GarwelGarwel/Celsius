using Verse;

namespace Celsius
{
    public class ThingThermalProperties : DefModExtension
    {
        public static readonly ThingThermalProperties Empty = new ThingThermalProperties() { ignoreStuff = true };

        public static readonly ThingThermalProperties Air = new ThingThermalProperties()
        {
            heatCapacity = Settings.AirHeatCapacity,
            conductivity = 0.17f
        };

        public float heatCapacity;
        public float conductivity = 1;
        public float volume;
        public bool ignoreStuff;

        public ThingThermalProperties()
        { }

        public ThingThermalProperties(ThingThermalProperties copyFrom)
        {
            if (copyFrom == null)
                return;
            heatCapacity = copyFrom.heatCapacity;
            conductivity = copyFrom.conductivity;
            volume = copyFrom.volume;
            ignoreStuff = copyFrom.ignoreStuff;
        }

        public override string ToString() => $"Heat capacity: {heatCapacity} J/C. Conductivity: {conductivity} W/C. Volume: {volume} m^3.";

        public override bool Equals(object obj) =>
            obj is ThingThermalProperties props
            && props.heatCapacity == heatCapacity
            && props.conductivity == conductivity
            && props.volume == volume
            && props.ignoreStuff == ignoreStuff;

        public override int GetHashCode() => (heatCapacity, conductivity, volume).GetHashCode();
    }
}
