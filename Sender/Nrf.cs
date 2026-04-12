using System.Buffers.Binary;
using Tyr.Common.Config;
using Tyr.Common.Network;
using Tyr.Common.Sender.Data;

namespace Tyr.Sender;

[Configurable]
public sealed partial class Nrf : ISender // TODO: this is untested, test at the competition
{
    [ConfigEntry] private static bool Enabled { get; set; } = false;

    [ConfigEntry] private static Address Address { get; set; } = new() { Ip = "224.5.92.5", Port = 60005 };

    [ConfigEntry("Scale factor to convert mm/s to NRF protocol units")]
    private static float MotionScaleFactor { get; set; } = 22.0f;

    [ConfigEntry("NRF Move command packet length")]
    private static byte MoveCommandLength { get; set; } = 15;

    [ConfigEntry("NRF Move command identifier")]
    private static byte MoveCommandId { get; set; } = 12;

    [ConfigEntry("Maximum shoot power in NRF protocol units")]
    private static float MaxShootPower { get; set; } = 6500.0f;

    [ConfigEntry("Scale factor for shoot power conversion")]
    private static float ShootPowerScale { get; set; } = 1000.0f;

    [ConfigEntry("Maximum chip power in NRF protocol units")]
    private static float MaxChipPower { get; set; } = 150.0f;

    [ConfigEntry("NRF Halt command packet length")]
    private static byte HaltCommandLength { get; set; } = 10;

    [ConfigEntry("NRF Halt command identifier")]
    private static byte HaltCommandId { get; set; } = 6;

    [ConfigEntry("NRF Demo/Heartbeat packet length")]
    private static byte DemoCommandLength { get; set; } = 10;

    private readonly UdpServer _udp = new();
    private Span<byte> Buffer => _udp.GetBuffer();
    private int _buffIdx;

    public bool Send(CommandsWrapper commands)
    {
        if (!Enabled) return false;

        foreach (var command in commands.Commands)
        {
            if (command.Halted) AppendHalt(command.VisionId);
            else AppendCommand(command);
        }

        AppendDemoData();

        Log.ZLogTrace($"Sending {_buffIdx}  bytes to {Address}");

        var result = _udp.Send(_buffIdx, Address);
        if (!result)
        {
            Log.ZLogError($"Failed to send robot commands");
        }

        _buffIdx = 0;
        return result;
    }

    private void AppendCommand(Command command)
    {
        AppendByte((byte)command.VisionId);

        AppendByte(MoveCommandLength);
        AppendByte(MoveCommandId);

        AppendHalf((Half)(command.Motion.X / MotionScaleFactor));
        AppendHalf((Half)(command.Motion.Y / MotionScaleFactor));

        AppendHalf((Half)command.TargetAngle.Deg);
        AppendHalf((Half)command.CurrentAngle.Deg);

        if (command.Shoot > 0)
        {
            var raw = Math.Clamp(command.Shoot, 0.0f, MaxShootPower) / ShootPowerScale;
            var calibrated = ShootCalibration.GetCalibratedPower(
                raw, ShootCalibration.ShootType.Shoot, command.VisionId);

            AppendByte((byte)calibrated);
            AppendByte(0x00);
        }
        else if (command.Chip > 0)
        {
            var raw = Math.Clamp(command.Chip, 0.0f, MaxChipPower);
            var calibrated = ShootCalibration.GetCalibratedPower(
                raw, ShootCalibration.ShootType.Chip, command.VisionId);

            AppendByte(0x00);
            AppendByte((byte)calibrated);
        }
        else
        {
            AppendByte(0x00);
            AppendByte(0x00);
        }
        
        AppendByte(0x00);
        AppendByte(0x00);
    }

    private void AppendHalt(int robotId)
    {
        AppendByte((byte)robotId);
        AppendByte(HaltCommandLength);
        AppendByte(HaltCommandId);
        AppendByte(0x00);
        AppendByte(0x00);
        AppendByte(0x00);
        AppendByte(0x00);
        AppendByte(0x00);
        AppendByte(0x00);
        AppendByte(0x00);
    }

    // TODO: rename
    private void AppendDemoData()
    {
        AppendByte(25);
        AppendByte(DemoCommandLength);
        AppendByte(0x00);
        AppendByte(0x00);
        AppendByte(0x00);
        AppendByte(0x00);
        AppendByte(0x00);
        AppendByte(0x00);
        AppendByte(0x00);
        AppendByte(0x00);
    }

    private void AppendHalf(Half value)
    {
        var halfBits = BitConverter.HalfToUInt16Bits(value);
        BinaryPrimitives.WriteUInt16LittleEndian(Buffer[_buffIdx..], halfBits);

        _buffIdx += sizeof(ushort);
    }

    private void AppendByte(byte value)
    {
        Buffer[_buffIdx] = value;
        _buffIdx += sizeof(byte);
    }

    public void Dispose()
    {
        _udp.Dispose();
    }
}
