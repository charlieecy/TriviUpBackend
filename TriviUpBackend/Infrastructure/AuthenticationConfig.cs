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
            });

        services.AddAuthorizationBuilder()
            .AddPolicy("RequireAdminRole", policy => policy.RequireRole(UserRoles.ADMIN))
            .AddPolicy("RequireUserRole", policy => policy.RequireRole(UserRoles.USER, UserRoles.ADMIN));

        return services;
    }
}