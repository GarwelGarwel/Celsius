using Verse;

namespace Celsius
{
    public class ThingThermalProperties : DefModExtension
    {
        public float heatCapacity;
        public float volume;
        public float conductivity = 1;
        public float airflow;
        public float airflowWhenOpen;

        public ThingThermalProperties()
        { }

        public override string ToString() => $"Heat capacity: {heatCapacity} J/C. Volume: {volume} m^3. Conductivity: {conductivity} W/C. Airflow: {airflow:P0} ({airflowWhenOpen:P0} when open).";

        public override bool Equals(object obj) =>
            obj is ThingThermalProperties props
            && props.heatCapacity == heatCapacity
            && props.volume == volume
            && props.conductivity == conductivity
            && props.airflow == airflow;

        public override int GetHashCode() => (heatCapacity, volume, conductivity, airflow, airflowWhenOpen).GetHashCode();
    }
}
