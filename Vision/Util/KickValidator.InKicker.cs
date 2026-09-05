using System.Numerics;
using Tyr.Common.Config;
using Tyr.Common.Debug.Drawing;
using Tyr.Common.Vision.Data;
using Tyr.Vision.Data;

namespace Tyr.Vision.Util;

public partial class KickValidator
{
    [ConfigEntry("Minimum distance from bot to lead point on orientation line")]
    private static partial double MinDistanceInFront { get; set; } = 70.0;

    [ConfigEntry(
        "Minimum distance from orientation line to ball position (distance is increased with distance from bot)")]
    private static partial double MinDistanceOrthogonal { get; set; } = 40.0;

    private bool InKickerValidator(List<MergedBall> balls, List<FilteredRobot> robots)
    {
        var bot = robots[0];

        var direction = new Vector2(MathF.Cos(bot.State.Angle.Rad), MathF.Sin(bot.State.Angle.Rad));
        Draw.DrawArrow(bot.State.Position, bot.State.Position + direction, Color.Blue, Options.Outline(15f));
        var botPos = bot.State.Position;

        foreach (var b in balls)
        {
            var ballPos = b.RawPosition;
            var toBall = ballPos - botPos;

            var projectionLength = Vector2.Dot(toBall, direction);
            var leadPoint = botPos + direction * projectionLength;

            if (projectionLength < 0)
            {
                return false;
            }

            var distBotToLeadPoint = Vector2.Distance(botPos, leadPoint);

            if (distBotToLeadPoint < MinDistanceInFront)
            {
                return false;
            }

            var distBallToLeadPoint = Vector2.Distance(ballPos, leadPoint);

            if (distBallToLeadPoint > (MinDistanceOrthogonal + (distBotToLeadPoint - MinDistanceInFront)))
            {
                return false;
            }
        }

        return true;
    }
}
