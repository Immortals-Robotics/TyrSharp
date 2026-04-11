using Config = Tyr.Common.Config;

using var projectConfigs = new Config.Storage(args[0], Config.StorageType.Project);
using var userConfigs = new Config.Storage(
    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Tyr", "user.toml"),
    Config.StorageType.User);

using var sslVisionPublisher = new Tyr.Vision.SslVisionDataPublisher();
using var gcPublisher = new Tyr.Referee.GcDataPublisher();
using var robotStatusPublisher = new Tyr.Sender.RobotStatusPublisher();
using var robotDiscoveryPublisher = new Tyr.Sender.RobotDiscoveryPublisher();

using var referee = new Tyr.Referee.Runner();
using var vision = new Tyr.Vision.Runner();

Thread.Sleep(Timeout.Infinite);