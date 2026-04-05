using System.Numerics;
using Tyr.Common.Extensions;
using Tyr.Common.Time;
using Tyr.Common.Vision.Data;
using Tyr.Vision.Data;

namespace Tyr.Vision.Trajectory;

public class BallChip : IBallTrajectory
{
    private const float Gravity = 9810f;

    private readonly Vector3 _initialPosition;
    private readonly Vector3 _initialVelocity;
    private readonly Vector2 _initialSpin;
    private readonly float _timeInAir;
    private readonly Vector2 _kickPosition;
    private readonly Vector3 _kickVelocity;

    public BallChip(BallState initial)
    {
        _initialPosition = initial.Position3D;
        _initialVelocity = initial.Velocity;
        _initialSpin = initial.SpinRadians;

        var discriminant = MathF.Max(0f, (initial.Velocity.Z * initial.Velocity.Z) + (2f * Gravity * initial.Position3D.Z));
        _timeInAir = (MathF.Sqrt(discriminant) - initial.Velocity.Z) / Gravity;

        var tKickBack = -_timeInAir;
        var acceleration = new Vector3(0f, 0f, -Gravity);
        var kickPosition = initial.Position3D
                           + (initial.Velocity * tKickBack)
                           + (acceleration * (0.5f * tKickBack * tKickBack));

        _kickPosition = kickPosition.Xy();
        _kickVelocity = new Vector3(initial.Velocity.X, initial.Velocity.Y, MathF.Sqrt(discriminant));
    }

    private BallChip(Vector2 kickPosition, Vector3 kickVelocity, Vector2 kickSpin)
    {
        _initialPosition = kickPosition.Xyz();
        _initialVelocity = kickVelocity;
        _initialSpin = kickSpin;
        _timeInAir = 0f;
        _kickPosition = kickPosition;
        _kickVelocity = kickVelocity;
    }

    public static BallChip FromKick(Vector2 kickPosition, Vector3 kickVelocity, Vector2 kickSpin)
    {
        return new BallChip(kickPosition, kickVelocity, kickSpin);
    }

    public BallState GetState(DeltaTime time)
    {
        if (time.Seconds < 0f)
        {
            return new BallState
            {
                Position3D = _initialPosition,
                Velocity = _initialVelocity,
                Acceleration = Vector3.Zero,
                SpinRadians = _initialSpin
            };
        }

        var tQuery = (float)time.Seconds + _timeInAir;

        var position = _kickPosition.Xyz();
        var velocity = _kickVelocity;
        var acceleration = new Vector3(0f, 0f, -Gravity);
        var elapsed = 0f;
        var spin = _initialSpin;

        while (HopHeight(velocity) > BallParameters.MinHopHeight)
        {
            var flightTime = (2f * velocity.Z) / Gravity;

            if ((elapsed + flightTime) > tQuery)
            {
                var t = tQuery - elapsed;
                position += (velocity * t) + new Vector3(0f, 0f, -0.5f * Gravity * t * t);
                velocity += new Vector3(0f, 0f, -Gravity * t);

                return new BallState
                {
                    Position3D = position,
                    Velocity = velocity,
                    Acceleration = acceleration,
                    SpinRadians = spin
                };
            }

            position += velocity * flightTime;
            position.Z = 0f;
            velocity *= GetDamping(spin);
            elapsed += flightTime;

            spin = velocity.Xy() / BallParameters.Radius;
        }

        velocity.Z = 0f;

        var rollTime = tQuery - elapsed;
        var stopTime = GetRollingStopTime(velocity.Xy());
        if (rollTime > stopTime)
        {
            rollTime = stopTime;
        }

        var rollingAcceleration2D = velocity.Xy().WithLength(BallParameters.AccelerationRoll);
        position += (velocity * rollTime) + (rollingAcceleration2D.Xyz() * (0.5f * rollTime * rollTime));
        velocity += rollingAcceleration2D.Xyz() * rollTime;
        spin = velocity.Xy() / BallParameters.Radius;

        return new BallState
        {
            Position3D = position,
            Velocity = velocity,
            Acceleration = rollingAcceleration2D.Xyz(),
            SpinRadians = spin
        };
    }

    private DeltaTime RestTime()
    {
        var velocity = _kickVelocity;
        var elapsed = -_timeInAir;
        var spin = _initialSpin;

        while (HopHeight(velocity) > BallParameters.MinHopHeight)
        {
            var flightTime = (2f * velocity.Z) / Gravity;
            velocity *= GetDamping(spin);
            elapsed += flightTime;
            spin = velocity.Xy() / BallParameters.Radius;
        }

        velocity.Z = 0f;
        elapsed += GetRollingStopTime(velocity.Xy());

        return DeltaTime.FromSeconds(elapsed);
    }

    private static float HopHeight(Vector3 velocity)
    {
        return (velocity.Z * velocity.Z) / (2f * Gravity);
    }

    private static float GetRollingStopTime(Vector2 velocity)
    {
        if (velocity.LengthSquared() <= 0f)
        {
            return 0f;
        }

        return -velocity.Length() / BallParameters.AccelerationRoll;
    }

    private static Vector3 GetDamping(Vector2 spin)
    {
        if (spin.LengthSquared() > 0f)
        {
            return new Vector3(
                BallParameters.ChipDampingXyOtherHops,
                BallParameters.ChipDampingXyOtherHops,
                BallParameters.ChipDampingZ);
        }

        return new Vector3(
            BallParameters.ChipDampingXyFirstHop,
            BallParameters.ChipDampingXyFirstHop,
            BallParameters.ChipDampingZ);
    }
}
