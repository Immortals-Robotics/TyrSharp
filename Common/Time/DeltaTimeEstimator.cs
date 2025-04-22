using Tyr.Common.Math;

namespace Tyr.Common.Time;

/// <summary>
/// Estimates delta time between consecutive frame IDs using linear regression,
/// normalized against the first sample for numerical stability.
/// </summary>
public sealed class DeltaTimeEstimator
{
    /// <summary>
    /// Number of samples used for estimation history.
    /// </summary>
    public int HistoryCount { get; init; } = 100;

    /// <summary>
    /// Only 1 out of every N frames is added to the regression,
    /// to have a wider window with a small history buffer
    /// </summary>
    public int Stride { get; init; } = 10;

    private readonly LineEstimator _estimator;

    // Offset is used to normalize frame IDs and timestamps to smaller deltas,
    // improving numerical stability for regression. All samples are stored as
    // (frameId - offset.id, timestamp - offset.timestamp). The original values
    // can be reconstructed by adding the offset back to the regression result.
    private (uint id, Timestamp timestamp)? _offset;

    /// <summary>
    /// Estimated delta time per frame, or null if not enough samples.
    /// </summary>
    public DeltaTime? DeltaTime => _estimator.Estimate.HasValue
        ? Common.Time.DeltaTime.FromMilliseconds(_estimator.Estimate.Value.Slope)
        : null;

    public DeltaTimeEstimator()
    {
        _estimator = new LineEstimator(HistoryCount);
    }

    /// <summary>
    /// Estimates the timestamp of a given frame ID using the fitted regression.
    /// Returns null if the estimator is not yet initialized.
    /// </summary>
    public Timestamp? GetEstimatedTimestamp(uint frameId)
    {
        if (!_offset.HasValue || !_estimator.Estimate.HasValue)
            return null;

        var x = frameId - _offset.Value.id;
        var y = _estimator.Estimate.Value.Y(x);

        return _offset.Value.timestamp + Common.Time.DeltaTime.FromMilliseconds(y);
    }

    /// <summary>
    /// Adds a new (frameId, timestamp) sample to the estimator.
    /// Only 1 out of every <see cref="Stride"/> frames is accepted once full.
    /// </summary>
    public void AddSample(uint frameId, Timestamp timestamp)
    {
        if (_estimator.IsFull && frameId % Stride != 0) return;

        _offset ??= (frameId, timestamp);
        var x = frameId - _offset.Value.id;
        var y = (timestamp - _offset.Value.timestamp).Milliseconds;

        _estimator.AddSample(x, y);
    }

    /// <summary>
    /// Resets the estimator and clears all internal state.
    /// </summary>
    public void Reset()
    {
        _estimator.Reset();
        _offset = null;
    }
}