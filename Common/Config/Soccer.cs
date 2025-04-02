namespace Tyr.Common.Config;

public class Soccer
{
    public List<RobotPhysicalStatus> RobotPhysicalStatus { get; set; } = new(Common.MaxRobots);

    public VelocityProfile VelocityProfileSooski { get; set; }
    public VelocityProfile VelocityProfileAroom { get; set; }
    public VelocityProfile VelocityProfileMamooli { get; set; }
    public VelocityProfile VelocityProfileKharaki { get; set; }

    public float OneTouchBeta { get; set; } = 0.4f;
    public float OneTouchGamma { get; set; } = 0.14f;
    public float OneTouchShootK { get; set; } = 4000.0f;
    public float GkTightStartAngle { get; set; } = 20.0f;
    public float DefTightStartAngle { get; set; } = 40.0f;
    public float DefPredictionTime { get; set; } = 0.5f;
    public float DefMaxExtensionArea { get; set; } = 1100.0f;
    public float DefBallDistanceCoef { get; set; } = 0.7f;

    public float KickTuneCoef { get; set; } = 1.0f;
    public float ChipTuneCoef { get; set; } = 1.0f;

    public bool MarkInStop { get; set; } = false;
    public float MarkDistance { get; set; } = 500.0f;

    public bool PenaltyAreaMark { get; set; } = false;
    public float PenaltyAreaMarkDistance { get; set; } = 120.0f;

    // post-deserialization validation step
    public void Validate()
    {
        for (int i = 0; i < RobotPhysicalStatus.Count; i++)
        {
            var status = RobotPhysicalStatus[i];

            if (status.Id == -1)
            {
                Console.Error.WriteLine($"Robot ID is missing for index {i}");
            }

            if (status.Id != i)
            {
                Console.Error.WriteLine($"Robot ID mismatch at index {i}: got {status.Id}, expected {i}");
            }
        }
    }
}