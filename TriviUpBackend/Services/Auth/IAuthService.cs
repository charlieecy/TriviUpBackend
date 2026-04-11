using CSharpFunctionalExtensions;
using TriviUpBackend.DTO.User;
using TriviUpBackend.Errors;

namespace TriviUpBackend.Services.Auth;

public interface IAuthService
{
    public Task<Result<AuthResponseDto, AuthError>>  SignUpAsync(RegisterDto dto);
        
    public Task<Result<AuthResponseDto, AuthError>> SignInAsync(LoginDto dto);

    public Task<Result<AuthResponseDto, AuthError>> GoogleSignInAsync(string googleId, string email, string username);
}