using Tyr.Common.Time;
using Tyr.Soccer.Plays;
using RobotRef = Tyr.Soccer.Robot.Robot;

namespace Tyr.Soccer.RoleAssignment;

internal sealed class RoleAssignmentSolver
{
    private readonly double _requiredRoleUnfilledPenaltySeconds;

    public RoleAssignmentSolver(double requiredRoleUnfilledPenaltySeconds)
    {
        _requiredRoleUnfilledPenaltySeconds = requiredRoleUnfilledPenaltySeconds;
    }

    public RoleAssignmentResult Solve(
        Formation formation,
        IReadOnlyDictionary<int, Role.IRole?> previousRoleMapping)
    {
        var goalkeeperId = (int)Context.Referee.OurInfo().Goalkeeper;
        var assignedGoalieRobot = Context.OwnRobots.FirstOrDefault(r => r.Id == goalkeeperId && r.Seen);
        var robots = Context.OwnRobots.Where(r => r.Seen && r.Id != goalkeeperId).ToArray();

        var requiredGoalieRoles = formation.RequiredRoles.Where(r => r.IsGoalie).ToArray();
        var realRoles = formation.RequiredRoles
            .Where(r => !r.IsGoalie)
            .Concat(formation.DesiredRoles.Where(r => !r.IsGoalie))
            .ToArray();
        var requiredCount = formation.RequiredRoles.Count - requiredGoalieRoles.Length;
        var robotCount = robots.Length;
        var roleCount = realRoles.Length;
        var size = Math.Max(roleCount, robotCount);
        var dummyRobotCount = Math.Max(0, roleCount - robotCount);

        if (size == 0)
        {
            return BuildResultWithForcedGoalie(
                [],
                [],
                [],
                [],
                0.0,
                requiredGoalieRoles,
                assignedGoalieRobot);
        }

        var costs = new double[size, size];

        for (var row = 0; row < size; row++)
        {
            for (var col = 0; col < size; col++)
            {
                costs[row, col] = 0.0;
            }
        }

        for (var roleIndex = 0; roleIndex < roleCount; roleIndex++)
        {
            var role = realRoles[roleIndex];

            for (var robotIndex = 0; robotIndex < robotCount; robotIndex++)
            {
                var robot = robots[robotIndex];
                costs[roleIndex, robotIndex] = ComputeWeightedCost(role, robot, previousRoleMapping);
            }

            var unfilledCost = roleIndex < requiredCount
                ? role.Importance * _requiredRoleUnfilledPenaltySeconds
                : 0.0;
            for (var dummyRobotIndex = 0; dummyRobotIndex < dummyRobotCount; dummyRobotIndex++)
            {
                costs[roleIndex, robotCount + dummyRobotIndex] = unfilledCost;
            }
        }

        var assignment = Hungarian(costs);
        var filledRoles = new List<FilledRoleAssignment>(Math.Min(roleCount, robotCount));
        var unfilledRoles = new List<UnfilledRoleAssignment>(roleCount);
        var unassignedRobots = new List<RobotRef>(Math.Max(0, robotCount - roleCount));
        var roleMapping = new Dictionary<int, Role.IRole?>(Math.Min(roleCount, robotCount));
        double totalCost = 0.0;

        for (var row = 0; row < size; row++)
        {
            var col = assignment[row];
            totalCost += costs[row, col];

            if (row < roleCount)
            {
                var role = realRoles[row];
                var isRequired = row < requiredCount;
                if (col < robotCount)
                {
                    filledRoles.Add(new FilledRoleAssignment(robots[col], role));
                    roleMapping[robots[col].Id] = role;
                }
                else
                {
                    unfilledRoles.Add(new UnfilledRoleAssignment(role, isRequired));
                }
            }
            else if (col < robotCount)
            {
                unassignedRobots.Add(robots[col]);
            }
        }

        return BuildResultWithForcedGoalie(
            filledRoles,
            unfilledRoles,
            unassignedRobots,
            roleMapping,
            totalCost,
            requiredGoalieRoles,
            assignedGoalieRobot);
    }

