using EffortHours.Cli;

using CancellationTokenSource cancellation = new();
ConsoleCancelEventHandler cancelHandler = (_, eventArgs) =>
{
    if (cancellation.IsCancellationRequested)
    {
        eventArgs.Cancel = false;
        return;
    }

    eventArgs.Cancel = true;
    cancellation.Cancel();
};

Console.CancelKeyPress += cancelHandler;
try
{
    return await new EffortHoursApplication().RunAsync(
        args,
        Console.Out,
        Console.Error,
        cancellation.Token);
}
finally
{
    Console.CancelKeyPress -= cancelHandler;
}
