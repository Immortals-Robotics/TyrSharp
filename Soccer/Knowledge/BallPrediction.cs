using Tyr.Common.Extensions;
using Tyr.Common.Math;
using Tyr.Common.Time;
using Tyr.Common.Vision.Data;
using Tyr.Soccer.Robot;

namespace Tyr.Soccer.Knowledge;

public class BallPrediction
{
    private const float MaxReachTimeSeconds = 5f;

    public BallState PredictBall(DeltaTime timeAhead)
    {
        return Context.Ball.Extrapolate(Context.Ball.Timestamp + timeAhead).State;
    }

    public float CalculateBallRobotReachTime(Robot.Robot robot, Angle angle, VelocityProfile profile, float waitTimeSeconds)
    {
        for (var t = 0f; t < MaxReachTimeSeconds; t += 0.1f)
        {
            var ballState = PredictBall(DeltaTime.FromSeconds(t));
            var interceptionPoint = ballState.Position.CircleAroundPoint(angle, Context.RobotRadius);
            var reachTime = robot.CalculateReachTime(interceptionPoint, profile);

            if (reachTime + waitTimeSeconds <= t)
            {
                return reachTime;
            }
        }

        return MaxReachTimeSeconds;
    }
}
