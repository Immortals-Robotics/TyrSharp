using Tyr.Common.Config;
using Tyr.Common.Data;

namespace Tyr.Soccer;

[Configurable]
public sealed partial class Runner : IDisposable
{
    [ConfigEntry] private static bool RunYellow { get; set; } = false;
    [ConfigEntry] private static bool RunBlue { get; set; } = false;

    private readonly TeamRunner _blueRunner;
    private readonly TeamRunner _yellowRunner;

    public Runner()
    {
        _yellowRunner = new TeamRunner(TeamColor.Yellow);
        _blueRunner = new TeamRunner(TeamColor.Blue);

        UpdateRunners();
        Configurable.OnUpdated += _ => UpdateRunners();
    }

    private void UpdateRunners()
    {
        if (RunYellow) _yellowRunner.Start();
        else _yellowRunner.Stop();

        if (RunBlue) _blueRunner.Start();
        else _blueRunner.Stop();
    }

    public void Dispose()
    {
        _blueRunner.Dispose();
        _yellowRunner.Dispose();
    }
}