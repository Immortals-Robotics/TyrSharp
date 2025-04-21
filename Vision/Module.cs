global using DetectionFrame = Tyr.Common.Data.Ssl.Vision.Detection.Frame;
global using DetectionBall = Tyr.Common.Data.Ssl.Vision.Detection.Ball;
global using DetectionRobot = Tyr.Common.Data.Ssl.Vision.Detection.Robot;
global using RawBall = Tyr.Vision.RawDetection<Tyr.Common.Data.Ssl.Vision.Detection.Ball>;
global using RawRobot = Tyr.Vision.RawDetection<Tyr.Common.Data.Ssl.Vision.Detection.Robot>;
global using FilteredFrame = Tyr.Common.Data.Ssl.Vision.Tracker.Frame;
global using FilteredBall = Tyr.Common.Data.Ssl.Vision.Tracker.Ball;
global using FilteredRobot = Tyr.Common.Data.Ssl.Vision.Tracker.Robot;
global using TeamColor = Tyr.Common.Data.TeamColor;
global using RobotId = Tyr.Common.Data.Ssl.RobotId;
global using FieldSize = Tyr.Common.Data.Ssl.Vision.Geometry.FieldSize;
global using CameraCalibration = Tyr.Common.Data.Ssl.Vision.Geometry.CameraCalibration;

using Tyr.SourceGen;

namespace Tyr.Vision;

[GenerateGlobals]
internal static class Module;