using TriviUpBackend.Models.Auth;

namespace TriviUpBackend.Services.Auth;

public interface IJwtService
{
    string GenerateToken(User user);

    string? ValidateToken(string token);
}