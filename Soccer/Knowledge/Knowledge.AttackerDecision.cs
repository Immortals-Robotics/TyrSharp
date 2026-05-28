using System.Numerics;
using Tyr.Common.Config;
using Tyr.Common.Math.Shapes;

namespace Tyr.Soccer.Knowledge;

public partial class Knowledge
{
    [ConfigEntry] private static float AttackerKickSpeed { get; set; } = 6500.0f;
    [ConfigEntry] private static float PassKickSpeed { get; set; } = 3000.0f;
    [ConfigEntry] private static float PassChipSpeed { get; set; } = 2000.0f;
    [ConfigEntry] private static float DefensiveChipSpeed { get; set; } = 2000.0f;
    [ConfigEntry] private static float GoalOpenAngleForPassDeg { get; set; } = 15.0f;
    [ConfigEntry] private static float OpponentNearBallDistance { get; set; } = 150.0f;
    [ConfigEntry] private static float ReceiverReachedDistance { get; set; } = 1000.0f;
    [ConfigEntry] private static float MinReceiverBallDistance { get; set; } = 1000.0f;
    [ConfigEntry] private static float MaxPassToOwnGoalDot { get; set; } = 0.85f;
    [ConfigEntry] private static int PassShootHysteresisMax { get; set; } = 30;
    [ConfigEntry] private static float PassBlockerRadius { get; set; } = 150.0f;
    [ConfigEntry] private static float ShootBlockerRadius { get; set; } = 90.0f;
    [ConfigEntry] private static float FarBallShotDistance { get; set; } = 400.0f;

    public readonly record struct AttackerDecision(Vector2 TargetPosition, float Kick, float Chip, bool IsPass);
    private readonly record struct PassCandidate(Vector2 TargetPosition, float Score);

    private readonly Dictionary<int, AttackerDecision> _attackerDecisions = [];
    private readonly Dictionary<int, int> _passShootHysteresisByRobot = [];
    private readonly List<int> _staleHysteresisKeys = []; // Bolt: pre-allocated list to avoid per-frame allocations

    public AttackerDecision GetAttackerDecision(int robotId)
    {
        return _attackerDecisions.GetValueOrDefault(robotId,
            new AttackerDecision(Context.Field.OppGoal(), AttackerKickSpeed, 0.0f, false));
    }

    private void UpdateAttackerDecisions()
    {
        _attackerDecisions.Clear();
        var assignedAttackers = new HashSet<int>();

        foreach (var assignment in Context.RoleAssignment.FilledRoles)
        {
            if (assignment.Role is not Tyr.Soccer.Role.Attacker attackerRole)
            {
                continue;
            }

            var robotId = assignment.Robot.Id;
            assignedAttackers.Add(robotId);
            _attackerDecisions[robotId] = BuildAttackerDecision(assignment.Robot, attackerRole);
        }

        // Bolt: eliminates LINQ .Where(...).ToList() allocation per frame
        _staleHysteresisKeys.Clear();
        foreach (var robotId in _passShootHysteresisByRobot.Keys)
        {
            if (!assignedAttackers.Contains(robotId))
            {
                _staleHysteresisKeys.Add(robotId);
            }
        }

        foreach (var robotId in _staleHysteresisKeys)
        {
            _passShootHysteresisByRobot.Remove(robotId);
        }
    }

    private AttackerDecision BuildAttackerDecision(Robot.Robot robot, Tyr.Soccer.Role.Attacker attackerRole)
    {
        if (attackerRole.Mode == Tyr.Soccer.Role.Attacker.PlayMode.Defending)
        {
            return BuildDefensiveDecision(robot.Position);
        }

        if (!attackerRole.AllowPassing)
        {
            _passShootHysteresisByRobot[robot.Id] = -Math.Max(1, PassShootHysteresisMax);
            return BuildDirectShootDecision(robot.Position);
        }

        var passCandidate = FindPassCandidate(robot.Id);
        var openAngle = OpenAngle.CalculateOpenAngleToGoal(Context.Ball.State.Position, robot);
        var opponentNearBall = FindKickerOpp(maxDis: OpponentNearBallDistance).HasValue;
        var preferPass = openAngle.Magnitude.Deg < GoalOpenAngleForPassDeg &&
                         passCandidate.HasValue &&
                         !opponentNearBall;

        var passShootHys = UpdatePassShootHysteresis(robot.Id, preferPass, passCandidate.HasValue);
        if (passShootHys > 0 && passCandidate.HasValue)
        {
            return BuildPassDecision(passCandidate.Value.TargetPosition);
        }

        return BuildDirectShootDecision(robot.Position);
    }

