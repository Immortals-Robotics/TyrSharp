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

    private readonly UdpServer _udp = new();
    private Span<byte> Buffer => _udp.GetBuffer();
    private int _buffIdx;

    // TODO: probably not needed anymore
    private int _startup = 5;

    public bool Send(CommandsWrapper commands)
    {
        if (!Enabled) return false;

        if (_startup > 0)
        {
            _startup--;
            return false;
        }

        foreach (var command in commands.Commands)
        {
            AppendCommand(command);
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
        if (command.Halted)
        {
            AppendHalt(command.VisionId);
            return;
        }

        AppendByte((byte)command.VisionId);

        AppendByte(15); // length=15
        AppendByte(12); // Command to move with new protocol

        // TODO: verify this magic number. motion is in mm/s
        AppendHalf((Half)(command.Motion.X / 20.0f));
        AppendHalf((Half)(command.Motion.Y / 20.0f));

        AppendHalf((Half)command.TargetAngle.Deg);
        AppendHalf((Half)command.CurrentAngle.Deg);

        if (command.Shoot > 0)
        {
            var raw = Math.Clamp(command.Shoot, 0.0f, 6500.0f) / 1000.0f;
            var calibrated = ShootCalibration.GetCalibratedPower(
                raw, ShootCalibration.ShootType.Shoot, command.VisionId);

            AppendByte((byte)calibrated);
            AppendByte(0x00);
        }
        else if (command.Chip > 0)
        {
            var raw = Math.Clamp(command.Chip, 0.0f, 150.0f);
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
    }

    private void AppendHalt(int robotId)
    {
        AppendByte((byte)robotId);
        AppendByte(0x0A); // length=10
        AppendByte(0x06); // Command to HALT
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
        AppendByte(0x0A);
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