    private RoleAssignmentResult BuildResultWithForcedGoalie(
        List<FilledRoleAssignment> filledRoles,
        List<UnfilledRoleAssignment> unfilledRoles,
        List<RobotRef> unassignedRobots,
        Dictionary<int, Role.IRole?> roleMapping,
        double totalCost,
        IReadOnlyList<Role.IRole> requiredGoalieRoles,
        RobotRef? assignedGoalieRobot)
    {
        var goalieRole = requiredGoalieRoles.FirstOrDefault();
        if (goalieRole != null)
        {
            if (assignedGoalieRobot != null)
            {
                filledRoles.Add(new FilledRoleAssignment(assignedGoalieRobot, goalieRole));
                roleMapping[assignedGoalieRobot.Id] = goalieRole;
            }
            else
            {
                var isRequired = requiredGoalieRoles.Count > 0;
                unfilledRoles.Add(new UnfilledRoleAssignment(goalieRole, isRequired));
                if (isRequired)
                {
                    totalCost += goalieRole.Importance * _requiredRoleUnfilledPenaltySeconds;
                }
            }
        }

        foreach (var extraRequiredGoalieRole in requiredGoalieRoles.Skip(goalieRole == null ? 0 : 1))
        {
            unfilledRoles.Add(new UnfilledRoleAssignment(extraRequiredGoalieRole, true));
            totalCost += extraRequiredGoalieRole.Importance * _requiredRoleUnfilledPenaltySeconds;
        }

        return new RoleAssignmentResult(filledRoles, unfilledRoles, unassignedRobots, roleMapping, totalCost);
    }

    private static int[] Hungarian(double[,] costs)
    {
        var n = costs.GetLength(0);
        var u = new double[n + 1];
        var v = new double[n + 1];
        var p = new int[n + 1];
        var way = new int[n + 1];

        for (var i = 1; i <= n; i++)
        {
            p[0] = i;
            var minv = new double[n + 1];
            var used = new bool[n + 1];
            Array.Fill(minv, double.PositiveInfinity);
            var j0 = 0;

            do
            {
                used[j0] = true;
                var i0 = p[j0];
                var delta = double.PositiveInfinity;
                var j1 = 0;

                for (var j = 1; j <= n; j++)
                {
                    if (used[j]) continue;

                    var cur = costs[i0 - 1, j - 1] - u[i0] - v[j];
                    if (cur < minv[j])
                    {
                        minv[j] = cur;
                        way[j] = j0;
                    }

                    if (minv[j] < delta)
                    {
                        delta = minv[j];
                        j1 = j;
                    }
                }

                for (var j = 0; j <= n; j++)
                {
                    if (used[j])
                    {
                        u[p[j]] += delta;
                        v[j] -= delta;
                    }
                    else
                    {
                        minv[j] -= delta;
                    }
                }

                j0 = j1;
            } while (p[j0] != 0);

            do
            {
                var j1 = way[j0];
                p[j0] = p[j1];
                j0 = j1;
            } while (j0 != 0);
        }

        var result = new int[n];
        for (var j = 1; j <= n; j++)
        {
            if (p[j] != 0)
            {
                result[p[j] - 1] = j - 1;
            }
        }

        return result;
    }

    private static double ComputeWeightedCost(
        Role.IRole role,
        RobotRef robot,
        IReadOnlyDictionary<int, Role.IRole?> previousRoleMapping)
    {
        var effectiveCost = role.CostFor(robot);
        if (previousRoleMapping.TryGetValue(robot.Id, out var previousRole) && role.Equals(previousRole))
        {
            effectiveCost = DeltaTime.Max(DeltaTime.Zero, effectiveCost - role.StickinessBonus);
        }

        return role.Importance * effectiveCost.Seconds;
    }

}
