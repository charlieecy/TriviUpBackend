namespace TriviUpBackend.Infrastructure;

public static class CorsExtension
{
    public static IApplicationBuilder UseCorsPolicy(this IApplicationBuilder app)
    {
        var env = ((WebApplication)app).Environment;

        var policyName = "AllowAll"; // Cambiar luego según queramos

        return app.UseCors(policyName);
    }
}