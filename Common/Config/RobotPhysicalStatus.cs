using System.Runtime.Serialization;

namespace Tyr.Common.Config;

public class RobotPhysicalStatus
{
    public int Id { get; set; } = -1;
    public bool HasDribbler { get; set; } = false;
    public bool HasDirectKick { get; set; } = false;
    public bool HasChipKick { get; set; } = false;
    [DataMember(Name = "is_3d_printed")] public bool Is3dPrinted { get; set; }
}