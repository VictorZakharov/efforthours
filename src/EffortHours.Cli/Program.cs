using EffortHours.Cli;

return await new EffortHoursApplication().RunAsync(
    args,
    Console.Out,
    Console.Error,
    CancellationToken.None);
