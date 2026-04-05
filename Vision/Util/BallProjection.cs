using System.Numerics;
using Tyr.Common.Data.Ssl.Vision.Geometry;
using Tyr.Common.Extensions;

namespace Tyr.Vision.Util;

public static class BallProjection
{
    private static readonly Vector3 MissingCalibrationFallback = new(0f, 0f, 2000f);

    public static Vector2 ProjectToGround(Vector3 ballPosition, Vector3 cameraPosition)
    {
        var scale = cameraPosition.Z / (cameraPosition.Z - ballPosition.Z);
        return cameraPosition.Xy() + (ballPosition - cameraPosition).Xy() * scale;
    }

    public static Vector3 GetCameraPosition(
        IReadOnlyDictionary<uint, CameraCalibration> calibrations,
        uint cameraId)
    {
        return calibrations.TryGetValue(cameraId, out var calibration)
            ? calibration.DerivedCameraWorld
            : MissingCalibrationFallback;
    }
}
