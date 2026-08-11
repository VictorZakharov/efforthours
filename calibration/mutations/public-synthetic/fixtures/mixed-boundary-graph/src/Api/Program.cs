using EffortHoursSynthetic.Domain;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
WebApplication app = builder.Build();
app.MapGet("/health", () => new ServiceStatus("api", true));
app.Run();
