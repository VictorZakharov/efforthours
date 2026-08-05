using Fairbill.Cli;

return await new FairbillApplication().RunAsync(
    args,
    Console.Out,
    Console.Error,
    CancellationToken.None);
