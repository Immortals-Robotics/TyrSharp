using Tyr.Common.Config;
using Tyr.Common.Vision.Data;
using Tyr.Vision.Data;

namespace Tyr.Vision.Util;

[Configurable]
public partial class KickValidator
{
    [ConfigEntry("Maximum angle difference for a valid kick")]
    private static double MaxAngleDiff { get; set; } = 45.0;

    private readonly Dictionary<uint, List<MergedBall>> _ballsByCamera = new(2);
    private readonly Stack<List<MergedBall>> _ballGroupPool = [];

    public bool Validate(List<MergedBall> balls, List<FilteredRobot> robots)
    {
        try
        {
            PrepareBallsByCamera(balls);

            var validateDistance = DistanceValidator(balls, robots);
            var validateVelocity = VelocityValidator();
            var validateGettingAway = GettingAwayValidator(robots);
            var validateInKicker = InKickerValidator(balls, robots);

            Log.ZLogDebug(
                $"Kick validator: Distance: {validateDistance} Velocity: {validateVelocity} GettingAway: {validateGettingAway} InKicker: {validateInKicker}");
            return validateDistance & validateVelocity & validateGettingAway & validateInKicker;
        }
        finally
        {
            ClearBallsByCamera();
        }
    }

    private void PrepareBallsByCamera(List<MergedBall> balls)
    {
        for (var i = 0; i < balls.Count; i++)
        {
            var ball = balls[i];
            if (!ball.LatestRawBall.HasValue)
            {
                continue;
            }

            var cameraId = ball.LatestRawBall.Value.CameraId;
            if (!_ballsByCamera.TryGetValue(cameraId, out var group))
            {
                group = GetBallGroup();
                _ballsByCamera[cameraId] = group;
            }

            group.Add(ball);
        }
    }

    private List<MergedBall> GetBallGroup()
    {
        return _ballGroupPool.Count > 0 ? _ballGroupPool.Pop() : [];
    }

    private void ClearBallsByCamera()
    {
        foreach (var group in _ballsByCamera.Values)
        {
            group.Clear();
            _ballGroupPool.Push(group);
        }

        _ballsByCamera.Clear();
    }
}
