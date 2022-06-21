using RimWorld;
using Verse;

namespace Celsius
{
    public class CompThermal : ThingComp
    {
        [Unsaved]
        CellThermalProps thermalProps;

        [Unsaved]
        CellThermalProps thermalPropsOpen;

        bool IsOpen => (parent is Building_Door door && door.Open) || (parent is Building_Vent && parent.GetComp<CompFlickable>()?.SwitchIsOn == true);

        CellThermalProps GetCachedThermalProps(bool open) => open ? thermalPropsOpen : thermalProps;

        public CellThermalProps ThermalProperties
        {
            get
            {
                // Checking if thermal props already cached
                bool open = IsOpen;
                CellThermalProps cachedProps = GetCachedThermalProps(open);
                if (cachedProps != null)
                    return cachedProps;

                // If the parent Thing has no ThingThermalProperties, return empty record
                ThingThermalProperties thingThermalProps = parent.def.GetModExtension<ThingThermalProperties>();
                if (thingThermalProps == null)
                    return CellThermalProps.Empty;

                StuffThermalProperties stuffProps = thingThermalProps.volume > 0
                    ? stuffProps = parent.GetUnderlyingStuff()?.GetModExtension<StuffThermalProperties>() ?? parent.def.GetModExtension<StuffThermalProperties>()
                    : null;
                if (open)
                    thermalPropsOpen = CellThermalProps.Create(thingThermalProps, stuffProps, true);
                else thermalProps = CellThermalProps.Create(thingThermalProps, stuffProps, false);
                return GetCachedThermalProps(open);
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
