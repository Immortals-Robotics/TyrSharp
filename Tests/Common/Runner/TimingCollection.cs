namespace Tyr.Tests.Common.Runner;

/// The runner tick-rate tests measure wall-clock accuracy, so they can't run
/// while parallel test collections saturate the CPU. This collection runs
/// sequentially after the parallel collections finish.
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class TimingCollection
{
    public const string Name = "Timing";
}
