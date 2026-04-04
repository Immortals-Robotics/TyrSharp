namespace Tyr.Vision.Data;

public class BallFilterInput(
    MergedBall? mergedBall = null,
    DetectedKick? detectedKick = null)
{
    public DetectedKick? DetectedKick { get; init; } = detectedKick;
    public MergedBall? MergedBall { get; init; } = mergedBall;
}