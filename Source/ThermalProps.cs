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

        public bool IsAir = false;

        public ThermalProps(float heatCapacity, float insulation, float airflow)
        {
            this.heatCapacity = heatCapacity;
            this.insulation = TemperatureUtility.GetInsulationWithAirflow(insulation, airflow);
            this.airflow = airflow;
            heatflow = heatCapacity * Conductivity;
            heatflowNoConvection = heatflow / Settings.ConvectionConductivityEffect;
            if (heatCapacity == 1 && insulation == 1 && airflow == 1) IsAir = true;
        }

        public override string ToString() => $"Heat capacity: {heatCapacity}. Insulation: {insulation}. Conductivity: {Conductivity:P1}. Airflow: {airflow:P0}.";
    }
}
