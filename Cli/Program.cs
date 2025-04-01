namespace Tyr.Cli;

internal static class Program
{
    private static void Main(string[] args)
    {
        var configPath = args[0];
        var config = Common.Config.Config.Load(configPath);
    }
}