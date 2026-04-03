﻿using Tyr.Common.Config;
using Tyr.Common.Data;
using Tyr.Common.Data.Ssl.Vision.Geometry;
using Tyr.Common.Dataflow;
using Tyr.Common.Runner;
using Tyr.Common.Time;
using Vision = Tyr.Common.Vision.Data;
using Referee = Tyr.Common.Referee.Data;

namespace Tyr.Soccer;

[Configurable]
public sealed partial class TeamRunner : IDisposable
{
    [ConfigEntry] private static DeltaTime SleepTime { get; set; } = DeltaTime.FromMilliseconds(1);

    private readonly Subscriber<Referee.State> _refereeSubscriber;
    private readonly Subscriber<Vision.FilteredFrame> _visionSubscriber;
    private readonly Subscriber<FieldSize> _fieldSizeSubscriber;

    private readonly RunnerSync _runner;

    private Referee.State _referee = new();
    private FieldSize _field = FieldSize.DivisionA;

    private readonly TeamColor _color;
    private readonly Ai _ai;

    private readonly Knowledge.Knowledge _knowledge = new();

    public TeamRunner(TeamColor color)
    {
        _color = color;

        _refereeSubscriber = Hub.Referee.Subscribe(Mode.Latest);
        _visionSubscriber = Hub.Vision.Subscribe(Mode.Latest);
        _fieldSizeSubscriber = Hub.FieldSize.Subscribe(Mode.Latest);

        _runner = new RunnerSync(Tick, 0, $"{ModuleName}{color}");
        _runner.SetInit(Init);

        _ai = new Ai();
    }

    public void Start() => _runner.Start();
    public void Stop() => _runner.Stop();

    private bool Init()
    {
        // set a default empty context
        Context.Data.Value = new ContextData()
        {
            Color = _color,
            Ball = default,
            OppRobots = [],
            OwnRobots = [],
            Referee = _referee,
            Field = _field,
            Timer = _runner.Timer,
            Knowledge = _knowledge,
        };

        _ai.Init();

        return true;
    }

    private bool Tick()
    {
        if (!_visionSubscriber.Reader.TryRead(out var vision))
        {
            Thread.Sleep(SleepTime.ToTimeSpan());
            return false;
        }

        if (_refereeSubscriber.Reader.TryRead(out var referee))
            _referee = referee;

        if (_fieldSizeSubscriber.Reader.TryRead(out var field))
            _field = field;

        _ai.UpdateContext(vision, _referee, _field);

        _ai.Process();

        _ai.PublishCommands();

        return true;
    }

    public void Dispose()
    {
        _refereeSubscriber.Dispose();
        _visionSubscriber.Dispose();
        _fieldSizeSubscriber.Dispose();

        _runner.Stop();
    }
}