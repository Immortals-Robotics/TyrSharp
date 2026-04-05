namespace Tyr.Vision.Data;

public sealed class KickEstimatorsUpdateResult(KickFitResult? bestFitResult)
{
    public KickFitResult? BestFitResult { get; init; } = bestFitResult;
}
