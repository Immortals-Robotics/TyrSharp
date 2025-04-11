using Tyr.Common.Config;
using Tyr.Vision;

namespace Tyr.Cli;

internal static class Program
{
    private static void Main(string[] args)
    {
        var configPath = args[0];
        Configs.Load(configPath);

        using var sslVisionPublisher = new SslVisionDataPublisher();
        using var gcPublisher = new Referee.GcDataPublisher();

        using var referee = new Referee.Referee();

        Thread.Sleep(Timeout.Infinite);
    }
}