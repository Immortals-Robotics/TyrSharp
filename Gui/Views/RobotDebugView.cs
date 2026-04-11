using System.Numerics;
using System.Collections.Concurrent;
using Hexa.NET.ImGui;
using Tyr.Common.Config;
using Tyr.Common.Data.Robot;
using Tyr.Common.Dataflow;
using Tyr.Common.Network;
using Tyr.Gui.Backend;
using Color = Tyr.Common.Debug.Drawing.Color;

namespace Tyr.Gui.Views;

[Configurable]
public sealed partial class RobotDebugView : IDisposable
{
    public static readonly string WindowTitle = $"{IconFonts.FontAwesome6.Microchip} Robot Debug";

    [ConfigEntry("Command port for the robot", StorageType.User)]
    private static int CommandPort { get; set; } = 5671;

    private readonly Subscriber<IReadOnlyList<DiscoveredRobot>> _discoverySubscriber;
    private readonly Subscriber<StatusUpdate> _statusSubscriber;
    private readonly ConcurrentDictionary<int, HardwareStatus> _hardwareStatuses = new();
    
    private int _selectedRobotId = -1;
    private readonly Dictionary<int, ZmqSender> _senders = new();
    private readonly Dictionary<int, string> _robotIps = new();

    // Command state
    private float _vx;
    private float _vy;
    private float _currentAngle;
    private float _targetAngle;
    private float _shootPower;
    private float _chipPower;
    private float _dribblerSpeed;
    private float _dribblerForce = 5.0f;
    private bool _halted;
    private bool _continuousSend;

    public RobotDebugView()
    {
        _discoverySubscriber = Hub.DiscoveredRobots.Subscribe(Mode.Latest);
        _statusSubscriber = Hub.RobotStatus.Subscribe(Mode.All);
    }

    public void Draw()
    {
        if (!ImGui.Begin(WindowTitle))
        {
            ImGui.End();
            return;
        }

        UpdateDiscovery();
        UpdateStatuses();

        if (ImGui.BeginChild("robot_list", new Vector2(200, 0), ImGuiChildFlags.None, ImGuiWindowFlags.None))
        {
            ImGui.SeparatorText("Online Robots");
            foreach (var id in _senders.Keys.OrderBy(id => id))
            {
                var isSelected = _selectedRobotId == id;
                if (ImGui.Selectable($"Robot {id}##list", isSelected))
                {
                    _selectedRobotId = id;
                }
            }
            ImGui.EndChild();
        }

        ImGui.SameLine();

        if (ImGui.BeginChild("robot_controls", Vector2.Zero, ImGuiChildFlags.None, ImGuiWindowFlags.None))
        {
            if (_selectedRobotId != -1 && _senders.TryGetValue(_selectedRobotId, out var sender))
            {
                DrawControls(sender);
            }
            else
            {
                ImGui.TextDisabled("Select a robot from the list to send commands.");
            }
            ImGui.EndChild();
        }

        ImGui.End();
    }

    private void UpdateStatuses()
    {
        while (_statusSubscriber.Reader.TryRead(out var update))
        {
            var status = _hardwareStatuses.GetOrAdd(update.RobotId, _ => new HardwareStatus());
            status.Apply(update);
        }
    }

    private void UpdateDiscovery()
    {
        if (!_discoverySubscriber.Reader.TryRead(out var robots))
            return;

        var activeIds = new HashSet<int>();
        foreach (var robot in robots)
        {
            if (!robot.IsOnline) continue;
            activeIds.Add(robot.Id);

            if (!_senders.TryGetValue(robot.Id, out var sender) || _robotIps[robot.Id] != robot.Ip)
            {
                sender?.Dispose();
                
                var address = new Address { Ip = robot.Ip, Port = CommandPort };
                _robotIps[robot.Id] = robot.Ip;
                _senders[robot.Id] = new ZmqSender(address, ZmqMode.Connect);
            }
        }

        // Clean up timed-out robots
        var toRemove = _senders.Keys.Where(id => !activeIds.Contains(id)).ToList();
        foreach (var id in toRemove)
        {
            if (_senders.Remove(id, out var sender))
            {
                sender.Dispose();
                _robotIps.Remove(id);
                if (_selectedRobotId == id) _selectedRobotId = -1;
            }
        }
    }

