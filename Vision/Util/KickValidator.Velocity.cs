using System.Numerics;
using Tyr.Common.Config;
using Tyr.Vision.Data;

namespace Tyr.Vision.Util;

public partial class KickValidator
{
    [ConfigEntry("Minimum required ball velocity [mm/s]")]
    private static partial double MinVelocity { get; set; } = 600.0;

    private bool VelocityValidator()
    {
        var validSamples = 0;

        foreach (var group in _ballsByCamera.Values)
        {
            for (var i = 1; i < group.Count; i++)
            {
                var bPrev = group[i - 1].LatestRawBall!.Value;
                var bNow = group[i].LatestRawBall!.Value;
                var tPrev = bPrev.CaptureTimestamp;
                var tNow = bNow.CaptureTimestamp;
                var prev = bPrev.Detection.Position;
                var now = bNow.Detection.Position;

                if (tPrev == tNow)
                {
                    continue;
                }

                var vel = Vector2.Distance(prev, now) / ((tNow - tPrev).Seconds);

                if (vel > MinVelocity)
                {
                    validSamples++;
                }
            }

            if (validSamples >= 2)
            {
                return true;
            }
        }

        return false;
    }
}
