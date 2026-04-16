using Tyr.Soccer.Role;

namespace Tyr.Soccer.Plays;

public sealed record Formation
{
    public static readonly Formation Empty = new();

    public IReadOnlyList<IRole> RequiredRoles { get; init; } = [];
    public IReadOnlyList<IRole> DesiredRoles { get; init; } = [];
}
