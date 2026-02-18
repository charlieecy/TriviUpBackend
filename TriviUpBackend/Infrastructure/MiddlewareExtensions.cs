namespace TriviUpBackend.Infrastructure;

public static class MiddlewareExtensions
{
    public static IApplicationBuilder UseGlobalExceptionHandler(this IApplicationBuilder app)
    {
        app.Use(async (context, next) =>
        {
            try
            {
                await next(); // Intentamos pasar la petición al siguiente paso del pipeline
            }
            catch (Exception ex)
            {
                context.Response.StatusCode = 500;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(new { message = "Error inesperado: " + ex.Message });
            }
        });

        return app;
    }
}