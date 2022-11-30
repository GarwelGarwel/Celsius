using Verse;

namespace Celsius
{
    public class ThingThermalProperties : DefModExtension
    {
        public float heatCapacity;
        public float volume;
        public float isolation = 1;
        public float airflow;
        public float airflowWhenOpen;

        ThermalProps defaultProps;

        public ThingThermalProperties()
        { }

        public ThermalProps GetThermalProps()
        {
            if (defaultProps == null)
                defaultProps = new ThermalProps()
                {
                    heatCapacity = heatCapacity,
                    isolation = isolation,
                    airflow = airflow
                };
            return defaultProps;
        }

        public ThermalProps GetThermalProps(StuffThermalProperties stuffProps, bool open)
        {
            if (stuffProps == null)
            {
                if (heatCapacity <= 0)
                    return null;
                if (!open)
                    return GetThermalProps();
                return new ThermalProps()
                {
                    heatCapacity = heatCapacity,
                    isolation = GenMath.WeightedAverage(1, airflowWhenOpen, isolation, 1 - airflowWhenOpen),
                    airflow = airflowWhenOpen
                };
            }

            float airflow = open ? airflowWhenOpen : this.airflow;
            return new ThermalProps()
            {
                heatCapacity = (stuffProps.volumetricHeatCapacity - 1) * volume + 1,
                isolation = GenMath.WeightedAverage(1, airflow, isolation * stuffProps.isolation, 1 - airflow),
                airflow = airflow
            };
        }

        public void Reset() => defaultProps = null;

        public override string ToString() => $"Heat capacity: {heatCapacity}. Volume: {volume} m^3. Isolation: {isolation}. Conductivity: {GetThermalProps().Conductivity:P1}. Airflow: {airflow:P0} ({airflowWhenOpen:P0} when open).";
    }
}
