namespace Tyr.Vision.Data;

public sealed class KickEstimatorsUpdateResult(DirectKickFitResult? bestFitResult)
{
    public DirectKickFitResult? BestFitResult { get; init; } = bestFitResult;
}
