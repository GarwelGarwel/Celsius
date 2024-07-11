using RimWorld;
using System.Text;
using Verse;

namespace Celsius
{
    public class StatWorker_HeatCapacity : StatWorker
    {
        public override float GetValueUnfinalized(StatRequest req, bool applyPostProcess = true)
        {
            ThermalProps thermalProps = req.HasThing
                ? req.Thing.TryGetComp<CompThermal>()?.ThermalProperties
                : req.Def?.GetModExtension<ThingThermalProperties>()?.GetThermalProps(req.StuffDef?.GetModExtension<StuffThermalProperties>());
            return thermalProps != null ? thermalProps.heatCapacity : 0;
        }

        public override string GetExplanationUnfinalized(StatRequest req, ToStringNumberSense numberSense)
        {
            ThingThermalProperties thingThermalProperties = null;
            StuffThermalProperties stuffThermalProperties = null;
            ThermalProps thermalProps = null;
            string label = null;
            string stuffLabel = null;

            if (req.HasThing)
            {
                CompThermal compThermal = req.Thing.TryGetComp<CompThermal>();
                if (compThermal != null)
                {
                    label = req.Thing.Label;
                    thingThermalProperties = compThermal.ThingThermalProperties;
                    if (thingThermalProperties == null)
                    {
                        LogUtility.Log($"{label} has no ThingThermalProperties!");
                        return $"{label} has no ThingThermalProperties!\n{base.GetExplanationUnfinalized(req, numberSense)}";
                    }
                    stuffLabel = req.Thing.GetStuff().label;
                    stuffThermalProperties = compThermal.StuffThermalProperties;
                    thermalProps = compThermal.ThermalProperties;
                }
            }

            else if (req.Def != null)
            {
                label = req.Def.label;
                thingThermalProperties = req.Def.GetModExtension<ThingThermalProperties>();
                stuffThermalProperties = req.StuffDef?.GetModExtension<StuffThermalProperties>();
                stuffLabel = req.StuffDef?.label;
                thermalProps = thingThermalProperties?.GetThermalProps(stuffThermalProperties);
            }

            if (thermalProps == null || stuffThermalProperties == null)
                return base.GetExplanationUnfinalized(req, numberSense);

            StringBuilder explanation = new StringBuilder("Celsius_Stat_HeatCapacity_StuffVolumetricHeatCapacity"
                .Translate(stuffLabel ?? "stuff", stuffThermalProperties.volumetricHeatCapacity)
                .CapitalizeFirst());
            explanation.AppendInNewLine("Celsius_Stat_HeatCapacity_Volume".Translate(label, thingThermalProperties.volume.ToString("F3")).CapitalizeFirst());
            explanation.AppendInNewLine("Celsius_Stat_HeatCapacity_AirHeatCapacity".Translate((1 - thingThermalProperties.volume).ToStringByStyle(stat.toStringStyle, numberSense)));
            explanation.AppendInNewLine(base.GetExplanationUnfinalized(req, numberSense));

            return explanation.ToString();
        }
    }
}
