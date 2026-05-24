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
            // Try REDIS_URL first, then construct from individual variables
            var redisUrl = Environment.GetEnvironmentVariable("REDIS_URL");
            if (!string.IsNullOrEmpty(redisUrl))
            {
                options.Configuration = redisUrl;
            }
            else
            {
                // Construct from individual Railway variables
                var host = Environment.GetEnvironmentVariable("REDISHOST") ?? "localhost";
                var port = Environment.GetEnvironmentVariable("REDISPORT") ?? "6379";
                var password = Environment.GetEnvironmentVariable("REDISPASSWORD") ?? "";
                var user = Environment.GetEnvironmentVariable("REDISUSER") ?? "default";
                options.Configuration = $"redis://{user}:{password}@{host}:{port}";
            }
            options.InstanceName = "TriviUp:";
        });
        services.AddScoped<ICacheService, RedisCacheService>();

        // Eventos
        //services.AddScoped<IEventPublisher, EventPublisher>();

        return services;
    }
}