using System.Runtime.CompilerServices;
using Tyr.Common.Debug.Drawing.Drawables;

namespace Tyr.Common.Debug;

public enum DebugDumpEntryKind : byte
{
    LogEntry,
    CircleDraw,
    ArcDraw,
    ArrowDraw,
    LineDraw,
    LineSegmentDraw,
    PathDraw,
    PointDraw,
    RectangleDraw,
    RobotDraw,
    TextDraw,
    TriangleDraw,
    PlotCommand,
    Frame,
    CustomEntry,
}

public readonly record struct DebugDumpEntry
{
    private readonly Logging.Entry _logEntry;
    private readonly Circle _circle;
    private readonly Arc _arc;
    private readonly Arrow _arrow;
    private readonly Drawing.Drawables.Line _line;
    private readonly Drawing.Drawables.LineSegment _lineSegment;
    private readonly Drawing.Drawables.Path _path;
    private readonly Drawing.Drawables.Point _point;
    private readonly Drawing.Drawables.Rectangle _rectangle;
    private readonly Drawing.Drawables.Robot _robot;
    private readonly Drawing.Drawables.Text _text;
    private readonly Drawing.Drawables.Triangle _triangle;
    private readonly Plotting.Command _plotCommand;
    private readonly Frame _frame;
    private readonly object? _customEntry;

    private DebugDumpEntry(Logging.Entry logEntry)
    {
        Kind = DebugDumpEntryKind.LogEntry;
        EntryType = typeof(Logging.Entry);
        _logEntry = logEntry;
        _circle = default;
        _arc = default;
        _arrow = default;
        _line = default;
        _lineSegment = default;
        _path = default;
        _point = default;
        _rectangle = default;
        _robot = default;
        _text = default;
        _triangle = default;
        _plotCommand = default;
        _frame = default;
        _customEntry = null;
    }

    private DebugDumpEntry(Circle circle)
    {
        Kind = DebugDumpEntryKind.CircleDraw;
        EntryType = typeof(Circle);
        _logEntry = default;
        _circle = circle;
        _arc = default;
        _arrow = default;
        _line = default;
        _lineSegment = default;
        _path = default;
        _point = default;
        _rectangle = default;
        _robot = default;
        _text = default;
        _triangle = default;
        _plotCommand = default;
        _frame = default;
        _customEntry = null;
    }

    private DebugDumpEntry(Arc arc)
    {
        Kind = DebugDumpEntryKind.ArcDraw;
        EntryType = typeof(Arc);
        _logEntry = default;
        _circle = default;
        _arc = arc;
        _arrow = default;
        _line = default;
        _lineSegment = default;
        _path = default;
        _point = default;
        _rectangle = default;
        _robot = default;
        _text = default;
        _triangle = default;
        _plotCommand = default;
        _frame = default;
        _customEntry = null;
    }

    private DebugDumpEntry(Arrow arrow)
    {
        Kind = DebugDumpEntryKind.ArrowDraw;
        EntryType = typeof(Arrow);
        _logEntry = default;
        _circle = default;
        _arc = default;
        _arrow = arrow;
        _line = default;
        _lineSegment = default;
        _path = default;
        _point = default;
        _rectangle = default;
        _robot = default;
        _text = default;
        _triangle = default;
        _plotCommand = default;
        _frame = default;
        _customEntry = null;
    }

    private DebugDumpEntry(Drawing.Drawables.Line line)
    {
        Kind = DebugDumpEntryKind.LineDraw;
        EntryType = typeof(Drawing.Drawables.Line);
        _logEntry = default;
        _circle = default;
        _arc = default;
        _arrow = default;
        _line = line;
        _lineSegment = default;
        _path = default;
        _point = default;
        _rectangle = default;
        _robot = default;
        _text = default;
        _triangle = default;
        _plotCommand = default;
        _frame = default;
        _customEntry = null;
    }

    private DebugDumpEntry(Drawing.Drawables.LineSegment lineSegment)
    {
        Kind = DebugDumpEntryKind.LineSegmentDraw;
        EntryType = typeof(Drawing.Drawables.LineSegment);
        _logEntry = default;
        _circle = default;
        _arc = default;
        _arrow = default;
        _line = default;
        _lineSegment = lineSegment;
        _path = default;
        _point = default;
        _rectangle = default;
        _robot = default;
        _text = default;
        _triangle = default;
        _plotCommand = default;
        _frame = default;
        _customEntry = null;
    }

    private DebugDumpEntry(Drawing.Drawables.Path path)
    {
        Kind = DebugDumpEntryKind.PathDraw;
        EntryType = typeof(Drawing.Drawables.Path);
        _logEntry = default;
        _circle = default;
        _arc = default;
        _arrow = default;
        _line = default;
        _lineSegment = default;
        _path = path;
        _point = default;
        _rectangle = default;
        _robot = default;
        _text = default;
        _triangle = default;
        _plotCommand = default;
        _frame = default;
        _customEntry = null;
    }

    private DebugDumpEntry(Drawing.Drawables.Point point)
    {
        Kind = DebugDumpEntryKind.PointDraw;
        EntryType = typeof(Drawing.Drawables.Point);
        _logEntry = default;
        _circle = default;
        _arc = default;
        _arrow = default;
        _line = default;
        _lineSegment = default;
        _path = default;
        _point = point;
        _rectangle = default;
        _robot = default;
        _text = default;
        _triangle = default;
        _plotCommand = default;
        _frame = default;
        _customEntry = null;
    }

    private DebugDumpEntry(Drawing.Drawables.Rectangle rectangle)
    {
        Kind = DebugDumpEntryKind.RectangleDraw;
        EntryType = typeof(Drawing.Drawables.Rectangle);
        _logEntry = default;
        _circle = default;
        _arc = default;
        _arrow = default;
        _line = default;
        _lineSegment = default;
        _path = default;
        _point = default;
        _rectangle = rectangle;
        _robot = default;
        _text = default;
        _triangle = default;
        _plotCommand = default;
        _frame = default;
        _customEntry = null;
    }

    private DebugDumpEntry(Drawing.Drawables.Robot robot)
    {
        Kind = DebugDumpEntryKind.RobotDraw;
        EntryType = typeof(Drawing.Drawables.Robot);
        _logEntry = default;
        _circle = default;
        _arc = default;
        _arrow = default;
        _line = default;
        _lineSegment = default;
        _path = default;
        _point = default;
        _rectangle = default;
        _robot = robot;
        _text = default;
        _triangle = default;
        _plotCommand = default;
        _frame = default;
        _customEntry = null;
    }

    private DebugDumpEntry(Drawing.Drawables.Text text)
    {
        Kind = DebugDumpEntryKind.TextDraw;
        EntryType = typeof(Drawing.Drawables.Text);
        _logEntry = default;
        _circle = default;
        _arc = default;
        _arrow = default;
        _line = default;
        _lineSegment = default;
        _path = default;
        _point = default;
        _rectangle = default;
        _robot = default;
        _text = text;
        _triangle = default;
        _plotCommand = default;
        _frame = default;
        _customEntry = null;
    }

    private DebugDumpEntry(Drawing.Drawables.Triangle triangle)
    {
        Kind = DebugDumpEntryKind.TriangleDraw;
        EntryType = typeof(Drawing.Drawables.Triangle);
        _logEntry = default;
        _circle = default;
        _arc = default;
        _arrow = default;
        _line = default;
        _lineSegment = default;
        _path = default;
        _point = default;
        _rectangle = default;
        _robot = default;
        _text = default;
        _triangle = triangle;
        _plotCommand = default;
        _frame = default;
        _customEntry = null;
    }

    private DebugDumpEntry(Plotting.Command plotCommand)
    {
        Kind = DebugDumpEntryKind.PlotCommand;
        EntryType = typeof(Plotting.Command);
        _logEntry = default;
        _circle = default;
        _arc = default;
        _arrow = default;
        _line = default;
        _lineSegment = default;
        _path = default;
        _point = default;
        _rectangle = default;
        _robot = default;
        _text = default;
        _triangle = default;
        _plotCommand = plotCommand;
        _frame = default;
        _customEntry = null;
    }

    private DebugDumpEntry(Frame frame)
    {
        Kind = DebugDumpEntryKind.Frame;
        EntryType = null;
        _logEntry = default;
        _circle = default;
        _arc = default;
        _arrow = default;
        _line = default;
        _lineSegment = default;
        _path = default;
        _point = default;
        _rectangle = default;
        _robot = default;
        _text = default;
        _triangle = default;
        _plotCommand = default;
        _frame = frame;
        _customEntry = null;
    }

    private DebugDumpEntry(object customEntry, Type entryType)
    {
        Kind = DebugDumpEntryKind.CustomEntry;
        EntryType = entryType;
        _logEntry = default;
        _circle = default;
        _arc = default;
        _arrow = default;
        _line = default;
        _lineSegment = default;
        _path = default;
        _point = default;
        _rectangle = default;
        _robot = default;
        _text = default;
        _triangle = default;
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

        if (typeof(T) == typeof(Circle))
            return new DebugDumpEntry(Unsafe.As<T, Circle>(ref entry));

        if (typeof(T) == typeof(Arc))
            return new DebugDumpEntry(Unsafe.As<T, Arc>(ref entry));

        if (typeof(T) == typeof(Arrow))
            return new DebugDumpEntry(Unsafe.As<T, Arrow>(ref entry));

        if (typeof(T) == typeof(Drawing.Drawables.Line))
            return new DebugDumpEntry(Unsafe.As<T, Drawing.Drawables.Line>(ref entry));

        if (typeof(T) == typeof(Drawing.Drawables.LineSegment))
            return new DebugDumpEntry(Unsafe.As<T, Drawing.Drawables.LineSegment>(ref entry));

        if (typeof(T) == typeof(Drawing.Drawables.Path))
            return new DebugDumpEntry(Unsafe.As<T, Drawing.Drawables.Path>(ref entry));

        if (typeof(T) == typeof(Drawing.Drawables.Point))
            return new DebugDumpEntry(Unsafe.As<T, Drawing.Drawables.Point>(ref entry));

        if (typeof(T) == typeof(Drawing.Drawables.Rectangle))
            return new DebugDumpEntry(Unsafe.As<T, Drawing.Drawables.Rectangle>(ref entry));

        if (typeof(T) == typeof(Drawing.Drawables.Robot))
            return new DebugDumpEntry(Unsafe.As<T, Drawing.Drawables.Robot>(ref entry));

        if (typeof(T) == typeof(Drawing.Drawables.Text))
            return new DebugDumpEntry(Unsafe.As<T, Drawing.Drawables.Text>(ref entry));

        if (typeof(T) == typeof(Drawing.Drawables.Triangle))
            return new DebugDumpEntry(Unsafe.As<T, Drawing.Drawables.Triangle>(ref entry));

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

        if (typeof(T) == typeof(Circle) && Kind == DebugDumpEntryKind.CircleDraw)
        {
            entry = Unsafe.As<Circle, T>(ref Unsafe.AsRef(in _circle));
            return true;
        }

        if (typeof(T) == typeof(Arc) && Kind == DebugDumpEntryKind.ArcDraw)
        {
            entry = Unsafe.As<Arc, T>(ref Unsafe.AsRef(in _arc));
            return true;
        }

        if (typeof(T) == typeof(Arrow) && Kind == DebugDumpEntryKind.ArrowDraw)
        {
            entry = Unsafe.As<Arrow, T>(ref Unsafe.AsRef(in _arrow));
            return true;
        }

        if (typeof(T) == typeof(Drawing.Drawables.Line) && Kind == DebugDumpEntryKind.LineDraw)
        {
            entry = Unsafe.As<Drawing.Drawables.Line, T>(ref Unsafe.AsRef(in _line));
            return true;
        }

        if (typeof(T) == typeof(Drawing.Drawables.LineSegment) && Kind == DebugDumpEntryKind.LineSegmentDraw)
        {
            entry = Unsafe.As<Drawing.Drawables.LineSegment, T>(ref Unsafe.AsRef(in _lineSegment));
            return true;
        }

        if (typeof(T) == typeof(Drawing.Drawables.Path) && Kind == DebugDumpEntryKind.PathDraw)
        {
            entry = Unsafe.As<Drawing.Drawables.Path, T>(ref Unsafe.AsRef(in _path));
            return true;
        }

        if (typeof(T) == typeof(Drawing.Drawables.Point) && Kind == DebugDumpEntryKind.PointDraw)
        {
            entry = Unsafe.As<Drawing.Drawables.Point, T>(ref Unsafe.AsRef(in _point));
            return true;
        }

        if (typeof(T) == typeof(Drawing.Drawables.Rectangle) && Kind == DebugDumpEntryKind.RectangleDraw)
        {
            entry = Unsafe.As<Drawing.Drawables.Rectangle, T>(ref Unsafe.AsRef(in _rectangle));
            return true;
        }

        if (typeof(T) == typeof(Drawing.Drawables.Robot) && Kind == DebugDumpEntryKind.RobotDraw)
        {
            entry = Unsafe.As<Drawing.Drawables.Robot, T>(ref Unsafe.AsRef(in _robot));
            return true;
        }

        if (typeof(T) == typeof(Drawing.Drawables.Text) && Kind == DebugDumpEntryKind.TextDraw)
        {
            entry = Unsafe.As<Drawing.Drawables.Text, T>(ref Unsafe.AsRef(in _text));
            return true;
        }

        if (typeof(T) == typeof(Drawing.Drawables.Triangle) && Kind == DebugDumpEntryKind.TriangleDraw)
        {
            entry = Unsafe.As<Drawing.Drawables.Triangle, T>(ref Unsafe.AsRef(in _triangle));
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
