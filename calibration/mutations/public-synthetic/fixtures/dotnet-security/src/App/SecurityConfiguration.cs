namespace EffortHoursSynthetic;

[Authorize]
public sealed class ProtectedStatusEndpoint;

public static class SecurityConfiguration
{
    public static void Configure(dynamic services, dynamic app)
    {
        services.AddAuthentication();
        services.AddAuthorization();
        app.UseAuthentication();
        app.UseAuthorization();
    }
}
