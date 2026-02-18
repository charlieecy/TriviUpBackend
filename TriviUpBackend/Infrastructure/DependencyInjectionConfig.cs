using TriviUpBackend.Repositories.Users;
using TriviUpBackend.Services.Auth;

namespace TriviUpBackend.Infrastructure;

public static class DependencyInjectionConfig
{
    public static IServiceCollection AddRepositoriesAndServices(this IServiceCollection services)
    {
        // Repositorios
        //services.AddScoped<IFunkoRepository, FunkoRepository>();
        //services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        
        // Servicios
        //services.AddScoped<IFunkoService, FunkoService>();
        //services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IJwtService, JwtService>();
        services.AddScoped<IJwtTokenExtractor, JwtTokenExtractor>();
        
        // Storage
        //services.AddScoped<IFunkoStorage, FunkoStorageService>();
        
        // Eventos
        //services.AddScoped<IEventPublisher, EventPublisher>();

        return services;
    }
}