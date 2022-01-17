using RimWorld;
using Verse;

namespace Celsius
{
    public class CompThermal : ThingComp
    {
        ThingThermalProperties thermalProps;

        float Mass => parent.def.EverHaulable ? parent.GetStatValue(StatDefOf.Mass) : parent.def.CostStuffCount;

        public ThingThermalProperties ThermalProperties
        {
            get
            {
                if (parent is Building_Door door && door.Open)
                    return ThingThermalProperties.Empty;

                if (parent is Building_Vent vent && vent.GetComp<CompFlickable>().SwitchIsOn)
                    return ThingThermalProperties.Empty;

                if (thermalProps != null)
                    return thermalProps;

                thermalProps = new ThingThermalProperties(parent.def.GetModExtension<ThingThermalProperties>());
                StuffThermalProperties stuffProps = parent.GetUnderlyingStuff()?.GetModExtension<StuffThermalProperties>() ?? parent.def.GetModExtension<StuffThermalProperties>();
                if (stuffProps != null)
                {
                    if (thermalProps.mass == 0)
                        thermalProps.mass = Mass;
                    if (stuffProps.specificHeatCapacity > 0 && thermalProps.mass > 0)
                        thermalProps.heatCapacity = stuffProps.specificHeatCapacity * thermalProps.mass;
                    thermalProps.conductivity *= stuffProps.conductivity;
                }
                thermalProps.heatCapacity *= parent.stackCount;
                return thermalProps;
            }
        }

        internal static bool ShouldApplyTo(ThingDef thingDef) => thingDef.category == ThingCategory.Building;
    }
}
