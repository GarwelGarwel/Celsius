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

            if (req.HasThing)
            {
                CompThermal compThermal = req.Thing.TryGetComp<CompThermal>();
                if (compThermal != null)
                {
                    thingThermalProperties = compThermal.ThingThermalProperties;
                    stuffThermalProperties = compThermal.StuffThermalProperties;
                    thermalProps = compThermal.ThermalProperties;
                }
            }

            else if (req.Def != null)
            {
                thingThermalProperties = req.Def.GetModExtension<ThingThermalProperties>();
                stuffThermalProperties = req.StuffDef?.GetModExtension<StuffThermalProperties>();
                thermalProps = thingThermalProperties?.GetThermalProps(stuffThermalProperties);
            }

            if (thermalProps == null || stuffThermalProperties == null)
                return base.GetExplanationUnfinalized(req, numberSense);

            StringBuilder explanation = new StringBuilder("Celsius_Stat_HeatCapacity_StuffVolumetricHeatCapacity"
                .Translate(req.StuffDef.label, stuffThermalProperties.volumetricHeatCapacity)
                .CapitalizeFirst());
            explanation.AppendInNewLine("Celsius_Stat_HeatCapacity_Volume".Translate(req.Def.label, thingThermalProperties.volume.ToString("F3")).CapitalizeFirst());
            explanation.AppendInNewLine("Celsius_Stat_HeatCapacity_AirHeatCapacity".Translate((1 - thingThermalProperties.volume).ToStringByStyle(stat.toStringStyle, numberSense)));
            explanation.AppendInNewLine(base.GetExplanationUnfinalized(req, numberSense));

            return explanation.ToString();
        }
    }
}
