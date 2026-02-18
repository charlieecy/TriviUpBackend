using CSharpFunctionalExtensions;
using TriviUpBackend.DTO.User;
using TriviUpBackend.Errors;

namespace TriviUpBackend.Services.Auth;

public interface IAuthService
{
    public interface IAuthService
    {
    
        Task<Result<AuthResponseDto, AuthError>> SignUpAsync(RegisterDto dto);
    

  
        Task<Result<AuthResponseDto, AuthError>> SignInAsync(LoginDto dto);
    }
}