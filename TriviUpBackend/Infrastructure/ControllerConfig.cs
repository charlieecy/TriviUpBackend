namespace TriviUpBackend.Infrastructure;

public static class ControllerConfig
{
    public static IServiceCollection AddControllersConfiguration(this IServiceCollection services)
    {
        // Registramos nuestros controladores para que la API pueda gestionar las rutas
        services.AddControllers();
        
        services.AddControllers(options =>
        {
            options.SuppressAsyncSuffixInActionNames = false;
        });

        return services;
    }
}