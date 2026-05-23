using TriviUpBackend.Infrastructure;
using Microsoft.Extensions.FileProviders;
using TriviUpBackend.Game.Hubs;
using TriviUpBackend.Game.Services;
using TriviUpBackend.Game.Configuration;

var builder = WebApplication.CreateBuilder(args);

// --- 1. Registro de Servicios (Inyección de Dependencias) ---

// Configuración de Controladores
builder.Services.AddControllersConfiguration();

// OpenAPI / Swagger
builder.Services.AddOpenApi();

// Base de Datos (InMemory según tu DatabaseConfig)
builder.Services.AddDatabase(builder.Configuration);

// Repositorios y Servicios de Aplicación
builder.Services.AddRepositoriesAndServices(builder.Configuration);

// Autenticación y Autorización (JWT)
builder.Services.AddAuthentication(builder.Configuration);

// Configuración de CORS
builder.Services.AddCorsPolicy(builder.Configuration, builder.Environment.IsDevelopment());

// Validaciones personalizadas para BadRequest
builder.Services.AddCustomValidation();

// Storage
builder.Services.AddStorage();

// Game Services (SignalR)
builder.Services.AddSignalR();
builder.Services.AddSingleton<ITurnManager, TurnManager>();
builder.Services.AddSingleton<IGameService, GameService>();
builder.Services.AddSingleton<GameOptions>();


var app = builder.Build();

// --- 2. Configuración del Pipeline de HTTP (Middleware) ---

// Exception Handler Global (Tu extensión)
app.UseGlobalExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// CORS: Debe ir después de Routing y antes de Authentication/Authorization
app.UseRouting();

// Tu extensión de CORS
app.UseCorsPolicy(); 

// Seguridad: El orden es CRÍTICO aquí
app.UseAuthentication();
app.UseAuthorization();

// Sembrado de datos (Tu extensión de DatabaseSeeder)
app.SeedDatabase();

// Mapeo de Controladores
app.MapControllers();

// Game Hub (SignalR)
app.MapHub<GameHub>("/hubs/game");

app.Run();