    private void DrawControls(ZmqSender sender)
    {
        ImGui.SeparatorText($"Controls: Robot {_selectedRobotId} ({_robotIps[_selectedRobotId]})");

        if (_hardwareStatuses.TryGetValue(_selectedRobotId, out var status))
        {
            ImGui.TextColored(Color.Yellow400, $"{IconFonts.FontAwesome6.BatteryFull} {status.Power?.V24Voltage:F2}V");
            ImGui.SameLine();
            ImGui.TextColored(status.IrSensor?.Blocked ?? false ? Color.Orange400 : Color.Zinc500, 
                $"{IconFonts.FontAwesome6.CircleDot} Ball Detected");
            ImGui.SameLine();
            ImGui.TextColored(Color.Zinc400, $"{IconFonts.FontAwesome6.TemperatureHalf} {status.Diag?.ImuTemp:F1}Â°C");
        }

        if (ImGui.Checkbox("HALTED", ref _halted)) { /* State update only */ }
        ImGui.SameLine();
        ImGui.Checkbox("Continuous Send", ref _continuousSend);

        ImGui.Spacing();
        ImGui.SeparatorText("Movement (m/s)");
        ImGui.SliderFloat("VX (Forward)", ref _vx, -4.0f, 4.0f);
        ImGui.SliderFloat("VY (Left)", ref _vy, -4.0f, 4.0f);
        
        if (ImGui.Button("Reset Motion"))
        {
            _vx = 0; _vy = 0;
        }

        ImGui.Spacing();
        ImGui.SeparatorText("Angles (deg)");
        ImGui.SliderFloat("Current Angle", ref _currentAngle, -180.0f, 180.0f);
        ImGui.SliderFloat("Target Angle", ref _targetAngle, -180.0f, 180.0f);

        ImGui.Spacing();
        ImGui.SeparatorText("Actions");
        ImGui.SliderFloat("Shoot Power (0-1)", ref _shootPower, 0.0f, 1.0f);
        ImGui.SameLine();
        if (ImGui.Button("SHOOT"))
        {
            SendCommand(sender, shoot: _shootPower);
        }

        ImGui.SliderFloat("Chip Power (0-1)", ref _chipPower, 0.0f, 1.0f);
        ImGui.SameLine();
        if (ImGui.Button("CHIP"))
        {
            SendCommand(sender, chip: _chipPower);
        }

        ImGui.Spacing();
        ImGui.SliderFloat("Dribbler Speed (m/s)", ref _dribblerSpeed, 0.0f, 10.0f);
        ImGui.SliderFloat("Dribbler Force (N)", ref _dribblerForce, 0.0f, 20.0f);

        ImGui.Spacing();
        ImGui.Separator();
        
        if (_continuousSend)
        {
            SendCommand(sender);
            ImGui.TextColored(Color.Green400, "Sending continuous commands...");
        }
        else
        {
            if (ImGui.Button("SEND ONCE", new Vector2(-1, 40)))
            {
                SendCommand(sender);
            }
        }

        ImGui.PushStyleColor(ImGuiCol.Button, Color.Red800.RGBA);
        if (ImGui.Button("EMERGENCY STOP", new Vector2(-1, 40)))
        {
            _halted = true;
            _vx = 0; _vy = 0;
            _continuousSend = false;
            SendCommand(sender);
        }
        ImGui.PopStyleColor();
    }

    private void SendCommand(ZmqSender sender, float shoot = 0, float chip = 0)
    {
        var robotCmd = new RobotCommand
        {
            Halted = _halted,
            Vx = _vx,
            Vy = _vy,
            CurrentAngle = _currentAngle,
            TargetAngle = _targetAngle,
            Shoot = shoot,
            Chip = chip,
            DribblerSpeed = _dribblerSpeed,
            DribblerForce = _dribblerForce
        };

        sender.Send(CommandType.CommandTypeRobot, robotCmd);
    }

    public void Dispose()
    {
        foreach (var sender in _senders.Values)
            sender.Dispose();
        _senders.Clear();
    }
}
