using RimWorld;
using Verse;

namespace Celsius
{
    public class CompThermal : ThingComp
    {
        [Unsaved]
        ThingThermalProperties thermalProps;

        [Unsaved]
        ThingThermalProperties thermalPropsOpen;

        bool IsOpen => (parent is Building_Door door && door.Open) || (parent is Building_Vent && parent.GetComp<CompFlickable>()?.SwitchIsOn == true);

        ThingThermalProperties GetThingThermalProperties(bool open) => open ? thermalPropsOpen : thermalProps;

        public ThingThermalProperties ThermalProperties
        {
            get
            {
                // Checking if thermal props already cached
                bool open = IsOpen;
                ThingThermalProperties cachedProps = GetThingThermalProperties(open);
                if (cachedProps != null)
                    return cachedProps;

                // If the parent Thing has no ThingThermalProperties, return empty record
                thermalProps = parent.def.GetModExtension<ThingThermalProperties>();
                if (thermalProps == null)
                    return ThingThermalProperties.Empty;
                
                if (open)
                    thermalPropsOpen = new ThingThermalProperties(thermalProps, true);
                if (thermalProps.volume <= 0)
                    return GetThingThermalProperties(open);

                // Applying stuff properties
                StuffThermalProperties stuffProps = parent.GetUnderlyingStuff()?.GetModExtension<StuffThermalProperties>() ?? parent.def.GetModExtension<StuffThermalProperties>();
                if (stuffProps != null)
                {
                    thermalProps = new ThingThermalProperties(thermalProps);
                    thermalProps.heatCapacity = stuffProps.volumetricHeatCapacity * thermalProps.volume + ThingThermalProperties.Air.heatCapacity * (1 - thermalProps.volume / 1000);
                    thermalProps.conductivity *= stuffProps.conductivity;
                    if (open)
                    {
                        thermalPropsOpen.heatCapacity = thermalProps.heatCapacity;
                        thermalPropsOpen.conductivity *= stuffProps.conductivity;
                    }
                }
                return GetThingThermalProperties(open);
            }
        }

        internal static bool ShouldApplyTo(ThingDef thingDef) => thingDef.category == ThingCategory.Building && thingDef.HasModExtension<ThingThermalProperties>();

        internal void Reset()
        {
            thermalProps = null;
            thermalPropsOpen = null;
        }
    }
}
