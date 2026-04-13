using Tyr.Common.Config;

namespace Tyr.Soccer.Robot;

[Configurable]
public readonly record struct PhysicalStatus()
{
    [ConfigEntry]
    public static PhysicalStatus[] StatusArray { get; set; } = new PhysicalStatus[Common.Data.CommonConfigs.MaxRobots];

    public bool HasDribbler { get; init; }
    public bool HasDirectKick { get; init; }
    public bool HasChipKick { get; init; }
    public bool Is3dPrinted { get; init; }
    public float CenterToDribbler { get; init; } = 75f;
}