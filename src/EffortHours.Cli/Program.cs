using EffortHours.Cli;

using CancellationTokenSource cancellation = new();
Console.CancelKeyPress += HandleCancel;
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
    Console.CancelKeyPress -= HandleCancel;
}

void HandleCancel(object? _, ConsoleCancelEventArgs eventArgs)
{
    if (cancellation.IsCancellationRequested)
    {
        eventArgs.Cancel = false;
        return;
    }

    eventArgs.Cancel = true;
    cancellation.Cancel();
}
