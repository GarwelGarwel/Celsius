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

                ThingThermalProperties thingThermalProps = parent.def.GetModExtension<ThingThermalProperties>();
                StuffThermalProperties stuffProps = thingThermalProps.volume > 0
                    ? parent.GetUnderlyingStuff()?.GetModExtension<StuffThermalProperties>() ?? parent.def.GetModExtension<StuffThermalProperties>()
                    : null;
                if (open)
                    thermalPropsOpen = thingThermalProps.GetCellThermalProps(stuffProps, true);
                else thermalProps = thingThermalProps.GetCellThermalProps(stuffProps, false);
                return GetCachedThermalProps(open);
            }
        }

        internal static bool ShouldApplyTo(ThingDef thingDef) => thingDef.category == ThingCategory.Building && thingDef.HasModExtension<ThingThermalProperties>();

        internal void Reset() => thermalProps = thermalPropsOpen = null;
    }
}
