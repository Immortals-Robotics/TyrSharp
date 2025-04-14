using Tyr.Common.Config;
using Tyr.Vision;

namespace Tyr.Cli;

internal static class Program
{
    private static void Main(string[] args)
    {
        ConfigRegistry.Initialize();

        var configurables = ConfigRegistry.Configurables;
        Logger.ZLogInformation($"Found {configurables.Count} configurables");
        foreach (var configurable in configurables.Values)
        {
            Logger.ZLogInformation($"{configurable.TypeName} @ {configurable.Namespace}");
            configurable.SetDefaults();
        }

        var configPath = args[0];
        ConfigStorage.Initialize(configPath);

        ConfigStorage.Load();
        ConfigStorage.Save();

        using var sslVisionPublisher = new SslVisionDataPublisher();
        using var gcPublisher = new Referee.GcDataPublisher();

        using var referee = new Referee.Runner();
        using var vision = new Vision.Vision();

        Thread.Sleep(Timeout.Infinite);
    }
}