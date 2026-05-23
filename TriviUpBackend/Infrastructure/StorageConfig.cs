using TriviUpBackend.Common.Storage;
using Microsoft.Extensions.Logging;

namespace TriviUpBackend.Infrastructure;

public static class StorageConfig
{
    /// <summary>
    /// Configura el servicio de almacenamiento de archivos locales.
    /// </summary>
    public static IServiceCollection AddStorage(this IServiceCollection services)
    {
        return services.AddScoped<IStorage, Storage>();
    }
}
