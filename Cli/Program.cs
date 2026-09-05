using System.Runtime.InteropServices;
using Tyr.Cli;

HarnessOptions options;
try
{
    options = HarnessOptions.Parse(args);
}
catch (Exception ex) when (ex is ArgumentException or FormatException)
{
    Log.ZLogError($"{ex.Message}\n{HarnessOptions.Usage}");
    return 64;
}

if (options.ShowHelp)
{
    Log.ZLogInformation($"{HarnessOptions.Usage}");
    return 0;
}

using var cancellation = new CancellationTokenSource();
using var sigint = PosixSignalRegistration.Create(PosixSignal.SIGINT, context =>
{
    context.Cancel = true;
    cancellation.Cancel();
});
using var sigterm = PosixSignalRegistration.Create(PosixSignal.SIGTERM, context =>
{
    context.Cancel = true;
    cancellation.Cancel();
});

return Harness.Run(options, cancellation.Token);
