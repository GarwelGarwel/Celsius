using RimWorld;
using Verse;

namespace Celsius
{
    public class CompThermal : ThingComp
    {
        [Unsaved]
        ThingThermalProperties thermalProps;

        public ThingThermalProperties ThermalProperties
        {
            get
            {
                if (parent is Building_Door door && door.Open)
                    return ThingThermalProperties.Empty;

                if (parent is Building_Vent)
                {
                    CompFlickable flickable = parent.GetComp<CompFlickable>();
                    if (flickable == null || flickable.SwitchIsOn)
                        return ThingThermalProperties.Empty;
                }

                if (thermalProps != null)
                    return thermalProps;

                StuffThermalProperties stuffProps = parent.GetUnderlyingStuff()?.GetModExtension<StuffThermalProperties>() ?? parent.def.GetModExtension<StuffThermalProperties>();
                if (stuffProps != null)
                {
                    thermalProps = new ThingThermalProperties(parent.def.GetModExtension<ThingThermalProperties>());
                    float hc = stuffProps.volumetricHeatCapacity * thermalProps.volume;
                    if (hc > 0)
                        thermalProps.heatCapacity = hc + ThingThermalProperties.Air.heatCapacity * (1 - thermalProps.volume / 1000);
                    thermalProps.conductivity *= stuffProps.conductivity;
                }
                else thermalProps = parent.def.GetModExtension<ThingThermalProperties>() ?? ThingThermalProperties.Empty;
                return thermalProps;
            }
        }

        internal static bool ShouldApplyTo(ThingDef thingDef) => thingDef.category == ThingCategory.Building && thingDef.HasModExtension<ThingThermalProperties>();

        internal void Reset() => thermalProps = null;
    }
}
