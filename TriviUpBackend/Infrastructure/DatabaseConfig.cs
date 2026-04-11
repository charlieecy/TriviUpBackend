using Microsoft.EntityFrameworkCore;
using TriviUpBackend.Database;

namespace TriviUpBackend.Infrastructure;

public static class DatabaseConfig
{
    public static IServiceCollection AddDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<Context>(options =>
        {
            var provider = configuration["Database:Provider"] ?? "InMemory";
            
            if (provider == "PostgreSQL")
            {
                var connectionString = configuration.GetConnectionString("DefaultConnection");
                options.UseNpgsql(connectionString);
            }
            else
            {
                options.UseInMemoryDatabase("TriviUpDb");
            }
        });

        return services;
    }
}