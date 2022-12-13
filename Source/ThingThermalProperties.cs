using UnityEngine;
using Verse;

namespace Celsius
{
    public class ThingThermalProperties : DefModExtension
    {
        public float heatCapacity;
        public float volume;
        public float insulation = 1;
        public float airflow;
        public float airflowWhenOpen;

        ThermalProps defaultProps;

        public ThingThermalProperties()
        { }

        public ThermalProps GetThermalProps()
        {
            if (defaultProps == null)
                defaultProps = new ThermalProps(heatCapacity, insulation, airflow);
            return defaultProps;
        }

        public ThermalProps GetThermalProps(StuffThermalProperties stuffProps, bool open)
        {
            if (stuffProps == null || volume == 0)
            {
                if (heatCapacity <= 0)
                    return null;
                if (!open)
                    return GetThermalProps();
                return new ThermalProps(heatCapacity, insulation, airflowWhenOpen);
            }

            float airflow = open ? airflowWhenOpen : this.airflow;
            return new ThermalProps(Mathf.Lerp(1, stuffProps.volumetricHeatCapacity, volume), insulation * stuffProps.insulationFactor, airflow);
        }

        public void Reset() => defaultProps = null;

        public override string ToString() => $"Heat capacity: {heatCapacity}. Volume: {volume} m^3. Insulation: {insulation}. Conductivity: {GetThermalProps().Conductivity:P1}. Airflow: {airflow:P0} ({airflowWhenOpen:P0} when open).";
    }
}
