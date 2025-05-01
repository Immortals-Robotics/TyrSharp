using Config = Tyr.Common.Config;

using var projectConfigs = new Config.Storage(args[0], Config.StorageType.Project);
using var userConfigs = new Config.Storage("user.toml", Config.StorageType.User);

using var runner = new Tyr.Gui.Runner();

using var referee = new Tyr.Referee.Runner();
using var vision = new Tyr.Vision.Runner();

using var sslVisionPublisher = new Tyr.Vision.SslVisionDataPublisher();
using var gcPublisher = new Tyr.Referee.GcDataPublisher();

runner.Start();