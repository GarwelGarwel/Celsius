using System;
using UnityEngine;
using Verse;

using static Celsius.Settings;

namespace Celsius
{
    public class CelsiusMod : Mod
    {
        Vector2 scrollPosition = new Vector2();
        Rect viewRect;
        Listing_Standard content = new Listing_Standard();

        public CelsiusMod(ModContentPack content)
            : base(content) =>
            GetSettings<Settings>();

        public override void DoSettingsWindowContents(Rect rect)
        {
            if (viewRect.height <= 0)
                viewRect = new Rect(0, 0, rect.width - GenUI.ScrollBarWidth, 0);
            Widgets.BeginScrollView(rect, ref scrollPosition, viewRect);
            content.ColumnWidth = viewRect.width;
            content.Begin(viewRect);

            content.CheckboxLabeled("Celsius_Settings_Vanilla".Translate(), ref UseVanillaTemperatureColors, "Celsius_Settings_Vanilla_tooltip".Translate());
            content.CheckboxLabeled("Celsius_Settings_ShowTempTooltip".Translate(), ref ShowTemperatureTooltip, "Celsius_Settings_ShowTempTooltip_tooltip".Translate());
            content.CheckboxLabeled("Celsius_Settings_FreezingAndMelting".Translate(), ref FreezingAndMeltingEnabled, "Celsius_Settings_FreezingAndMelting_tooltip".Translate());
            content.CheckboxLabeled("Celsius_Settings_AdvAutoignition".Translate(), ref AutoignitionEnabled, "Celsius_Settings_AdvAutoignition_tooltip".Translate());
            content.CheckboxLabeled("Celsius_Settings_PawnWeatherEffects".Translate(), ref PawnWeatherEffects, "Celsius_Settings_PawnWeatherEffects_tooltip".Translate());

            content.Label("Celsius_Settings_TempDisplayDigits".Translate(TemperatureDisplayDigits), tooltip: "Celsius_Settings_TempDisplayDigits_tooltip".Translate(TemperatureDisplayDigits_Default));
            TemperatureDisplayDigits = Mathf.RoundToInt(content.Slider(TemperatureDisplayDigits, 0, 2));
            TemperatureDisplayFormatString = $"F{TemperatureDisplayDigits}";

            content.Gap();
            content.Label("Celsius_Settings_MountainTemp".Translate());

            for (MountainTemperatureMode mode = 0; mode <= MountainTemperatureMode.Manual; mode++)
            {
                string tooltip = $"Celsius_MountainTemperatureMode_{mode.ToStringSafe()}_tooltip".Translate();
                if (Find.CurrentMap?.TemperatureInfo() != null)
                    tooltip += "\n" + "Celsius_Settings_CurrentMapTemp_tooltip".Translate(Find.CurrentMap.TemperatureInfo().GetMountainTemperatureFor(mode).ToStringTemperature(TemperatureDisplayFormatString));
                if (content.RadioButton($"Celsius_MountainTemperatureMode_{mode.ToStringSafe()}".Translate(), Settings.MountainTemperatureMode == mode, (int)mode, tooltip))
                    Settings.MountainTemperatureMode = mode;
            }

            if (Settings.MountainTemperatureMode == MountainTemperatureMode.Manual)
            {
                content.Label("Celsius_Settings_ManualTemperature".Translate(MountainTemperature.ToStringTemperature("F0")));
                MountainTemperature = Mathf.Round(content.Slider(MountainTemperature, -100, 100));
            }
            else if (Settings.MountainTemperatureMode != MountainTemperatureMode.Vanilla)
            {
                content.Label("Celsius_Settings_MountainTempOffset".Translate(MountainTemperatureOffset.ToStringTemperatureOffset("F0")));
                MountainTemperatureOffset = Mathf.Round(content.Slider(MountainTemperatureOffset, -50, 50));
            }
    
            content.Gap();
            content.Label("Celsius_Settings_TakeOwnRisk".Translate().Colorize(Color.red));

            content.Label("Celsius_Settings_UpdateInterval".Translate(TicksPerUpdate), tooltip: "Celsius_Settings_UpdateInterval_tooltip".Translate(TicksPerUpdate_Default));
            TicksPerUpdate = (int)content.Slider(TicksPerUpdate, 60, 300) / 20 * 20;

            content.Label($"{"Celsius_Settings_ConvectionConductivityEffect".Translate()} x{ConvectionConductivityEffect}", tooltip: "Celsius_Settings_ConvectionConductivityEffect_tooltip".Translate(ConvectionConductivityEffect_Default));
            ConvectionConductivityEffect = Mathf.Round(content.Slider(ConvectionConductivityEffect, 1, 50));

            content.Label("Celsius_Settings_EnvironmentDiffusion".Translate(EnvironmentDiffusionFactor.ToStringPercent()), tooltip: "Celsius_Settings_EnvironmentDiffusion_tooltip".Translate(EnvironmentDiffusionFactor_Default.ToStringPercent()));
            EnvironmentDiffusionFactor = (float)Math.Round(content.Slider(EnvironmentDiffusionFactor, 0, 1), 1);

            content.Label("Celsius_Settings_RoofInsulation".Translate(RoofInsulation), tooltip: "Celsius_Settings_RoofInsulation_tooltip".Translate(RoofInsulation_Default));
            RoofInsulation = Mathf.Round(content.Slider(RoofInsulation, 0, 20));

            content.Label("Celsius_Settings_HeatPush".Translate(HeatPushMultiplier.ToStringPercent()), tooltip: "Celsius_Settings_HeatPush_tooltip".Translate());
            HeatPushMultiplier = (float)Math.Round(content.Slider(HeatPushMultiplier, 0, 2), 1);
            HeatPushEffect = HeatPushEffect_Base * HeatPushMultiplier;

            content.CheckboxLabeled("Celsius_Settings_Threading".Translate(), ref Threading, "Celsius_Settings_Threading_tooltip".Translate());
            if (Threading)
            {
                content.Label("Celsius_Settings_NumThreadWorkers".Translate(NumThreadsWorkers), tooltip: "Celsius_Settings_NumThreadWorkers_tooltip".Translate());
                NumThreadsWorkers = (int)content.Slider(NumThreadsWorkers, 2, 64);
            }

            content.CheckboxLabeled("Celsius_Settings_UnityJobs".Translate(), ref UseUnityJobs, "Celsius_Settings_UnityJobs_tooltip".Translate());
            
            content.CheckboxLabeled("Celsius_Settings_DebugMode".Translate(), ref DebugMode, "Celsius_Settings_DebugMode_tooltip".Translate());

            if (content.ButtonText("Celsius_Settings_ResetDefault".Translate()))
                Reset();

            viewRect.height = content.MaxColumnHeightSeen + 40;
            content.End();
            Widgets.EndScrollView();
        }

        public override string SettingsCategory() => "Celsius";

        public override void WriteSettings()
        {
            RecalculateValues();
            base.WriteSettings();
            LogUtility.Log("Settings changed.");
            Print();
            TemperatureUtility.SettingsChanged();
        }
    }
}
