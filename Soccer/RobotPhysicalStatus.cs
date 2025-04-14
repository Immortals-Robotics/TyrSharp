namespace Tyr.Soccer;

public class RobotPhysicalStatus
{
    public int Id { get; set; } = -1;
    public bool HasDribbler { get; set; } = false;
    public bool HasDirectKick { get; set; } = false;
    public bool HasChipKick { get; set; } = false;
    public bool Is3dPrinted { get; set; }
}