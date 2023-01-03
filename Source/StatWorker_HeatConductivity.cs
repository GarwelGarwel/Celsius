using RimWorld;
using System.Text;
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
            StringBuilder explanation = new StringBuilder(base.GetExplanationUnfinalized(req, numberSense));
            CompThermal compThermal = req.Thing?.TryGetComp<CompThermal>();
            if (compThermal == null)
                return explanation.ToString();
            ThingThermalProperties thingThermalProperties = compThermal.ThingThermalProperties;
            if (thingThermalProperties == null)
                return explanation.ToString();
            explanation.AppendInNewLine("Celsius_Stat_HeatCapacity_Insulation".Translate(req.Thing.def.LabelCap, thingThermalProperties.insulation));
            StuffThermalProperties stuffThermalProperties = compThermal.StuffThermalProperties;
            if (stuffThermalProperties != null)
                explanation.AppendInNewLine("Celsius_Stat_HeatCapacity_StuffInsulation".Translate(stuffThermalProperties.insulationFactor.ToStringPercent()));
            if (thingThermalProperties.airflow != thingThermalProperties.airflowWhenOpen && compThermal.IsOpen)
                explanation.AppendInNewLine("Celsius_Stat_HeatCapacity_AirflowOpen".Translate(req.Thing.LabelNoCount).Colorize(Color.yellow));
            ThermalProps thermalProps = compThermal.ThermalProperties;
            if (thermalProps != null)
            {
                if (thermalProps.airflow != 0)
                {
                    explanation.AppendInNewLine("Celsius_Stat_HeatCapacity_Airflow".Translate(thermalProps.airflow.ToStringPercent()));
                    explanation.AppendInNewLine("Celsius_Stat_HeatCapacity_ActualInsulation".Translate(thermalProps.insulation.ToString("F2")));
                }
                explanation.AppendInNewLine("Celsius_Stat_HeatCapacity_Conductivity".Translate(Settings.ConductivityPowerBase, thermalProps.insulation.ToString("F2"), thermalProps.Conductivity.ToStringPercent()));
            }
            return explanation.ToString();
        }
    }
}
