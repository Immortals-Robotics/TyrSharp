namespace Tyr.Common.Network;

public record Address : ISpanFormattable, IUtf8SpanFormattable
{
    public required string Ip { get; init; }
    public int Port { get; init; }

    public bool TryFormat(Span<char> destination, out int charsWritten,
        ReadOnlySpan<char> format, IFormatProvider? provider)
    {
        charsWritten = 0;

        if (!Ip.TryCopyTo(destination))
            return false;
        charsWritten += Ip.Length;

        if (charsWritten >= destination.Length)
            return false;
        destination[charsWritten++] = ':';

        if (!Port.TryFormat(destination[charsWritten..], out var portChars, default, provider))
            return false;
        charsWritten += portChars;
        return true;
    }

    public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten,
        ReadOnlySpan<char> format, IFormatProvider? provider)
    {
        bytesWritten = 0;

        // IP addresses are always ASCII — each char maps to one byte
        if (Ip.Length >= utf8Destination.Length)
            return false;
        foreach (var c in Ip)
            utf8Destination[bytesWritten++] = (byte)c;

        utf8Destination[bytesWritten++] = (byte)':';

        if (!Port.TryFormat(utf8Destination[bytesWritten..], out var portBytes, default, provider))
            return false;
        bytesWritten += portBytes;
        return true;
    }

    public string ToString(string? format, IFormatProvider? formatProvider) => ToString();

    public override string ToString()
    {
        Span<char> buf = stackalloc char[Ip.Length + 6]; // ':' + up to 5 port digits
        TryFormat(buf, out var written, default, null);
        return new string(buf[..written]);
    }
}
