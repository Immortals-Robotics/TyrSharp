using System.Runtime.CompilerServices;

namespace Tyr.Common.Debug;

public enum DebugDumpEntryKind : byte
{
    LogEntry,
    DrawCommand,
    PlotCommand,
    Frame,
    CustomEntry,
}

public readonly record struct DebugDumpEntry
{
    private readonly Logging.Entry _logEntry;
    private readonly Drawing.Command _drawCommand;
    private readonly Plotting.Command _plotCommand;
    private readonly Frame _frame;
    private readonly object? _customEntry;

    private DebugDumpEntry(Logging.Entry logEntry)
    {
        Kind = DebugDumpEntryKind.LogEntry;
        EntryType = typeof(Logging.Entry);
        _logEntry = logEntry;
        _drawCommand = default;
        _plotCommand = default;
        _frame = default;
        _customEntry = null;
    }

    private DebugDumpEntry(Drawing.Command drawCommand)
    {
        Kind = DebugDumpEntryKind.DrawCommand;
        EntryType = typeof(Drawing.Command);
        _logEntry = default;
        _drawCommand = drawCommand;
        _plotCommand = default;
        _frame = default;
        _customEntry = null;
    }

    private DebugDumpEntry(Plotting.Command plotCommand)
    {
        Kind = DebugDumpEntryKind.PlotCommand;
        EntryType = typeof(Plotting.Command);
        _logEntry = default;
        _drawCommand = default;
        _plotCommand = plotCommand;
        _frame = default;
        _customEntry = null;
    }

    private DebugDumpEntry(Frame frame)
    {
        Kind = DebugDumpEntryKind.Frame;
        EntryType = null;
        _logEntry = default;
        _drawCommand = default;
        _plotCommand = default;
        _frame = frame;
        _customEntry = null;
    }

    private DebugDumpEntry(object customEntry, Type entryType)
    {
        Kind = DebugDumpEntryKind.CustomEntry;
        EntryType = entryType;
        _logEntry = default;
        _drawCommand = default;
        _plotCommand = default;
        _frame = default;
        _customEntry = customEntry;
    }

    public DebugDumpEntryKind Kind { get; }
    public Type? EntryType { get; }

    public static DebugDumpEntry From<T>(T entry) where T : struct, IEntry
    {
        if (typeof(T) == typeof(Logging.Entry))
            return new DebugDumpEntry(Unsafe.As<T, Logging.Entry>(ref entry));

        if (typeof(T) == typeof(Drawing.Command))
            return new DebugDumpEntry(Unsafe.As<T, Drawing.Command>(ref entry));

        if (typeof(T) == typeof(Plotting.Command))
            return new DebugDumpEntry(Unsafe.As<T, Plotting.Command>(ref entry));

        return new DebugDumpEntry(entry, typeof(T));
    }

    public static DebugDumpEntry From(Frame frame)
    {
        return new DebugDumpEntry(frame);
    }

    public bool TryGetEntry<T>(out T entry) where T : struct, IEntry
    {
        if (typeof(T) == typeof(Logging.Entry) && Kind == DebugDumpEntryKind.LogEntry)
        {
            entry = Unsafe.As<Logging.Entry, T>(ref Unsafe.AsRef(in _logEntry));
            return true;
        }

        if (typeof(T) == typeof(Drawing.Command) && Kind == DebugDumpEntryKind.DrawCommand)
        {
            entry = Unsafe.As<Drawing.Command, T>(ref Unsafe.AsRef(in _drawCommand));
            return true;
        }

        if (typeof(T) == typeof(Plotting.Command) && Kind == DebugDumpEntryKind.PlotCommand)
        {
            entry = Unsafe.As<Plotting.Command, T>(ref Unsafe.AsRef(in _plotCommand));
            return true;
        }

        if (Kind == DebugDumpEntryKind.CustomEntry && _customEntry is T typed)
        {
            entry = typed;
            return true;
        }

        entry = default;
        return false;
    }

    public bool TryGetFrame(out Frame frame)
    {
        if (Kind == DebugDumpEntryKind.Frame)
        {
            frame = _frame;
            return true;
        }

        frame = default;
        return false;
    }

    public bool TryGetKnownEntry(out Logging.Entry logEntry)
    {
        if (Kind == DebugDumpEntryKind.LogEntry)
        {
            logEntry = _logEntry;
            return true;
        }

        logEntry = default;
        return false;
    }

    public bool TryGetKnownEntry(out Drawing.Command drawCommand)
    {
        if (Kind == DebugDumpEntryKind.DrawCommand)
        {
            drawCommand = _drawCommand;
            return true;
        }

        drawCommand = default;
        return false;
    }

    public bool TryGetKnownEntry(out Plotting.Command plotCommand)
    {
        if (Kind == DebugDumpEntryKind.PlotCommand)
        {
            plotCommand = _plotCommand;
            return true;
        }

        plotCommand = default;
        return false;
    }

    public bool TryGetCustomEntry(out object entry, out Type entryType)
    {
        if (Kind == DebugDumpEntryKind.CustomEntry && _customEntry is not null && EntryType is not null)
        {
            entry = _customEntry;
            entryType = EntryType;
            return true;
        }

        entry = null!;
        entryType = null!;
        return false;
    }
}
