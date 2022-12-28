using RimWorld;
using Verse;

namespace Celsius
{
    public class StatPart_WindSpeed : StatPart
    {
        SimpleCurve offset;

        bool AppliesTo(StatRequest req) =>
            Settings.PawnWeatherEffects && offset != null && req.HasThing && req.Thing.Spawned && !req.Thing.Position.Roofed(req.Thing.Map);

        public override void TransformValue(StatRequest req, ref float val)
        {
            if (AppliesTo(req))
                val += offset.Evaluate(req.Thing.Map.windManager.WindSpeed);
        }

        public override string ExplanationPart(StatRequest req) =>
            AppliesTo(req)
                ? "Celsius_StatPart_WindSpeed_Explanation".Translate(
                    req.Thing.Map.windManager.WindSpeed.ToString("F2"),
                    offset.Evaluate(req.Thing.Map.windManager.WindSpeed).ToStringTemperatureOffset())
                : null;
    }
}
