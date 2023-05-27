using RimWorld;
using Verse;

namespace Celsius
{
    public class StatPart_DirectSunlight : StatPart
    {
        public float minGlow = 1;
        public float offset;

        public bool AtDirectSunlight(Thing thing) =>
            thing != null
            && thing.Spawned
            && thing.Map != null
            && thing.Map.skyManager.CurSkyGlow >= minGlow
            && thing.Map.weatherManager.curWeather == WeatherDefOf.Clear
            && !thing.Position.Roofed(thing.Map);

        bool AppliesTo(StatRequest req) => Settings.PawnWeatherEffects && offset != 0 && AtDirectSunlight(req.Thing);

        public override void TransformValue(StatRequest req, ref float val)
        {
            if (AppliesTo(req))
                val += offset;
        }

        public override string ExplanationPart(StatRequest req) =>
            AppliesTo(req)
                ? "Celsius_StatPart_DirectSunlight_Explanation".Translate(offset.ToStringTemperatureOffset())
                : null;
    }
}
