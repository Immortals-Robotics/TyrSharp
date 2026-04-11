using System.Buffers.Text;
using ProtoBuf;
using MemoryPack;

namespace Tyr.Common.Data.Ssl;

[ProtoContract]
[MemoryPackable]
public partial record struct RobotId : ISpanFormattable, IUtf8SpanFormattable
{
    [ProtoMember(1)] public uint? Id { get; set; }
    [ProtoMember(2)] public TeamColor? Team { get; set; }

    public bool TryFormat(Span<char> destination, out int charsWritten,
        ReadOnlySpan<char> format, IFormatProvider? provider)
    {
        charsWritten = 0;

        ReadOnlySpan<char> team = Team switch
        {
            TeamColor.Yellow => "Yellow",
            TeamColor.Blue => "Blue",
            TeamColor.Unknown => "Unknown",
            _ => "?"
        };

        if (!team.TryCopyTo(destination))
            return false;
        charsWritten += team.Length;

        if (charsWritten >= destination.Length)
            return false;
        destination[charsWritten++] = '/';

        if (Id is { } id)
        {
            if (!id.TryFormat(destination[charsWritten..], out var idChars, default, provider))
                return false;
            charsWritten += idChars;
        }
        else
        {
            if (charsWritten >= destination.Length)
                return false;
            destination[charsWritten++] = '?';
        }

        return true;
    }

    public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten,
        ReadOnlySpan<char> format, IFormatProvider? provider)
    {
        bytesWritten = 0;

        // Team names are ASCII — cast each char to byte directly
        ReadOnlySpan<char> team = Team switch
        {
            TeamColor.Yellow => "Yellow",
            TeamColor.Blue => "Blue",
            TeamColor.Unknown => "Unknown",
            _ => "?"
        };

        if (team.Length >= utf8Destination.Length)
            return false;
        foreach (var c in team)
            utf8Destination[bytesWritten++] = (byte)c;

        if (bytesWritten >= utf8Destination.Length)
            return false;
        utf8Destination[bytesWritten++] = (byte)'/';

        if (Id is { } id)
        {
            if (!Utf8Formatter.TryFormat(id, utf8Destination[bytesWritten..], out var idBytes))
                return false;
            bytesWritten += idBytes;
        }
        else
        {
            if (bytesWritten >= utf8Destination.Length)
                return false;
            utf8Destination[bytesWritten++] = (byte)'?';
        }

        return true;
    }

    public string ToString(string? format, IFormatProvider? formatProvider) => ToString();

    public override string ToString()
    {
        Span<char> buf = stackalloc char[18]; // "Unknown" (7) + '/' (1) + uint max (10)
        TryFormat(buf, out var written, default, null);
        return new string(buf[..written]);
    }
}
