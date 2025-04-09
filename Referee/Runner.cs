namespace Tyr.Referee;

public class Runner : Common.Module.Runner
{
    protected override string Name => "Referee";

    private readonly Referee _referee = new();

    protected override void OnStart()
    {
    }

    protected override void OnStop()
    {
    }

    protected override void Tick()
    {
        var gcReceived = _referee.ReceiveGc();
        var visionReceived = _referee.ReceiveVision();

        if (!gcReceived && !visionReceived)
        {
            Thread.Sleep(1);
            return;
        }

        _referee.Process();
    }
}