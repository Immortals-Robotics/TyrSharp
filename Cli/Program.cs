using Tyr.Common.Config;

var configPath = args[0];
ConfigStorage.Initialize(configPath);

using var sslVisionPublisher = new Tyr.Vision.SslVisionDataPublisher();
using var gcPublisher = new Tyr.Referee.GcDataPublisher();

using var referee = new Tyr.Referee.Runner();
using var vision = new Tyr.Vision.Vision();

Thread.Sleep(Timeout.Infinite);