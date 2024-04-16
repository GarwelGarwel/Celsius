using UnityEngine;

namespace Celsius
{
    public class ThermalProps
    {
        static ThermalProps air;
        public static ThermalProps Air => air;

        static ThermalProps() => Init();

        public static void Init() => air = new ThermalProps(1, 1, 1);

        public float heatCapacity;
        public float insulation;  // Effective insulation (taking into cosideration airflow)
        public float airflow;

        public float conductivity;
        public float heatflow;
        public float heatflowNoConvection;

        public float HeatFlow => heatflow;

        public float HeatFlowNoConvection => heatflowNoConvection;

        public bool IsAir => this == Air;

        public ThermalProps(float heatCapacity, float insulation, float airflow)
        {
            this.heatCapacity = heatCapacity;
            this.insulation = TemperatureUtility.GetInsulationWithAirflow(insulation, airflow);
            this.airflow = airflow;
            conductivity = Mathf.Pow(Settings.ConductivityPowerBase, insulation);
            heatflow = heatCapacity * conductivity;
            heatflowNoConvection = heatflow / Settings.ConvectionConductivityEffect;
        }

        public override string ToString() => $"Heat capacity: {heatCapacity}. Insulation: {insulation}. Conductivity: {conductivity:P1}. Airflow: {airflow:P0}.";
    }
}
