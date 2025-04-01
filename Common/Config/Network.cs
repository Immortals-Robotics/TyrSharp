namespace Tyr.Common.Config;

using Tyr.Common.Network;

public class Network
{
    public const int MaxUdpPacketSize = 16 * 1024;

    public bool UseSimulatedVision { get; set; } = false;
    public bool UseInternalReferee { get; set; } = false;

    public Address VisionAddress { get; set; } = new() { Ip = "224.5.23.2", Port = 10006 };
    public Address TrackerAddress { get; set; } = new() { Ip = "224.5.23.2", Port = 10010 };

    public Address RefereeAddress { get; set; } = new() { Ip = "224.5.23.1", Port = 10003 };

    public Address StrategyAddress { get; set; } = new() { Ip = "224.5.23.3", Port = 60006 };

    public Address SenderAddress { get; set; } = new() { Ip = "224.5.92.5", Port = 60005 };
    public Address GrSimAddress  { get; set; } = new() { Ip = "127.0.0.1", Port = 20011 };

    public Address ControlSimulationAddress      { get; set; } = new() { Ip = "127.0.0.1", Port = 10300 };
    public Address BlueRobotSimulationAddress   { get; set; } = new() { Ip = "127.0.0.1", Port = 10301 };
    public Address YellowRobotSimulationAddress { get; set; } = new() { Ip = "127.0.0.1", Port = 10302 };


    // NNG URLs
    public string RawWorldStateUrl { get; set; } = "inproc://raw_world_state";
    public string WorldStateUrl { get; set; } = "inproc://world_state";
    public string DebugUrl { get; set; } = "inproc://debug";
    public string RefereeStateUrl { get; set; } = "inproc://referee_state";
    public string SoccerStateUrl { get; set; } = "inproc://soccer_state";
    public string CommandsUrl { get; set; } = "inproc://commands";

    // DB names
    public string RawWorldStateDb { get; set; } = "raw_world_state";
    public string WorldStateDb { get; set; } = "world_state";
    public string DebugDb { get; set; } = "debug";
    public string RefereeDb { get; set; } = "referee";
    public string SoccerDb { get; set; } = "soccer";
}