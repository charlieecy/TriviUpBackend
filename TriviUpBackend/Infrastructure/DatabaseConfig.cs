using Microsoft.EntityFrameworkCore;
using TriviUpBackend.Database;

namespace TriviUpBackend.Infrastructure;

public static class DatabaseConfig
{
    public static IServiceCollection AddDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<Context>(options =>
            options.UseInMemoryDatabase("TriviUpDb"));

        return services;
    }
}