using FairbillSynthetic;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
WebApplication app = builder.Build();
StatusFormatter formatter = new();

app.MapGet("/status", () => new { status = formatter.Format(healthy: true) });
app.Run();
