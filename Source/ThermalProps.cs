using UnityEngine;

namespace Celsius
{
    public class ThermalProps
    {
        public static readonly ThermalProps Air = new ThermalProps(1, 1, 1);

        public float heatCapacity;
        public float isolation = 1;  // Effective isolation (taking into cosideration airflow)
        public float airflow;

        public float heatflow;
        public float heatflowNoConvection;

        public float Conductivity => Mathf.Pow(Settings.ConductivityPowerBase, isolation);

        public float HeatFlow => heatflow;

        public float HeatFlowNoConvection => heatflowNoConvection;

        public bool IsAir => airflow == 1;

        public ThermalProps(float heatCapacity, float isolation, float airflow)
        {
            this.heatCapacity = heatCapacity;
            this.isolation = TemperatureUtility.GetIsolationWithAirflow(isolation, airflow);
            this.airflow = airflow;
            heatflow = heatCapacity * Conductivity;
            heatflowNoConvection = heatflow / Settings.ConvectionConductivityEffect;
        }

        public override string ToString() => $"Heat capacity: {heatCapacity}. Isolation: {isolation}. Conductivity: {Conductivity:P1}. Airflow: {airflow:P0}.";

        public bool Equals(ThermalProps props) => props.heatCapacity == heatCapacity && props.isolation == isolation && props.airflow == airflow;

        public override int GetHashCode() => (heatCapacity, isolation, airflow).GetHashCode();
    }
}
