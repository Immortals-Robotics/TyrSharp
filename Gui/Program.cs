using Config = Tyr.Common.Config;

using var projectConfigs = new Config.Storage(args[0], Config.StorageType.Project);
using var userConfigs = new Config.Storage(
    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Tyr", "user.toml"),
    Config.StorageType.User);

using var debugDbDumper = new Tyr.Common.Debug.Db.DebugDbDumper();
using var playbackSessions = new Tyr.Gui.Data.PlaybackSessionManager(debugDbDumper.Db, Path.Combine(debugDbDumper.SessionRootDirectory, "sessions"));
using var runner = new Tyr.Gui.Runner(playbackSessions);

using var sender = new Tyr.Sender.Runner();
using var referee = new Tyr.Referee.Runner();
using var vision = new Tyr.Vision.Runner();
using var soccer = new Tyr.Soccer.Runner();

using var sslVisionPublisher = new Tyr.Vision.SslVisionDataPublisher();
using var gcPublisher = new Tyr.Referee.GcDataPublisher();
using var robotStatusPublisher = new Tyr.Sender.RobotStatusPublisher(); 

runner.Start();
