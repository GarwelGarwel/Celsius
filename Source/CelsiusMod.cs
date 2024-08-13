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
            content.Label("Celsius_Settings_TakeOwnRisk".Translate().Colorize(Color.red));

            content.Label("Celsius_Settings_UpdateInterval".Translate(TicksPerUpdate), tooltip: "Celsius_Settings_UpdateInterval_tooltip".Translate(TicksPerUpdate_Default));
            TicksPerUpdate = (int)content.Slider(TicksPerUpdate, 60, 1000).RoundWithPrecision(20);

            content.Label($"{"Celsius_Settings_ConvectionConductivityEffect".Translate()} x{ConvectionConductivityEffect}", tooltip: "Celsius_Settings_ConvectionConductivityEffect_tooltip".Translate(ConvectionConductivityEffect_Default));
            ConvectionConductivityEffect = Mathf.Round(content.Slider(ConvectionConductivityEffect, 1, 50));

            content.Label("Celsius_Settings_EnvironmentDiffusion".Translate(EnvironmentDiffusionFactor.ToStringPercent()), tooltip: "Celsius_Settings_EnvironmentDiffusion_tooltip".Translate(EnvironmentDiffusionFactor_Default.ToStringPercent()));
            EnvironmentDiffusionFactor = content.Slider(EnvironmentDiffusionFactor, 0, 1).RoundWithPrecision(0.05f);

            content.Label("Celsius_Settings_RoofInsulation".Translate(RoofInsulation), tooltip: "Celsius_Settings_RoofInsulation_tooltip".Translate(RoofInsulation_Default));
            RoofInsulation = content.Slider(RoofInsulation, 0, 20).RoundWithPrecision(0.5f);

            content.Label("Celsius_Settings_HeatPush".Translate(HeatPushMultiplier.ToStringPercent()), tooltip: "Celsius_Settings_HeatPush_tooltip".Translate());
            HeatPushMultiplier = content.Slider(HeatPushMultiplier, 0, 2).RoundWithPrecision(0.1f);
            HeatPushEffect = HeatPushEffect_Base * HeatPushMultiplier;

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
            TemperatureUtility.SettingsChanged();
            base.WriteSettings();
            LogUtility.Log("Settings changed.");
            Print();
        }
    }
}