    private int UpdatePassShootHysteresis(int robotId, bool preferPass, bool hasCandidate)
    {
        var maxHys = Math.Max(1, PassShootHysteresisMax);
        var current = _passShootHysteresisByRobot.GetValueOrDefault(robotId, 0);

        int next;
        if (!hasCandidate)
        {
            next = -maxHys;
        }
        else if (preferPass)
        {
            next = current <= 0
                ? 1
                : Math.Min(current + 1, maxHys);
        }
        else
        {
            next = current >= 0
                ? -1
                : Math.Max(current - 1, -maxHys);
        }

        _passShootHysteresisByRobot[robotId] = next;
        return next;
    }

    private PassCandidate? FindPassCandidate(int attackerRobotId)
    {
        var ballPos = Context.Ball.State.Position;
        var toOwnGoal = Context.Field.OwnGoal() - ballPos;
        var ownGoalDirection = toOwnGoal.LengthSquared() > 0.001f ? Vector2.Normalize(toOwnGoal) : Vector2.Zero;

        PassCandidate? best = null;
        foreach (var assignment in Context.RoleAssignment.FilledRoles)
        {
            var teammate = assignment.Robot;
            if (teammate.Id == attackerRobotId || !teammate.Seen)
            {
                continue;
            }

            if (assignment.Role is not Tyr.Soccer.Role.Supporter and not Tyr.Soccer.Role.Waiter)
            {
                continue;
            }

            var physical = teammate.PhysicalStatus;
            if (!physical.HasDirectKick || physical.Is3dPrinted)
            {
                continue;
            }

            var targetPosition = assignment.Role switch
            {
                Tyr.Soccer.Role.Waiter waiter => waiter.Target,
                Tyr.Soccer.Role.Supporter supporter => supporter.Zone.BestPosOffence,
                _ => teammate.Position
            };

            if (Vector2.Distance(teammate.Position, targetPosition) > ReceiverReachedDistance)
            {
                continue;
            }

            var ballToTarget = targetPosition - ballPos;
            if (ballToTarget.LengthSquared() < 0.001f)
            {
                continue;
            }

            if (Vector2.Distance(targetPosition, ballPos) < MinReceiverBallDistance)
            {
                continue;
            }

            var passDirection = Vector2.Normalize(ballToTarget);
            if (ownGoalDirection.LengthSquared() > 0.001f &&
                Vector2.Dot(passDirection, ownGoalDirection) >= MaxPassToOwnGoalDot)
            {
                continue;
            }

            var openAngle = OpenAngle.CalculateOpenAngleToGoal(targetPosition, teammate);
            var score = openAngle.Magnitude.Deg;
            if (!best.HasValue || score > best.Value.Score)
            {
                best = new PassCandidate(targetPosition, score);
            }
        }

        return best;
    }

    private static bool IsLaneBlocked(Vector2 from, Vector2 to, float blockerRadius)
    {
        var segment = new LineSegment { Start = from, End = to };
        var length = segment.Length();
        if (length < 1.0f)
        {
            return true;
        }

        var direction = (to - from) / length;
        foreach (var opp in Context.OppRobots)
        {
            if (opp.Quality <= 0f)
            {
                continue;
            }

            var fromStart = opp.State.Position - from;
            var projection = Vector2.Dot(fromStart, direction);
            if (projection <= 0f || projection >= length)
            {
                continue;
            }

            if (segment.Distance(opp.State.Position) < blockerRadius)
            {
                return true;
            }
        }

        return false;
    }

    private static AttackerDecision BuildDirectShootDecision(Vector2 attackerPosition)
    {
        var kick = Vector2.Distance(attackerPosition, Context.Ball.State.Position) > FarBallShotDistance
            ? 1.0f
            : AttackerKickSpeed;

        return new AttackerDecision(Context.Field.OppGoal(), kick, 0.0f, false);
    }

    private static AttackerDecision BuildDefensiveDecision(Vector2 attackerPosition)
    {
        var target = Context.Field.OppGoal();
        if (IsLaneBlocked(Context.Ball.State.Position, target, ShootBlockerRadius))
        {
            return new AttackerDecision(target, 0.0f, DefensiveChipSpeed, false);
        }

        return BuildDirectShootDecision(attackerPosition);
    }

    private static AttackerDecision BuildPassDecision(Vector2 targetPosition)
    {
        if (IsLaneBlocked(Context.Ball.State.Position, targetPosition, PassBlockerRadius))
        {
            return new AttackerDecision(targetPosition, 0.0f, PassChipSpeed, true);
        }

        return new AttackerDecision(targetPosition, PassKickSpeed, 0.0f, true);
    }
}
