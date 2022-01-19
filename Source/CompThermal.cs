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
                    float mass = parent.GetStatValue(StatDefOf.Mass);
                    if (stuffProps.specificHeatCapacity > 0 && mass > 0)
                        thermalProps.heatCapacity = stuffProps.specificHeatCapacity * mass;
                    thermalProps.conductivity *= stuffProps.conductivity;
                }
                else thermalProps = parent.def.GetModExtension<ThingThermalProperties>() ?? ThingThermalProperties.Empty;
                return thermalProps;
            }
        }

        internal static bool ShouldApplyTo(ThingDef thingDef) => thingDef.category == ThingCategory.Building && thingDef.HasModExtension<ThingThermalProperties>();
    }
}
