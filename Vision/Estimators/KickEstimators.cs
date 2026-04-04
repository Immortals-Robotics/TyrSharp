using System.Numerics;
using Tyr.Common.Config;
using Tyr.Common.Extensions;
using Tyr.Common.Time;
using Tyr.Common.Vision.Data;
using Tyr.Vision.Data;
using Tyr.Vision.Estimators.Direct;

namespace Tyr.Vision.Estimators;

[Configurable]
public partial class KickEstimators
{
    [ConfigEntry("Factor by which a new estimator must be better than the last one to become active")]
    private static double EstimatorSwitchHysteresis { get; set; } = 0.2;

    [ConfigEntry("Maximum angle deviation between two detected kicks before a new flat estimator is spawned [deg]")]
    private static double SpawnAngleDeviationDegrees { get; set; } = 20.0;

    [ConfigEntry("Maximum distance between two detected kicks before a new flat estimator is spawned [mm]")]
    private static double SpawnPositionDeviation { get; set; } = 500.0;

    [ConfigEntry("Maximum number of kick events to keep for debug history")]
    private static int KickEventHistorySize { get; set; } = 10;

    [ConfigEntry("Maximum number of filtered ball states to keep for estimator warm-start history")]
    private static int FilteredBallHistorySize { get; set; } = 20;

    private readonly List<DirectKickEstimator> _estimators = [];
    private readonly Queue<DetectedKick> _kickEventHistory = [];
    private readonly Queue<FilteredBall> _filteredBallHistory = [];

    private DirectKickEstimator? _lastBestEstimator;
    private Timestamp _lastKickTimestamp = Timestamp.Zero;

    public IReadOnlyList<DirectKickEstimator> ActiveEstimators => _estimators;
    public IReadOnlyList<DetectedKick> KickEventHistory => _kickEventHistory.ToList();
    public IReadOnlyList<FilteredBall> FilteredBallHistory => _filteredBallHistory.ToList();
    public DirectKickEstimator? LastBestEstimator => _lastBestEstimator;

    public KickEstimatorsUpdateResult Process(
        DetectedKick? detectedKick,
        MergedBall? ball,
        List<FilteredRobot> mergedRobots,
        Timestamp timestamp,
        FilteredBall lastFilteredBall)
    {
        Log.ZLogDebug(
            $"KickEstimators.Process start: ts={timestamp.Seconds:F3}, estimators={_estimators.Count}, detectedKick={(detectedKick != null)}, mergedBall={(ball != null)}, latestRawBall={(ball?.LatestRawBall != null)}");

        EnqueueWithLimit(_filteredBallHistory, lastFilteredBall, FilteredBallHistorySize);

        if (ball?.LatestRawBall is { } latestBall)
        {
            Log.ZLogDebug(
                $"KickEstimators.Process forwarding raw ball to estimators: cam={latestBall.CameraId}, frame={latestBall.FrameNumber}, estimators={_estimators.Count}");
            foreach (var estimator in _estimators)
            {
                estimator.AddCamBall(latestBall);
            }
        }
        else
        {
            Log.ZLogDebug($"KickEstimators.Process no latest raw ball to forward to estimators");
        }

        var finishedEstimators = _estimators
            .Where(estimator => estimator.IsDone(mergedRobots, timestamp))
            .ToList();

        Log.ZLogDebug(
            $"KickEstimators.Process finished estimators: {finishedEstimators.Count} / {_estimators.Count}");

        if ((_lastBestEstimator != null) && finishedEstimators.Contains(_lastBestEstimator))
        {
            // Keep the flat estimator set coherent: once the active one finishes, drop the whole group.
            Log.ZLogDebug($"KickEstimators.Process active estimator finished; clearing estimator group");
            _estimators.Clear();
        }
        else
        {
            if (finishedEstimators.Count > 0)
            {
                Log.ZLogDebug(
                    $"KickEstimators.Process removing finished estimators while keeping active group: {finishedEstimators.Count}");
            }

            _estimators.RemoveAll(estimator => finishedEstimators.Contains(estimator));
        }

        if (detectedKick != null)
        {
            Log.ZLogDebug(
                $"KickEstimators.Process enqueue detected kick: robot={detectedKick.RobotId}, ts={detectedKick.Timestamp.Seconds:F3}, pos={detectedKick.KickPosition}");
            EnqueueWithLimit(_kickEventHistory, detectedKick, KickEventHistorySize);
        }
        else
        {
            Log.ZLogDebug($"KickEstimators.Process no detected kick this tick");
        }

        UpdateEstimators(detectedKick);

        Log.ZLogDebug(
            $"KickEstimators.Process end: estimators={_estimators.Count}, lastBestEstimatorSet={_lastBestEstimator != null}, lastKickTs={_lastKickTimestamp.Seconds:F3}");

        return new KickEstimatorsUpdateResult(
            GetBestKickFitResult(timestamp));
    }

    public void Reset()
    {
        _estimators.Clear();
        _kickEventHistory.Clear();
        _filteredBallHistory.Clear();
        _lastBestEstimator = null;
        _lastKickTimestamp = Timestamp.Zero;
    }

