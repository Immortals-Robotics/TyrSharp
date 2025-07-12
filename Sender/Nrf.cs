using Tyr.Common.Config;
using Tyr.Common.Network;
using Tyr.Common.Sender.Data;

namespace Tyr.Sender;

[Configurable]
public sealed partial class Nrf : ISender
{
    [ConfigEntry] private static bool Enabled { get; set; } = false;
    
    [ConfigEntry] private static Address Address { get; set; } = new() { Ip = "224.5.92.5", Port = 60005 };
    
    private readonly UdpServer _udp = new();
    
    public bool Send(CommandsWrapper commands)
    {
        if (!Enabled) return false;

        
        
        return true;
    }
    
    public void Dispose()
    {
        _udp.Dispose();
    }
}