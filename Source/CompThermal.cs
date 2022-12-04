using RimWorld;
using Verse;

namespace Celsius
{
    public class CompThermal : ThingComp
    {
        bool isOpen;

        ThermalProps thermalProps;

        public bool IsOpen
        {
            get => isOpen;
            set
            {
                isOpen = value;
                Reset();
            }
        }

        public ThingThermalProperties ThingThermalProperties => parent.def.GetModExtension<ThingThermalProperties>();

        public StuffThermalProperties StuffThermalProperties =>
            ThingThermalProperties.volume > 0
            ? parent.GetUnderlyingStuff()?.GetModExtension<StuffThermalProperties>() ?? parent.def.GetModExtension<StuffThermalProperties>()
            : null;

        public ThermalProps ThermalProperties => thermalProps ?? (thermalProps = ThingThermalProperties.GetThermalProps(StuffThermalProperties, IsOpen));

        internal static bool ShouldApplyTo(ThingDef thingDef) => thingDef.category == ThingCategory.Building && thingDef.HasModExtension<ThingThermalProperties>();

        internal void Reset() => thermalProps = ThingThermalProperties.GetThermalProps(StuffThermalProperties, IsOpen);

        public override void PostExposeData()
        {
            Scribe_Values.Look(ref isOpen, "isOpen");
            // For compatibility: trying to mark open doors and vents as such when loading Celsius pre-2.0 saves
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
                isOpen |= (parent is Building_Door door && door.Open) || (parent is Building_Vent && parent.GetComp<CompFlickable>()?.SwitchIsOn == true);
            Reset();
        }

        public override void ReceiveCompSignal(string signal)
        {
            // Vents
            if (parent is Building_Vent)
                switch (signal.ToLowerInvariant())
                {
                    case "flickedon":
                        LogUtility.Log($"Vent {parent} was opened.");
                        IsOpen = true;
                        break;

                    case "flickedoff":
                        LogUtility.Log($"Vent {parent} was closed.");
                        IsOpen = false;
                        break;
                }
            // Windows mod
            else if (parent.GetType().FullName == "OpenTheWindows.Building_Window")
                switch (signal.ToLowerInvariant())
                {
                    case "airon":
                    case "bothon":
                        LogUtility.Log($"Window {parent} was opened.");
                        IsOpen = true;
                        break;

                    case "airoff":
                    case "bothoff":
                        LogUtility.Log($"Window {parent} was closed.");
                        IsOpen = false;
                        break;
                }
        }
    }
}
