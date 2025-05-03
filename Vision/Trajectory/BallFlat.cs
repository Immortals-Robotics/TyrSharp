using System.Numerics;
using Tyr.Common.Data.Vision;
using Tyr.Common.Time;

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
        var contactVelocity = initial.Velocity.Xy() - initial.SpinRadians * BallParameters.Radius;

        if (contactVelocity.Length() < 0.01f)
        {
            // ball is rolling
            _accelerationSlide = initial.Velocity.Xy().WithLength(BallParameters.AccelerationRoll);
            _accelerationSlideSpin = _accelerationSlide / BallParameters.Radius;
            _switchTime = DeltaTime.Zero;
        }
        else
        {
            // ball is sliding
            _accelerationSlide = contactVelocity.WithLength(BallParameters.AccelerationSlide);
            _accelerationSlideSpin = _accelerationSlide / (BallParameters.Radius * BallParameters.InertiaDistribution);
            var f = 1f / (1f + 1f / BallParameters.InertiaDistribution);
            var slideVel = (initial.SpinRadians * BallParameters.Radius) - initial.Velocity.Xy() * f;

            _switchTime = MathF.Abs(_accelerationSlide.X) > MathF.Abs(_accelerationSlide.Y)
                ? DeltaTime.FromSeconds(slideVel.X / _accelerationSlide.X)
                : DeltaTime.FromSeconds(slideVel.Y / _accelerationSlide.Y);
        }

        _switchVelocity = initial.Velocity.Xy() + _accelerationSlide * (float)_switchTime.Seconds;
        _switchPosition = initial.Position.Xy() +
                          initial.Velocity.Xy() * (float)_switchTime.Seconds +
                          _accelerationSlide * (float)(0.5 * _switchTime.Seconds * _switchTime.Seconds);

        _accelerationRoll = _switchVelocity.WithLength(BallParameters.AccelerationRoll);
    }

    public BallState GetState(DeltaTime time)
    {
        if (time.Seconds <= 0f) return _initial;

        if (time < _switchTime)
        {
            var timeSeconds = (float)time.Seconds;

            var position = _initial.Position.Xy() +
                           _initial.Velocity.Xy() * timeSeconds +
                           _accelerationSlide * (0.5f * timeSeconds * timeSeconds);

            var velocity = _initial.Velocity.Xy() + _accelerationSlide * timeSeconds;
            var spinRadians = _initial.SpinRadians - _accelerationSlideSpin * timeSeconds;

            return new BallState
            {
                Position = position.Xyz(),
                Velocity = velocity.Xyz(),
                Acceleration = _accelerationSlide.Xyz(),
                SpinRadians = spinRadians
            };
        }
        else
        {
            var t2 = DeltaTime.Min(time, RestTime()) - _switchTime;
            var t2Seconds = (float)t2.Seconds;

            var position = _switchPosition +
                           _switchVelocity * t2Seconds +
                           _accelerationRoll * (0.5f * t2Seconds * t2Seconds);

            var velocity = _switchVelocity + _accelerationRoll * t2Seconds;
            var spinRadians = velocity / BallParameters.Radius;

            return new BallState
            {
                Position = position.Xyz(),
                Velocity = velocity.Xyz(),
                Acceleration = _accelerationRoll.Xyz(),
                SpinRadians = spinRadians
            };
        }
    }

    private DeltaTime RestTime()
    {
        var tStop = DeltaTime.FromSeconds(-_switchVelocity.LengthSquared() / BallParameters.AccelerationRoll);
        return _switchTime + tStop;
    }
}