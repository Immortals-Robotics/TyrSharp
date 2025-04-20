using System.Numerics;
using Tyr.Vision.Filter;

namespace Tyr.Tests.Vision.Filter;

public class Filter2DTests
{
    [Fact]
    public void Initialize_WithPositionOnly_SetsCorrectValues()
    {
        // Arrange
        var initialPosition = new Vector2(10, 20);
        var covariance = 5.0f;
        var modelError = 1.0f;
        var measurementError = 2.0f;
        var timestamp = new DateTime(2023, 1, 1);

        // Act
        var filter = new Filter2D(initialPosition, covariance, modelError, measurementError, timestamp);

        // Assert
        Assert.Equal(initialPosition, filter.Position);
        Assert.Equal(Vector2.Zero, filter.Velocity);
        Assert.Equal(covariance, filter.PositionCovariance);
        Assert.Equal(covariance, filter.VelocityCovariance);
        Assert.Equal(timestamp, filter.LastTimestamp);
    }

    [Fact]
    public void Initialize_WithPositionAndVelocity_SetsCorrectValues()
    {
        // Arrange
        var initialPosition = new Vector2(10, 20);
        var initialVelocity = new Vector2(2, 3);
        var covariance = 5.0f;
        var modelError = 1.0f;
        var measurementError = 2.0f;
        var timestamp = new DateTime(2023, 1, 1);

        // Act
        var filter = new Filter2D(initialPosition, initialVelocity, covariance, modelError, measurementError,
            timestamp);

        // Assert
        Assert.Equal(initialPosition, filter.Position);
        Assert.Equal(initialVelocity, filter.Velocity);
        Assert.Equal(covariance, filter.PositionCovariance);
        Assert.Equal(covariance, filter.VelocityCovariance);
        Assert.Equal(timestamp, filter.LastTimestamp);
    }

    [Fact]
    public void Predict_UpdatesStateCorrectly()
    {
        // Arrange
        var initialPosition = new Vector2(10, 20);
        var initialVelocity = new Vector2(2, 3);
        var covariance = 5.0f;
        var modelError = 1.0f;
        var measurementError = 2.0f;
        var initialTimestamp = new DateTime(2023, 1, 1, 12, 0, 0);
        var filter = new Filter2D(initialPosition, initialVelocity, covariance, modelError, measurementError,
            initialTimestamp);

        // Act
        var newTimestamp = initialTimestamp.AddSeconds(2);
        filter.Predict(newTimestamp);

        // Assert
        Assert.Equal(newTimestamp, filter.LastTimestamp);
        // With constant velocity model, position should be initial + velocity*dt
        Assert.Equal(initialPosition.X + initialVelocity.X * 2, filter.Position.X, 0.001);
        Assert.Equal(initialPosition.Y + initialVelocity.Y * 2, filter.Position.Y, 0.001);
        // Velocity should remain the same
        Assert.Equal(initialVelocity.X, filter.Velocity.X, 0.001);
        Assert.Equal(initialVelocity.Y, filter.Velocity.Y, 0.001);
    }

    [Fact]
    public void Predict_WithNegativeOrZeroTimeDifference_DoesNotUpdate()
    {
        // Arrange
        var initialPosition = new Vector2(10, 20);
        var initialVelocity = new Vector2(2, 3);
        var covariance = 5.0f;
        var modelError = 1.0f;
        var measurementError = 2.0f;
        var initialTimestamp = new DateTime(2023, 1, 1, 12, 0, 0);
        var filter = new Filter2D(initialPosition, initialVelocity, covariance, modelError, measurementError,
            initialTimestamp);

        // Act - same timestamp
        filter.Predict(initialTimestamp);

        // Assert - nothing changed
        Assert.Equal(initialPosition, filter.Position);
        Assert.Equal(initialVelocity, filter.Velocity);
        Assert.Equal(initialTimestamp, filter.LastTimestamp);

        // Act - earlier timestamp
        filter.Predict(initialTimestamp.AddSeconds(-1));

        // Assert - nothing changed
        Assert.Equal(initialPosition, filter.Position);
        Assert.Equal(initialVelocity, filter.Velocity);
        Assert.Equal(initialTimestamp, filter.LastTimestamp);
    }

