using CSharpFunctionalExtensions;
using Microsoft.AspNetCore.Mvc;
using TriviUpBackend.DTO.User;
using TriviUpBackend.Errors;
using TriviUpBackend.Services.Auth;

namespace TriviUpBackend.Controllers;

[ApiController]
[Route("[controller]")]
[Produces("application/json")]
public class AuthController(
    IAuthService authService,
    ILogger<AuthController> logger
) : ControllerBase
{

    [HttpPost("signup")]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SignUp([FromBody] RegisterDto dto)
    {
        logger.LogInformation("Signup request received for user: {Username}", dto.Username);

        var resultado = await authService.SignUpAsync(dto);

        return resultado.Match(
            response => CreatedAtAction(nameof(SignUp), response),
            error => error switch
            {
                AuthValidationError validationError => BadRequest(new { message = validationError.Error }),
                AuthConflictError conflictError => Conflict(new { message = conflictError.Error }),
                _ => StatusCode(500, new { message = error.Error })
            }
        );
    }


    [HttpPost("signin")]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> SignIn([FromBody] LoginDto dto)
    {
        logger.LogInformation("Petición de inicio de sesión recibida para usuario: {Username}", dto.Username);

        var resultado = await authService.SignInAsync(dto);

        return resultado.Match(
            response => Ok(response),
            error => error switch
            {
                AuthUnauthorizedError unauthorizedError => Unauthorized(new { message = unauthorizedError.Error }),
                AuthValidationError validationError => BadRequest(new { message = validationError.Error }),
                _ => StatusCode(500, new { message = error.Error })
            }
        );
    }
}