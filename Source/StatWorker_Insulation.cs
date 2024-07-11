using RimWorld;
using System.Text;
using UnityEngine;
using Verse;

namespace Celsius
{
    public class StatWorker_Insulation : StatWorker
    {
        public override float GetValueUnfinalized(StatRequest req, bool applyPostProcess = true)
        {
            ThermalProps thermalProps = req.HasThing
                ? req.Thing.TryGetComp<CompThermal>()?.ThermalProperties
                : req.Def?.GetModExtension<ThingThermalProperties>()?.GetThermalProps(req.StuffDef?.GetModExtension<StuffThermalProperties>());
            return thermalProps != null ? thermalProps.insulation : 0;
        }

        public override string GetExplanationUnfinalized(StatRequest req, ToStringNumberSense numberSense)
        {
            ThingThermalProperties thingThermalProperties = null;
            StuffThermalProperties stuffThermalProperties = null;
            ThermalProps thermalProps = null;
            bool isOpen = false;
            string label = null;
            string stuffLabel = null;

            if (req.HasThing)
            {
                CompThermal compThermal = req.Thing.TryGetComp<CompThermal>();
                if (compThermal != null)
                {
                    label = req.Thing.Label;
                    thingThermalProperties = compThermal.ThingThermalProperties;
                    stuffLabel = req.Thing.GetStuff().label;
                    stuffThermalProperties = compThermal.StuffThermalProperties;
                    thermalProps = compThermal.ThermalProperties;
                    isOpen = compThermal.IsOpen;
                }
            }

            else if (req.Def != null)
            {
                label = req.Def.label;
                thingThermalProperties = req.Def.GetModExtension<ThingThermalProperties>();
                stuffLabel = req.StuffDef?.label;
                stuffThermalProperties = req.StuffDef?.GetModExtension<StuffThermalProperties>();
                thermalProps = thingThermalProperties?.GetThermalProps(stuffThermalProperties);
            }

            if (thermalProps == null)
                return base.GetExplanationUnfinalized(req, numberSense);

            StringBuilder explanation = new StringBuilder("Celsius_Stat_Insulation_BaseInsulation"
                .Translate(label, thingThermalProperties.insulation.ToString("F1"))
                .CapitalizeFirst());
            if (stuffThermalProperties != null)
            {
                explanation.AppendInNewLine("Celsius_Stat_Insulation_StuffInsulation".Translate(stuffLabel ?? "stuff").CapitalizeFirst());
                explanation.Append($"x{stuffThermalProperties.insulationFactor.ToStringPercent()}");
            }
            if (isOpen && thingThermalProperties.airflow != thingThermalProperties.airflowWhenOpen)
                explanation.AppendInNewLine("Celsius_Stat_Insulation_AirflowOpen".Translate(label).Colorize(Color.yellow).CapitalizeFirst());
            if (thermalProps.airflow != 0)
                explanation.AppendInNewLine("Celsius_Stat_Insulation_Airflow".Translate(thermalProps.airflow.ToStringPercent()));

            return explanation.ToString();
        }
    }
}
