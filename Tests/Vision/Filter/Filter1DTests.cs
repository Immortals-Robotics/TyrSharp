using Tyr.Common.Time;
using Tyr.Vision.Filter;

namespace Tyr.Tests.Vision.Filter;

public class Filter1DTests
{
    [Fact]
    public void Initialize_WithPositionOnly_SetsCorrectValues()
    {
        // Arrange
        var initialPosition = 10.0f;
        var covariance = 5.0f;
        var modelError = 1.0f;
        var measurementError = 2.0f;
        var timestamp = Timestamp.FromDateTime(new DateTime(2023, 1, 1));

        // Act
        var filter = new Filter1D(initialPosition, covariance, modelError, measurementError, timestamp);

        // Assert
        Assert.Equal(initialPosition, filter.Position);
        Assert.Equal(0.0f, filter.Velocity);
        Assert.Equal(covariance, filter.PositionErrorCovariance);
        Assert.Equal(covariance, filter.VelocityErrorCovariance);
        Assert.Equal(timestamp, filter.LastTimestamp);
    }

    [Fact]
    public void Initialize_WithPositionAndVelocity_SetsCorrectValues()
    {
        // Arrange
        var initialPosition = 10.0f;
        var initialVelocity = 2.0f;
        var covariance = 5.0f;
        var modelError = 1.0f;
        var measurementError = 2.0f;
        var timestamp = Timestamp.FromDateTime(new DateTime(2023, 1, 1));

        // Act
        var filter = new Filter1D(initialPosition, initialVelocity, covariance, modelError, measurementError,
            timestamp);

        // Assert
        Assert.Equal(initialPosition, filter.Position);
        Assert.Equal(initialVelocity, filter.Velocity);
        Assert.Equal(covariance, filter.PositionErrorCovariance);
        Assert.Equal(covariance, filter.VelocityErrorCovariance);
        Assert.Equal(timestamp, filter.LastTimestamp);
    }

    [Fact]
    public void Predict_UpdatesStateCorrectly()
    {
        // Arrange
        var initialPosition = 10.0f;
        var initialVelocity = 2.0f;
        var covariance = 5.0f;
        var modelError = 1.0f;
        var measurementError = 2.0f;
        var initialTimestamp = Timestamp.FromDateTime(new DateTime(2023, 1, 1, 12, 0, 0));
        var filter = new Filter1D(initialPosition, initialVelocity, covariance, modelError, measurementError,
            initialTimestamp);

        // Act
        var newTimestamp = initialTimestamp + DeltaTime.FromSeconds(2);
        filter.Predict(newTimestamp);

        // Assert
        Assert.Equal(newTimestamp, filter.LastTimestamp);
        // With constant velocity model, position should be initial + velocity*dt
        Assert.Equal(initialPosition + initialVelocity * 2, filter.Position, 0.001);
        // Velocity should remain the same
        Assert.Equal(initialVelocity, filter.Velocity, 0.001);
    }

    [Fact]
    public void Predict_WithNegativeOrZeroTimeDifference_DoesNotUpdate()
    {
        // Arrange
        var initialPosition = 10.0f;
        var initialVelocity = 2.0f;
        var covariance = 5.0f;
        var modelError = 1.0f;
        var measurementError = 2.0f;
        var initialTimestamp = Timestamp.FromDateTime(new DateTime(2023, 1, 1, 12, 0, 0));
        var filter = new Filter1D(initialPosition, initialVelocity, covariance, modelError, measurementError,
            initialTimestamp);

        // Act - same timestamp
        filter.Predict(initialTimestamp);

        // Assert - nothing changed
        Assert.Equal(initialPosition, filter.Position);
        Assert.Equal(initialVelocity, filter.Velocity);
        Assert.Equal(initialTimestamp, filter.LastTimestamp);

        // Act - earlier timestamp
        filter.Predict(initialTimestamp + DeltaTime.FromSeconds(-1));

        // Assert - nothing changed
        Assert.Equal(initialPosition, filter.Position);
        Assert.Equal(initialVelocity, filter.Velocity);
        Assert.Equal(initialTimestamp, filter.LastTimestamp);
    }

    [Fact]
    public void Correct_UpdatesStateBasedOnMeasurement()
    {
        // Arrange
        var initialPosition = 10.0f;
        var initialVelocity = 2.0f;
        var covariance = 5.0f;
        var modelError = 1.0f;
        var measurementError = 2.0f;
        var initialTimestamp = Timestamp.FromDateTime(new DateTime(2023, 1, 1, 12, 0, 0));
        var filter = new Filter1D(initialPosition, initialVelocity, covariance, modelError, measurementError,
            initialTimestamp);

        // First predict forward
        var newTimestamp = initialTimestamp + DeltaTime.FromSeconds(1);
        filter.Predict(newTimestamp);
        var positionAfterPredict = filter.Position;

        // Act - measure at a slightly different position
        var measurement = positionAfterPredict + 0.5f;
        filter.Correct(measurement);

        // Assert
        // Position should move toward measurement
        Assert.NotEqual(positionAfterPredict, filter.Position);
        // The corrected position should be between the predicted and measured positions
        Assert.True(IsBetween(filter.Position, positionAfterPredict, measurement));

        // Innovation should be measurement - predicted position
        Assert.Equal(measurement - positionAfterPredict, filter.PositionInnovation, 0.001);
    }

    [Fact]
    public void GetPositionEstimate_ReturnsCorrectEstimateForFutureTime()
    {
        // Arrange
        var initialPosition = 10.0f;
        var initialVelocity = 2.0f;
        var covariance = 5.0f;
        var modelError = 1.0f;
        var measurementError = 2.0f;
        var initialTimestamp = Timestamp.FromDateTime(new DateTime(2023, 1, 1, 12, 0, 0));
        var filter = new Filter1D(initialPosition, initialVelocity, covariance, modelError, measurementError,
            initialTimestamp);

        // Act
        var futureTimestamp = initialTimestamp + DeltaTime.FromSeconds(2.5);
        var estimatedPosition = filter.GetPosition(futureTimestamp);

        // Assert
        // Position should be initial + velocity*dt
        var expected = initialPosition + initialVelocity * 2.5f;
        Assert.Equal(expected, estimatedPosition, 0.001);
    }

    [Fact]
    public void Uncertainties_ReflectCovarianceMatrix()
    {
        // Arrange
        var initialPosition = 10.0f;
        var initialVelocity = 2.0f;
        var covariance = 5.0f;
        var modelError = 1.0f;
        var measurementError = 2.0f;
        var initialTimestamp = Timestamp.FromDateTime(new DateTime(2023, 1, 1, 12, 0, 0));
        var filter = new Filter1D(initialPosition, initialVelocity, covariance, modelError, measurementError,
            initialTimestamp);

        // Act - perform prediction which should increase uncertainty
        var newTimestamp = initialTimestamp + DeltaTime.FromSeconds(1);
        filter.Predict(newTimestamp);

        // Assert
        // Uncertainties should be the square root of the covariance matrix diagonal elements
        Assert.True(filter.PositionUncertainty > 0);
        Assert.True(filter.VelocityUncertainty > 0);

        // After prediction, position uncertainty should increase
        Assert.True(filter.PositionUncertainty > Math.Sqrt(covariance));
    }

    // Helper to check if a value is between two other values
    private static bool IsBetween(float value, float bound1, float bound2)
    {
        return (value >= bound1 && value <= bound2) || (value >= bound2 && value <= bound1);
    }
}