using Verse;

using static Celsius.LogUtility;

namespace Celsius
{
    public enum MountainTemperatureMode
    {
        Vanilla = 0,
        AnnualAverage,
        SeasonAverage,
        AmbientAir,
        Manual
    }

    public class Settings : ModSettings
    {
        public static bool UseVanillaTemperatureColors;
        public static bool ShowTemperatureTooltip;
        public static bool FreezingAndMeltingEnabled;
        public static bool AutoignitionEnabled;
        public static float ConductivityPowerBase;
        public static float ConvectionConductivityEffect;
        public static float EnvironmentDiffusionFactor;
        public static float HeatPushMultiplier;
        public static float HeatPushEffect;
        public static MountainTemperatureMode MountainTemperatureMode;
        public static float MountainTemperature = TemperatureTuning.DeepUndergroundTemperature;
        public static bool DebugMode = Prefs.LogVerbose;

        public const float ConductivityPowerBase_Default = 0.5f;
        public const float ConvectionConductivityEffect_Default = 10;
        public const float EnvironmentDiffusionFactor_Default = 0.3f;
        public const float HeatPushEffect_Base = 0.15f;

        public Settings() => Reset();

        public override void ExposeData()
        {
            Scribe_Values.Look(ref UseVanillaTemperatureColors, "UseVanillaTemperatureColors");
            Scribe_Values.Look(ref ShowTemperatureTooltip, "ShowTemperatureTooltip", true);
            Scribe_Values.Look(ref FreezingAndMeltingEnabled, "FreezingAndMeltingEnabled", true);
            Scribe_Values.Look(ref AutoignitionEnabled, "AutoignitionEnabled", true);
            Scribe_Values.Look(ref ConductivityPowerBase, "ConductivityPowerBase", ConductivityPowerBase_Default);
            Scribe_Values.Look(ref ConvectionConductivityEffect, "ConvectionConductivityEffect", ConvectionConductivityEffect_Default);
            Scribe_Values.Look(ref EnvironmentDiffusionFactor, "EnvironmentDiffusionFactor", EnvironmentDiffusionFactor_Default);
            Scribe_Values.Look(ref HeatPushMultiplier, "HeatPushMultiplier", 1);
            Scribe_Values.Look(ref MountainTemperatureMode, "MountainTemperatureMode", MountainTemperatureMode.Vanilla);
            Scribe_Values.Look(ref MountainTemperature, "MountainTemperature", TemperatureTuning.DeepUndergroundTemperature);
            Scribe_Values.Look(ref DebugMode, "DebugMode", forceSave: true);
        }

        public static void Reset()
        {
            UseVanillaTemperatureColors = false;
            ShowTemperatureTooltip = true;
            FreezingAndMeltingEnabled = true;
            AutoignitionEnabled = true;
            ConductivityPowerBase = ConductivityPowerBase_Default;
            ConvectionConductivityEffect = ConvectionConductivityEffect_Default;
            EnvironmentDiffusionFactor = EnvironmentDiffusionFactor_Default;
            HeatPushMultiplier = 1;
            HeatPushEffect = HeatPushEffect_Base;
            MountainTemperatureMode = MountainTemperatureMode.Vanilla;
            MountainTemperature = TemperatureTuning.DeepUndergroundTemperature;
            Print();
            TemperatureUtility.SettingsChanged();
        }

        public static void Print()
        {
            if (!DebugMode)
                return;
            Log($"UseVanillaTemperatureColors: {UseVanillaTemperatureColors}");
            Log($"ShowTemperatureTooltip: {ShowTemperatureTooltip}");
            Log($"FreezingAndMeltingEnabled: {FreezingAndMeltingEnabled}");
            Log($"AutoignitionEnabled: {AutoignitionEnabled}");
            Log($"ConductivityPowerBase: {ConductivityPowerBase}");
            Log($"ConvectionConductivityEffect: {ConvectionConductivityEffect}");
            Log($"EnvironmentDiffusionFactor: {EnvironmentDiffusionFactor}");
            Log($"HeatPushMultiplier: {HeatPushMultiplier.ToStringPercent()}");
            Log($"HeatPushEffect: {HeatPushEffect}");
            Log($"MountainTemperatureMode: {MountainTemperatureMode}");
            Log($"MountainTemperature: {MountainTemperature.ToStringTemperature()}");
        }
    }
}
