using RobotRef = Tyr.Soccer.Robot.Robot;

namespace Tyr.Soccer.RoleAssignment;

internal sealed record FilledRoleAssignment(RobotRef Robot, Role.IRole Role);
internal sealed record UnfilledRoleAssignment(Role.IRole Role, bool IsRequired);

internal sealed record RoleAssignmentResult(
    IReadOnlyList<FilledRoleAssignment> FilledRoles,
    IReadOnlyList<UnfilledRoleAssignment> UnfilledRoles,
    IReadOnlyList<RobotRef> UnassignedRobots,
    IReadOnlyDictionary<int, Role.IRole?> RoleMapping,
    double TotalCost)
{
    public static readonly RoleAssignmentResult Empty = new([], [], [], new Dictionary<int, Role.IRole?>(), 0.0);
}