    [Fact]
    public void Correct_UpdatesStateBasedOnMeasurement()
    {
        // Arrange
        var initialPosition = new Vector2(10, 20);
        var initialVelocity = new Vector2(2, 3);
        var covariance = 5.0f;
        var modelError = 1.0f;
        var measurementError = 2.0f;
        var initialTimestamp = new DateTime(2023, 1, 1, 12, 0, 0);
        var filter = new Filter2D(initialPosition, initialVelocity, covariance, modelError, measurementError,
            initialTimestamp);

        // First predict forward
        var newTimestamp = initialTimestamp.AddSeconds(1);
        filter.Predict(newTimestamp);
        var positionAfterPredict = filter.Position;

        // Act - measure at a slightly different position
        var measurement = new Vector2(positionAfterPredict.X + 0.5f, positionAfterPredict.Y - 0.5f);
        filter.Correct(measurement);

        // Assert
        // Position should move toward measurement
        Assert.NotEqual(positionAfterPredict, filter.Position);
        // The corrected position should be between the predicted and measured positions
        Assert.True(IsBetween(filter.Position.X, positionAfterPredict.X, measurement.X));
        Assert.True(IsBetween(filter.Position.Y, positionAfterPredict.Y, measurement.Y));

        // Innovation should be measurement - predicted position
        Assert.Equal(measurement.X - positionAfterPredict.X, filter.PositionInnovation.X, 0.001);
        Assert.Equal(measurement.Y - positionAfterPredict.Y, filter.PositionInnovation.Y, 0.001);
    }

    [Fact]
    public void GetPositionEstimate_ReturnsCorrectEstimateForFutureTime()
    {
        // Arrange
        var initialPosition = new Vector2(10, 20);
        var initialVelocity = new Vector2(2, 3);
        var covariance = 5.0f;
        var modelError = 1.0f;
        var measurementError = 2.0f;
        var initialTimestamp = new DateTime(2023, 1, 1, 12, 0, 0);
        var filter = new Filter2D(initialPosition, initialVelocity, covariance, modelError, measurementError,
            initialTimestamp);

        // Act
        var futureTimestamp = initialTimestamp.AddSeconds(2.5);
        var estimatedPosition = filter.GetPositionEstimate(futureTimestamp);

        // Assert
        // Position should be initial + velocity*dt
        var expectedX = initialPosition.X + initialVelocity.X * 2.5f;
        var expectedY = initialPosition.Y + initialVelocity.Y * 2.5f;
        Assert.Equal(expectedX, estimatedPosition.X, 0.001);
        Assert.Equal(expectedY, estimatedPosition.Y, 0.001);
    }

    [Fact]
    public void Uncertainties_ReflectCovarianceMatrix()
    {
        // Arrange
        var initialPosition = new Vector2(10, 20);
        var initialVelocity = new Vector2(2, 3);
        var covariance = 5.0f;
        var modelError = 1.0f;
        var measurementError = 2.0f;
        var initialTimestamp = new DateTime(2023, 1, 1, 12, 0, 0);
        var filter = new Filter2D(initialPosition, initialVelocity, covariance, modelError, measurementError,
            initialTimestamp);

        // Act - perform prediction which should increase uncertainty
        var newTimestamp = initialTimestamp.AddSeconds(1);
        filter.Predict(newTimestamp);

        // Assert
        // Uncertainties should be the square root of the covariance matrix diagonal elements
        Assert.True(filter.PositionUncertainty.X > 0);
        Assert.True(filter.PositionUncertainty.Y > 0);
        Assert.True(filter.VelocityUncertainty.X > 0);
        Assert.True(filter.VelocityUncertainty.Y > 0);

        // After prediction, position uncertainty should increase
        Assert.True(filter.PositionUncertainty.X > Math.Sqrt(covariance));
        Assert.True(filter.PositionUncertainty.Y > Math.Sqrt(covariance));
    }

    // Helper to check if a value is between two other values
    private static bool IsBetween(float value, float bound1, float bound2)
    {
        return (value >= bound1 && value <= bound2) || (value >= bound2 && value <= bound1);
    }
}