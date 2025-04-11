namespace Tyr.Referee;

public class Runner : Common.Module.AsyncRunner
{
    private readonly Referee _referee = new();

    protected override async Task Tick(CancellationToken token)
    {
        await Task.WhenAny(_referee.ReceiveGc(token), _referee.ReceiveVision(token));

        _referee.Process();
    }
}