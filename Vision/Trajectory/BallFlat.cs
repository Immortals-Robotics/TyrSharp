using System.Numerics;
using Tyr.Common.Time;
using Tyr.Common.Vision.Data;

namespace Tyr.Vision.Trajectory;

public class BallFlat : IBallTrajectory
{
    private readonly BallState _initial;

    private readonly DeltaTime _switchTime;
    private readonly Vector2 _switchPosition;
    private readonly Vector2 _switchVelocity;

    private readonly Vector2 _accelerationSlide;
    private readonly Vector2 _accelerationSlideSpin;
    private readonly Vector2 _accelerationRoll;

    public BallFlat(BallState initial)
    {
        _initial = initial;

        // compute relative velocity of ball to ground surface, if ball is rolling this is close to zero
        var contactVelocity = initial.Velocity.Xy() - initial.SpinRadians * Data.BallParameters.Radius;

        if (contactVelocity.Length() < 0.01f)
        {
            // ball is rolling
            _accelerationSlide = initial.Velocity.Xy().WithLength(Data.BallParameters.AccelerationRoll);
            _accelerationSlideSpin = _accelerationSlide / Data.BallParameters.Radius;
            _switchTime = DeltaTime.Zero;
        }
        else
        {
            // ball is sliding
            _accelerationSlide = contactVelocity.WithLength(Data.BallParameters.AccelerationSlide);
            _accelerationSlideSpin = _accelerationSlide /
                                     (Data.BallParameters.Radius * Data.BallParameters.InertiaDistribution);
            var f = 1f / (1f + 1f / Data.BallParameters.InertiaDistribution);
            var slideVel = ((initial.SpinRadians * Data.BallParameters.Radius) - initial.Velocity.Xy()) * f;

            _switchTime = MathF.Abs(_accelerationSlide.X) > MathF.Abs(_accelerationSlide.Y)
                ? DeltaTime.FromSeconds(slideVel.X / _accelerationSlide.X)
                : DeltaTime.FromSeconds(slideVel.Y / _accelerationSlide.Y);
        }

        _switchVelocity = initial.Velocity.Xy() + _accelerationSlide * (float)_switchTime.Seconds;
        _switchPosition = initial.Position +
                          initial.Velocity.Xy() * (float)_switchTime.Seconds +
                          _accelerationSlide * (float)(0.5 * _switchTime.Seconds * _switchTime.Seconds);

        _accelerationRoll = _switchVelocity.WithLength(Data.BallParameters.AccelerationRoll);
    }

    public BallState GetState(DeltaTime time)
    {
        if (time.Seconds < 0f)
        {
            return new BallState
            {
                Position3D = _initial.Position3D,
                Velocity = _initial.Velocity,
                Acceleration = Vector3.Zero,
                SpinRadians = _initial.SpinRadians
            };
        }

        if (time < _switchTime)
        {
            var timeSeconds = (float)time.Seconds;

            var position = _initial.Position +
                           _initial.Velocity.Xy() * timeSeconds +
                           _accelerationSlide * (0.5f * timeSeconds * timeSeconds);

            var velocity = _initial.Velocity.Xy() + _accelerationSlide * timeSeconds;
            var spinRadians = _initial.SpinRadians - _accelerationSlideSpin * timeSeconds;

            return new BallState
            {
                Position3D = position.Xyz(),
                Velocity = velocity.Xyz(),
                Acceleration = _accelerationSlide.Xyz(),
                SpinRadians = spinRadians
            };
        }

        var t2 = time - _switchTime;
        if (time > RestTime())
        {
            t2 = RestTime() - _switchTime;
        }

        var t2Seconds = (float)t2.Seconds;

        var rollPosition = _switchPosition +
                           _switchVelocity * t2Seconds +
                           _accelerationRoll * (0.5f * t2Seconds * t2Seconds);

        var rollVelocity = _switchVelocity + _accelerationRoll * t2Seconds;
        var rollSpinRadians = rollVelocity / Data.BallParameters.Radius;

        return new BallState
        {
            Position3D = rollPosition.Xyz(),
            Velocity = rollVelocity.Xyz(),
            Acceleration = _accelerationRoll.Xyz(),
            SpinRadians = rollSpinRadians
        };
    }

    private DeltaTime RestTime()
    {
        var tStop = DeltaTime.FromSeconds(-_switchVelocity.Length() / Data.BallParameters.AccelerationRoll);
        return _switchTime + tStop;
    }
}
