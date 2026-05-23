using TriviUpBackend.Repositories.Users;
using TriviUpBackend.Services.Auth;
using TriviUpBackend.Cuestionarios.Repositories;
using TriviUpBackend.Cuestionarios.Services;
using TriviUpBackend.Common.Storage;
using TriviUpBackend.Game.Repositories;
using TriviUpBackend.Services.Cache;

namespace TriviUpBackend.Infrastructure;

public static class DependencyInjectionConfig
{
    public static IServiceCollection AddRepositoriesAndServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Repositorios
        //services.AddScoped<IFunkoRepository, FunkoRepository>();
        //services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IQuizRepository, QuizRepository>();
        services.AddScoped<IGameHistoryRepository, GameHistoryRepository>();

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
        services.AddScoped<IQuestionImageStorage, QuestionImageStorage>();

        // Cache
        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = configuration.GetConnectionString("Redis")
                ?? configuration["Redis:ConnectionString"]
                ?? "localhost:6379";
            options.InstanceName = "TriviUp:";
        });
        services.AddScoped<ICacheService, RedisCacheService>();

        // Eventos
        //services.AddScoped<IEventPublisher, EventPublisher>();

        return services;
    }
}