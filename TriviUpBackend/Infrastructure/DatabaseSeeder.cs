using TriviUpBackend.Database;

namespace TriviUpBackend.Infrastructure;

public static class DatabaseSeeder
{
    public static void SeedDatabase(this WebApplication app)
    {
        using (var scope = app.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<Context>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

            logger.LogInformation("Estamos inicializando la Base de Datos...");
            // Ejecutamos EnsureCreated para crear la base de datos y cargar nuestros datos iniciales
            context.Database.EnsureCreated();
            logger.LogInformation("Hemos terminado de preparar la Base de Datos.");
        }
    }
}