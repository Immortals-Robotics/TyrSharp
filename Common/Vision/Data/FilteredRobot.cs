using Tyr.Common.Data.Ssl;
using Tyr.Common.Sender.Data;

namespace Tyr.Common.Vision.Data;

public readonly record struct FilteredRobot
{
    public RobotId Id { get; init; }

    public Timestamp Timestamp { get; init; }

    public RobotState State { get; init; }

    /// <summary>
    /// Quality value between 0 and 1
    /// </summary>
    public float Quality { get; init; }

    public FilteredRobot Extrapolate(Timestamp timestamp, IEnumerable<(Timestamp, Command)>? commandHistory = null)
    {
        if (timestamp <= Timestamp) return this;
        
        RobotState state;
        
        if (commandHistory != null)
        {
            var position = State.Position;
            
            var headTime = Timestamp;
            foreach (var (time, command) in commandHistory)
            {
                if (time < headTime) continue;
                if (time > timestamp) break;
                
                var dt = time - headTime;
                headTime = time;

                position += command.Motion * (float)dt.Seconds;
            }
            
            state = State with
            {
                Position = position
            };
        }
        else
        {
            var dt = timestamp - Timestamp;
            var dtSeconds = (float)dt.Seconds;
            
            state = State with
            {
                Position = State.Position + State.Velocity * dtSeconds,
                Angle = State.Angle + State.AngularVelocity * dtSeconds
            };
        }
        
        return this with
        {
            Timestamp = timestamp,
            State = state
        };
    }
}