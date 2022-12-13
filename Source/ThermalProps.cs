using UnityEngine;

namespace Celsius
{
    public class ThermalProps
    {
        public static readonly ThermalProps Air = new ThermalProps(1, 1, 1);

        public float heatCapacity;
        public float insulation = 1;  // Effective insulation (taking into cosideration airflow)
        public float airflow;

        public float heatflow;
        public float heatflowNoConvection;

        public float Conductivity => Mathf.Pow(Settings.ConductivityPowerBase, insulation);

        public float HeatFlow => heatflow;

        public float HeatFlowNoConvection => heatflowNoConvection;

        public bool IsAir => this == Air;

        public ThermalProps(float heatCapacity, float insulation, float airflow)
        {
            this.heatCapacity = heatCapacity;
            this.insulation = TemperatureUtility.GetInsulationWithAirflow(insulation, airflow);
            this.airflow = airflow;
            heatflow = heatCapacity * Conductivity;
            heatflowNoConvection = heatflow / Settings.ConvectionConductivityEffect;
        }

        public override string ToString() => $"Heat capacity: {heatCapacity}. Insulation: {insulation}. Conductivity: {Conductivity:P1}. Airflow: {airflow:P0}.";

        public bool Equals(ThermalProps props) => props.heatCapacity == heatCapacity && props.insulation == insulation && props.airflow == airflow;

        public override int GetHashCode() => (heatCapacity, insulation, airflow).GetHashCode();
    }
}
