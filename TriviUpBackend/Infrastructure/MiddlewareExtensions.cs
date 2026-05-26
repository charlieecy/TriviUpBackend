namespace TriviUpBackend.Infrastructure;

public static class MiddlewareExtensions
{
    public static IApplicationBuilder UseGlobalExceptionHandler(this IApplicationBuilder app)
    {
        app.Use(async (context, next) =>
        {
            try
            {
                var logger = context.RequestServices.GetService<ILogger<Program>>();
                logger.LogInformation("[MIDDLEWARE] Request started: {Method} {Path}", context.Request.Method, context.Request.Path);
                await next(); // Intentamos pasar la petición al siguiente paso del pipeline
                logger.LogInformation("[MIDDLEWARE] Request completed: {StatusCode}", context.Response.StatusCode);
            }
            catch (Exception ex)
            {
                var logger = context.RequestServices.GetService<ILogger<Program>>();
                logger.LogError(ex, "[MIDDLEWARE] Unhandled exception: {Message}", ex.Message);
                context.Response.StatusCode = 500;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(new { message = "Error inesperado: " + ex.Message });
            }
        });

        return app;
    }
}