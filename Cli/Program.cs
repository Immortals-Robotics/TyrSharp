using Tyr.Common.Config;
using Tyr.Referee;
using Tyr.Vision;

namespace Tyr.Cli;

internal static class Program
{
    private static void Main(string[] args)
    {
        var configPath = args[0];
        Configs.Load(configPath);

        var sslVisionRunner = new SslVisionRunner();
        sslVisionRunner.Start();

        var gcRunner = new GcRunner();
        gcRunner.Start();

        Thread.Sleep(Timeout.Infinite);
    }
}