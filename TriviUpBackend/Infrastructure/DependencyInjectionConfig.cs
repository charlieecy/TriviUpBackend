using TriviUpBackend.Repositories.Users;
using TriviUpBackend.Services.Auth;
using TriviUpBackend.Cuestionarios.Repositories;
using TriviUpBackend.Cuestionarios.Services;
using TriviUpBackend.Common.Storage;

namespace TriviUpBackend.Infrastructure;

public static class DependencyInjectionConfig
{
    public static IServiceCollection AddRepositoriesAndServices(this IServiceCollection services)
    {
        // Repositorios
        //services.AddScoped<IFunkoRepository, FunkoRepository>();
        //services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IQuizRepository, QuizRepository>();
        
        // Servicios
        //services.AddScoped<IFunkoService, FunkoService>();
        //services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IJwtService, JwtService>();
        services.AddScoped<IJwtTokenExtractor, JwtTokenExtractor>();
        services.AddScoped<IQuizService, QuizService>();
        
        // Storage
        services.AddScoped<IStorage, Storage>();
        services.AddScoped<IProfilePhotoStorage, ProfilePhotoStorage>();
        
        // Eventos
        //services.AddScoped<IEventPublisher, EventPublisher>();

        return services;
    }
}