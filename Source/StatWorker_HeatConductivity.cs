using RimWorld;
using UnityEngine;
using Verse;

namespace Celsius
{
    public class StatWorker_HeatConductivity : StatWorker
    {
        public override float GetValueUnfinalized(StatRequest req, bool applyPostProcess = true)
        {
            ThermalProps thermalProps = req.Thing?.TryGetComp<CompThermal>()?.ThermalProperties;
            return thermalProps == null ? 0 : thermalProps.Conductivity;
        }

        public override string GetExplanationUnfinalized(StatRequest req, ToStringNumberSense numberSense)
        {
            string str = base.GetExplanationUnfinalized(req, numberSense);
            CompThermal compThermal = req.Thing?.TryGetComp<CompThermal>();
            if (compThermal == null)
                return str;
            ThingThermalProperties thingThermalProperties = compThermal.ThingThermalProperties;
            if (thingThermalProperties == null)
                return str;
            // Localization Key: Celsius_Stat_HeatCapacity_insulation - {req.Thing.def.LabelCap} insulation: {thingThermalProperties.insulation}
            str += $"\n{"Celsius_Stat_HeatCapacity_insulation".Translate(req.Thing.def.LabelCap, thingThermalProperties.insulation)}";
            StuffThermalProperties stuffThermalProperties = compThermal.StuffThermalProperties;
            if (stuffThermalProperties != null)
                // Localization Key: Celsius_Stat_HeatCapacity_Stuffinsulation - Stuff insulation: x{stuffThermalProperties.insulationFactor.ToStringPercent()}
                str += $"\n{"Celsius_Stat_HeatCapacity_Stuffinsulation".Translate()} x{stuffThermalProperties.insulationFactor.ToStringPercent()}";
            if (thingThermalProperties.airflow != thingThermalProperties.airflowWhenOpen && compThermal.IsOpen)
                // Localization Key: Celsius_Stat_HeatCapacity_Airflowopen - The {req.Thing.LabelNoCount} is open.
                str += $"\n{"Celsius_Stat_HeatCapacity_Airflowopen".Translate(req.Thing.LabelNoCount)}".Colorize(Color.yellow);
            ThermalProps thermalProps = compThermal.ThermalProperties;
            if (thermalProps != null)
            {
                if (thermalProps.airflow != 0)
                {
                    // Localization Key: Celsius_Stat_HeatCapacity_Airflow - Airflow: {thermalProps.airflow.ToStringPercent()}
                    str += $"\n{"Celsius_Stat_HeatCapacity_Airflow".Translate(thermalProps.airflow.ToStringPercent())}";
                    // Localization Key: Celsius_Stat_HeatCapacity_ActualInsulation - Actual insulation: {thermalProps.insulation}
                    str += $"\n{"Celsius_Stat_HeatCapacity_ActualInsulation".Translate(thermalProps.insulation)}";
                }
                // Localization Key: Celsius_Stat_HeatCapacity_Conductivity - Conductivity:
                str += $"\n{"Celsius_Stat_HeatCapacity_Conductivity".Translate()} {Settings.ConductivityPowerBase} ^ {thermalProps.insulation} = {thermalProps.Conductivity.ToStringPercent()}";
            }
            return str;
        }
    }
}
