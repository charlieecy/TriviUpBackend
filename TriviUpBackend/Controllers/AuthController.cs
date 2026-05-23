using CSharpFunctionalExtensions;
using Google.Apis.Auth;
using Google.Apis.Util;
using Microsoft.AspNetCore.Mvc;
using TriviUpBackend.DTO.User;
using TriviUpBackend.Errors;
using TriviUpBackend.Services.Auth;

namespace TriviUpBackend.Controllers;

[ApiController]
[Route("[controller]")]
[Produces("application/json")]
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
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

    [HttpGet("google")]
    [ProducesResponseType(StatusCodes.Status302Found)]
    public IActionResult GoogleLogin([FromQuery] string? returnUrl = null)
    {
        var clientId = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_ID")
            ?? throw new InvalidOperationException("GOOGLE_CLIENT_ID no configurada");
        var redirectUri = BuildGoogleCallbackUri(returnUrl);

        var state = returnUrl != null ? Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(returnUrl)) : string.Empty;
        var scope = Uri.EscapeDataString("openid email profile");

        var authorizationUrl = $"https://accounts.google.com/o/oauth2/v2/auth?" +
            $"client_id={clientId}&" +
            $"redirect_uri={Uri.EscapeDataString(redirectUri)}&" +
            $"response_type=code&" +
            $"scope={scope}&" +
            $"state={state}&" +
            $"access_type=offline&" +
            $"prompt=select_account";

        logger.LogInformation("Redirecting to Google OAuth: {RedirectUri}", redirectUri);
        return Redirect(authorizationUrl);
    }

    [HttpGet("google/callback")]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GoogleCallback([FromQuery] string code, [FromQuery] string? state)
    {
        if (string.IsNullOrEmpty(code))
        {
            return BadRequest(new { message = "Código de autorización no proporcionado" });
        }

        try
        {
            var clientId = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_ID")
                ?? throw new InvalidOperationException("GOOGLE_CLIENT_ID no configurada");
            var clientSecret = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_SECRET")
                ?? throw new InvalidOperationException("GOOGLE_CLIENT_SECRET no configurada");
            var redirectUri = BuildGoogleCallbackUri(null);

            var tokenResponse = await ExchangeCodeForTokensAsync(code, clientId, clientSecret, redirectUri);
            var payload = await ValidateAndGetPayloadAsync(tokenResponse.IdToken, clientId);

            var username = payload.Name ?? payload.Email.Split('@')[0];
            var result = await authService.GoogleSignInAsync(payload.Subject, payload.Email, username);

            if (result.IsFailure)
            {
                logger.LogError("Error en Google sign-in: {Error}", result.Error.Error);
                return StatusCode(500, new { message = result.Error.Error });
            }

            var response = result.Value;
            var userJson = System.Text.Json.JsonSerializer.Serialize(response.User);
            var userParam = Uri.EscapeDataString(userJson);
            
            logger.LogInformation("Google OAuth success. Redirecting to frontend with user: {UserJson}", userJson);
            
            // Always redirect to frontend callback with token and user
            var frontendCallback = "http://localhost:4200/auth/callback";
            var redirectUrl = $"{frontendCallback}?token={response.Token}&user={userParam}";
            logger.LogInformation("Redirect URL: {RedirectUrl}", redirectUrl);
            return Redirect(redirectUrl);
        }
        catch (Google.Apis.Auth.InvalidJwtException ex)
        {
            logger.LogWarning("Google ID token inválido: {Message}", ex.Message);
            return BadRequest(new { message = "Token de Google inválido" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error en callback de Google OAuth");
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    private string BuildGoogleCallbackUri(string? returnUrl)
    {
        var baseUrl = Request.IsHttps ? "https" : "http";
        var host = Request.Host.Value;
        return $"{baseUrl}://{host}/auth/google/callback";
    }

    private async Task<GoogleTokenResponse> ExchangeCodeForTokensAsync(string code, string clientId, string clientSecret, string redirectUri)
    {
        using var client = new HttpClient();
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["code"] = code,
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["redirect_uri"] = redirectUri,
            ["grant_type"] = "authorization_code"
        });

        var response = await client.PostAsync("https://oauth2.googleapis.com/token", content);
        var json = await response.Content.ReadAsStringAsync();

        logger.LogInformation("Google token response: {Response}", json);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Error intercambiando código por tokens: {json}");
        }

        var tokenResponse = System.Text.Json.JsonSerializer.Deserialize<GoogleTokenResponse>(json);

        if (tokenResponse == null)
        {
            throw new InvalidOperationException("No se pudo deserializar la respuesta de tokens");
        }

        return tokenResponse;
    }

    private async Task<GoogleJsonWebSignature.Payload> ValidateAndGetPayloadAsync(string idToken, string clientId)
    {
        var settings = new GoogleJsonWebSignature.ValidationSettings
        {
            Audience = new[] { clientId },
            Clock = SystemClock.Default
        };

        return await GoogleJsonWebSignature.ValidateAsync(idToken, settings);
    }

    private async Task<GoogleUserInfo> GetGoogleUserInfoAsync(string accessToken)
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        var response = await client.GetAsync("https://www.googleapis.com/oauth2/v2/userinfo");
        var json = await response.Content.ReadAsStringAsync();
        return System.Text.Json.JsonSerializer.Deserialize<GoogleUserInfo>(json, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("No se pudo deserialize userinfo de Google");
    }

    private record GoogleTokenResponse(
        [property: System.Text.Json.Serialization.JsonPropertyName("id_token")] string IdToken,
        [property: System.Text.Json.Serialization.JsonPropertyName("access_token")] string AccessToken
    );
    private record GoogleUserInfo(string Id, string Email, string Name, string Picture);
}