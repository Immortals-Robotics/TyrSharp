using Tyr.Common.Config;
using Tyr.Common.Data;
using Tyr.Common.Data.Ssl.Simulation;
using Tyr.Common.Network;
using Tyr.Common.Sender.Data;

namespace Tyr.Sender;

[Configurable]
public sealed partial class Simulator : IDisposable
{
    [ConfigEntry] private static bool Enabled { get; set; } = false;
    [ConfigEntry] private static Address BlueAddress { get; set; } = new() { Ip = "127.0.0.1", Port = 10301 };
    [ConfigEntry] private static Address YellowAddress { get; set; } = new() { Ip = "127.0.0.1", Port = 10302 };

    private readonly UdpServer _udp = new();

    internal bool Send(CommandsWrapper commands)
    {
        if (!Enabled) return false;

        var control = new RobotControl();

        // TODO: fill control

        var address = commands.Color switch
        {
            TeamColor.Blue => BlueAddress,
            TeamColor.Yellow => YellowAddress,
            _ => throw new ArgumentOutOfRangeException()
        };

        _udp.Send(control, address);
        return true;
    }

    public void Dispose()
    {
        _udp.Dispose();
    }
}