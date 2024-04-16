using UnityEngine;
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
        public static bool PawnWeatherEffects;
        public static int TicksPerUpdate;
        public static int TicksPerSlice;
        public static float SnowMeltCoefficient;
        public static float SnowMeltCoefficientRain;
        public static float ConductivityPowerBase;
        public static float ConvectionConductivityEffect;
        public static float EnvironmentDiffusionFactor;
        public static float RoofInsulation;
        public static float RoofDiffusionFactor;
        public static float HeatPushMultiplier;
        public static float HeatPushEffect;
        public static MountainTemperatureMode MountainTemperatureMode;
        public static float MountainTemperature = TemperatureTuning.DeepUndergroundTemperature;
        public static float MountainTemperatureOffset;
        public static int TemperatureDisplayDigits;
        public static string TemperatureDisplayFormatString;
        public static bool DebugMode = Prefs.LogVerbose;

        public const int TicksPerUpdate_Default = 120;
        public const int SliceCount = 4;
        public const int TicksPerSlice_Default = TicksPerUpdate_Default / SliceCount;
        public const float SnowMeltCoefficient_Default = TicksPerUpdate_Default * 0.0006f;
        public const float SnowMeltCoefficientRain_Default = SnowMeltCoefficient_Default * 2;

        public const float ConductivityPowerBase_Default = 0.5f;
        public const float ConvectionConductivityEffect_Default = 20;
        public const float EnvironmentDiffusionFactor_Default = 0.3f;
        public const float RoofInsulation_Default = 10;
        public const float HeatPushEffect_Base = 0.15f;
        public const int TemperatureDisplayDigits_Default = 0;

        public Settings() => Reset();

        public override void ExposeData()
        {
            Scribe_Values.Look(ref UseVanillaTemperatureColors, "UseVanillaTemperatureColors");
            Scribe_Values.Look(ref ShowTemperatureTooltip, "ShowTemperatureTooltip", true);
            Scribe_Values.Look(ref FreezingAndMeltingEnabled, "FreezingAndMeltingEnabled", true);
            Scribe_Values.Look(ref AutoignitionEnabled, "AutoignitionEnabled", true);
            Scribe_Values.Look(ref PawnWeatherEffects, "PawnWeatherEffects", true);
            Scribe_Values.Look(ref TicksPerUpdate, "TicksPerUpdate", TicksPerUpdate_Default);
            Scribe_Values.Look(ref ConvectionConductivityEffect, "ConvectionConductivityEffect", ConvectionConductivityEffect_Default);
            Scribe_Values.Look(ref EnvironmentDiffusionFactor, "EnvironmentDiffusionFactor", EnvironmentDiffusionFactor_Default);
            Scribe_Values.Look(ref RoofInsulation, "RoofInsulation", RoofInsulation_Default);
            Scribe_Values.Look(ref HeatPushMultiplier, "HeatPushMultiplier", 1);
            Scribe_Values.Look(ref MountainTemperatureMode, "MountainTemperatureMode", MountainTemperatureMode.Vanilla);
            Scribe_Values.Look(ref MountainTemperature, "MountainTemperature", TemperatureTuning.DeepUndergroundTemperature);
            Scribe_Values.Look(ref MountainTemperatureOffset, "MountainTemperatureOffset");
            Scribe_Values.Look(ref TemperatureDisplayDigits, "TemperatureDisplayDigits", TemperatureDisplayDigits_Default);
            Scribe_Values.Look(ref DebugMode, "DebugMode", forceSave: true);
            if (Scribe.mode == LoadSaveMode.LoadingVars)
                RecalculateValues();
        }

        public static void Reset()
        {
            Log("Settings reset.");
            UseVanillaTemperatureColors = false;
            ShowTemperatureTooltip = true;
            FreezingAndMeltingEnabled = true;
            AutoignitionEnabled = true;
            PawnWeatherEffects = true;
            TicksPerUpdate = TicksPerUpdate_Default;
            ConvectionConductivityEffect = ConvectionConductivityEffect_Default;
            EnvironmentDiffusionFactor = EnvironmentDiffusionFactor_Default;
            RoofInsulation = RoofInsulation_Default;
            HeatPushMultiplier = 1;
            MountainTemperatureMode = MountainTemperatureMode.Vanilla;
            MountainTemperature = TemperatureTuning.DeepUndergroundTemperature;
            MountainTemperatureOffset = 0;
            TemperatureDisplayDigits = TemperatureDisplayDigits_Default;
            RecalculateValues();
            Print();
            TemperatureUtility.SettingsChanged();
        }

        public static void RecalculateValues()
        {
            TemperatureDisplayFormatString = $"F{TemperatureDisplayDigits}";
            TicksPerSlice = TicksPerUpdate / SliceCount;
            SnowMeltCoefficient = TicksPerUpdate * 0.00006f;
            SnowMeltCoefficientRain = SnowMeltCoefficient * 2;
            ConductivityPowerBase = Mathf.Pow(ConductivityPowerBase_Default, (float)TicksPerUpdate_Default / TicksPerUpdate);
            RoofDiffusionFactor = EnvironmentDiffusionFactor * Mathf.Pow(ConductivityPowerBase, RoofInsulation);
            HeatPushEffect = HeatPushEffect_Base * HeatPushMultiplier;
            ThermalProps.Init();
        }

        public static void Print()
        {
            if (!DebugMode)
                return;
            Log($"UseVanillaTemperatureColors: {UseVanillaTemperatureColors}");
            Log($"ShowTemperatureTooltip: {ShowTemperatureTooltip}");
            Log($"FreezingAndMeltingEnabled: {FreezingAndMeltingEnabled}");
            Log($"AutoignitionEnabled: {AutoignitionEnabled}");
            Log($"PawnWeatherEffects: {PawnWeatherEffects}");
            Log($"TicksPerUpdate: {TicksPerUpdate}");
            Log($"ConductivityPowerBase: {ConductivityPowerBase}");
            Log($"ConvectionConductivityEffect: {ConvectionConductivityEffect}");
            Log($"EnvironmentDiffusionFactor: {EnvironmentDiffusionFactor}");
            Log($"RoofInsulation: {RoofInsulation}");
            Log($"HeatPushMultiplier: {HeatPushMultiplier.ToStringPercent()}");
            Log($"HeatPushEffect: {HeatPushEffect}");
            Log($"MountainTemperatureMode: {MountainTemperatureMode}");
            Log($"MountainTemperature: {MountainTemperature.ToStringTemperature()}");
            Log($"MountainTemperatureOffset: {MountainTemperatureOffset.ToStringTemperatureOffset()}");
            Log($"TemperatureDisplayDigits: {TemperatureDisplayDigits}");
        }
    }
}