    private void UpdateEstimators(DetectedKick? kickEvent)
    {
        var kickDirection = kickEvent?.GetKickDirection();
        if (!kickDirection.HasValue || (kickEvent == null))
        {
            Log.ZLogDebug(
                $"KickEstimators.UpdateEstimators skipped: kickEvent={(kickEvent != null)}, hasDirection={kickDirection.HasValue}");
            return;
        }

        var flatEstimator = _estimators.FirstOrDefault();
        Log.ZLogDebug(
            $"KickEstimators.UpdateEstimators start: currentEstimators={_estimators.Count}, hasLastBest={_lastBestEstimator != null}, kickTs={kickEvent.Timestamp.Seconds:F3}");

        if (_lastBestEstimator != null)
        {
            var lastFit = _lastBestEstimator.GetFitResult();
            if ((lastFit != null) && ShouldSpawnNewEstimator(lastFit, kickEvent, kickDirection.Value))
            {
                Log.ZLogDebug(
                    $"KickEstimators.UpdateEstimators spawning new estimator because kick deviates from last fit");
                flatEstimator = new DirectKickEstimator(kickEvent, _filteredBallHistory.ToList());
            }
            else
            {
                Log.ZLogDebug(
                    $"KickEstimators.UpdateEstimators reusing current estimator group: lastFitExists={(lastFit != null)}");
            }
        }

        if (flatEstimator == null)
        {
            Log.ZLogDebug($"KickEstimators.UpdateEstimators creating first estimator for kick");
            flatEstimator = new DirectKickEstimator(kickEvent, _filteredBallHistory.ToList());
        }

        _estimators.Clear();
        _estimators.Add(flatEstimator);
        _lastKickTimestamp = kickEvent.Timestamp;

        Log.ZLogDebug(
            $"KickEstimators.UpdateEstimators end: estimators={_estimators.Count}, lastKickTs={_lastKickTimestamp.Seconds:F3}");
    }

    private static bool ShouldSpawnNewEstimator(
        DirectKickFitResult lastFit,
        DetectedKick kickEvent,
        Vector2 kickDirection)
    {
        var bestVelocity = lastFit.KickVelocity.Xy();
        if (bestVelocity == Vector2.Zero)
        {
            return true;
        }

        var normalizedVelocity = Vector2.Normalize(bestVelocity);
        var normalizedKickDirection = Vector2.Normalize(kickDirection);
        var dot = Math.Abs(Vector2.Dot(normalizedVelocity, normalizedKickDirection));
        dot = Math.Clamp(dot, 0f, 1f);

        var angleDeviationDegrees = MathF.Acos(dot) * (180f / MathF.PI);
        var positionDeviation = Vector2.Distance(lastFit.KickPosition, kickEvent.KickPosition);

        return (angleDeviationDegrees > SpawnAngleDeviationDegrees)
               || (positionDeviation > SpawnPositionDeviation);
    }

    private DirectKickFitResult? GetBestKickFitResult(Timestamp timestamp)
    {
        var bestEstimator = _estimators
            .Where(estimator => estimator.GetFitResult() is { AvgDistance: > 0.0 })
            .MinBy(estimator => estimator.GetFitResult()!.AvgDistance);

        Log.ZLogDebug(
            $"KickEstimators.GetBestKickFitResult start: estimators={_estimators.Count}, candidateBest={(bestEstimator != null)}, hasLastBest={_lastBestEstimator != null}");

        if (bestEstimator != null)
        {
            var bestFit = bestEstimator.GetFitResult()!;
            var lastBestFit = _lastBestEstimator?.GetFitResult();
            var noLastBestEstimator = (_lastBestEstimator == null) || !_estimators.Contains(_lastBestEstimator);

            if (noLastBestEstimator
                || (lastBestFit == null)
                || (bestEstimator == _lastBestEstimator)
                || (bestFit.AvgDistance < (lastBestFit.AvgDistance * (1.0 - EstimatorSwitchHysteresis))))
            {
                Log.ZLogDebug(
                    $"KickEstimators.GetBestKickFitResult promoting estimator: avgDistance={bestFit.AvgDistance:F3}");
                _lastBestEstimator = bestEstimator;
            }
        }
        else
        {
            Log.ZLogDebug($"KickEstimators.GetBestKickFitResult no candidate best estimator");
            _lastBestEstimator = null;
        }

        if (_lastBestEstimator == null)
        {
            Log.ZLogDebug($"KickEstimators.GetBestKickFitResult returning null because no last best estimator");
            return null;
        }

        var lastBest = _lastBestEstimator.GetFitResult();
        if (lastBest == null)
        {
            Log.ZLogDebug($"KickEstimators.GetBestKickFitResult returning null because last best has no fit");
            return null;
        }

        if ((timestamp - _lastKickTimestamp).Seconds > 0.5)
        {
            Log.ZLogDebug(
                $"KickEstimators.GetBestKickFitResult pruning worse estimators after 0.5s window: tsSinceKick={(timestamp - _lastKickTimestamp).Seconds:F3}");
            _estimators.RemoveAll(estimator =>
            {
                var fit = estimator.GetFitResult();
                return (fit != null) && (fit.AvgDistance > lastBest.AvgDistance);
            });
        }

        Log.ZLogDebug(
            $"KickEstimators.GetBestKickFitResult returning fit: avgDistance={lastBest.AvgDistance:F3}, solver={lastBest.SolverName}");
        return lastBest;
    }

    private static void EnqueueWithLimit<T>(Queue<T> queue, T item, int limit)
    {
        queue.Enqueue(item);
        while (queue.Count > limit)
        {
            queue.Dequeue();
        }
    }
}