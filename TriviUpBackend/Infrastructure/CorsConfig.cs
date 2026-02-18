namespace TriviUpBackend.Infrastructure;

public static class CorsConfig
{
    public static IServiceCollection AddCorsPolicy(this IServiceCollection services, IConfiguration configuration, bool isDevelopment)
    {

        return services.AddCors(options =>
        {
            if (isDevelopment)
            {
                options.AddPolicy("AllowAll", policy =>
                {
                    policy.AllowAnyOrigin()
                        .AllowAnyMethod()
                        .AllowAnyHeader();
                });
            }
            else
            {
                var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                                     ?? throw new InvalidOperationException("Cors:AllowedOrigins no configurado");

                options.AddPolicy("ProductionPolicy", policy =>
                {
                    policy.WithOrigins(allowedOrigins)
                        .AllowAnyMethod()
                        .AllowAnyHeader()
                        .AllowCredentials();
                });
            }
        });
    }
}