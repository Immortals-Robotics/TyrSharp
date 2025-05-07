global using Detection = Tyr.Common.Data.Ssl.Vision.Detection;
global using RawBall = Tyr.Vision.Data.RawDetection<Tyr.Common.Data.Ssl.Vision.Detection.Ball>;
global using RawRobot = Tyr.Vision.Data.RawDetection<Tyr.Common.Data.Ssl.Vision.Detection.Robot>;
using Tyr.SourceGen;

namespace Tyr.Vision;

[GenerateGlobals]
internal static class Module;