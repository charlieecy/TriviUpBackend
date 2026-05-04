using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using TriviUpBackend.Models.Auth;

namespace TriviUpBackend.Infrastructure;

public static class AuthenticationConfig
{
  
    public static IServiceCollection AddAuthentication(this IServiceCollection services, IConfiguration configuration)
    {

        var jwtKey = configuration["Jwt:Key"]
                     ?? throw new InvalidOperationException("JWT Key no configurada");
        var jwtIssuer = configuration["Jwt:Issuer"] ?? "TiendaApi";
        var jwtAudience = configuration["Jwt:Audience"] ?? "TiendaApi";
        

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
                    ValidateIssuer = true,
                    ValidIssuer = jwtIssuer,
                    ValidateAudience = true,
                    ValidAudience = jwtAudience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };

                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<JwtBearerEvents>>();
                        logger.LogDebug("JwtBearer - Message received. Path: {Path}", context.Request.Path);
                        return Task.CompletedTask;
                    },
                    OnTokenValidated = context =>
                    {
                        var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<JwtBearerEvents>>();
                        logger.LogInformation("JwtBearer - Token validated. User: {User}", context.Principal?.Identity?.Name);
                        return Task.CompletedTask;
                    },
                    OnAuthenticationFailed = context =>
                    {
                        var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<JwtBearerEvents>>();
                        logger.LogError("JwtBearer - Auth failed: {Error}", context.Exception.Message);
                        return Task.CompletedTask;
                    },
                    OnChallenge = context =>
                    {
                        var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<JwtBearerEvents>>();
                        logger.LogWarning("JwtBearer - Challenge. Error: {Error}, Path: {Path}",
                            context.Error, context.Request.Path);
                        return Task.CompletedTask;
                    }
                };
            });

        services.AddAuthorizationBuilder()
            .AddPolicy("RequireAdminRole", policy => policy.RequireRole(UserRoles.ADMIN))
            .AddPolicy("RequireUserRole", policy => policy.RequireRole(UserRoles.USER, UserRoles.ADMIN));

        return services;
    }
}