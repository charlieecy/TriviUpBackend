using System.Security.Claims;
using CSharpFunctionalExtensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TriviUpBackend.Cuestionarios.DTOs;
using TriviUpBackend.Cuestionarios.Services;
using TriviUpBackend.Errors;

namespace TriviUpBackend.Controllers;

[ApiController]
[Route("api/cuestionarios")]
[Produces("application/json")]
[Authorize]
public class CuestionariosController(
    IQuizService quizService,
    ILogger<CuestionariosController> logger
) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(QuizResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Create([FromBody] CreateQuizRequest request)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return Unauthorized(new { message = "Usuario no autenticado" });
        }

        logger.LogInformation("Crear request de cuestionario con userId: {UserId}", userId);

        var result = await quizService.CreateAsync(request, userId.Value);

        return result.Match(
            response => CreatedAtAction(nameof(GetById), new { id = response.Id }, response),
            error => HandleError(error)
        );
    }

    [HttpGet]
    [ProducesResponseType(typeof(QuizListResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        logger.LogInformation("Get all quizzes - Page: {Page}, PageSize: {PageSize}", page, pageSize);

        var result = await quizService.GetAllAsync(page, pageSize);

        return result.Match(
            response => Ok(new
            {
                quizzes = response.Quizzes,
                totalCount = response.TotalCount,
                page,
                pageSize
            }),
            error => HandleError(error)
        );
    }

    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(QuizResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(long id)
    {
        logger.LogInformation("Get quiz by ID: {Id}", id);

        var result = await quizService.GetByIdAsync(id);

        return result.Match(
            response => Ok(response),
            error => HandleError(error)
        );
    }

    [HttpGet("gamecode/{gameCode}")]
    [ProducesResponseType(typeof(QuizResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByGameCode(string gameCode)
    {
        logger.LogInformation("Get quiz by GameCode: {GameCode}", gameCode);

        var result = await quizService.GetByGameCodeAsync(gameCode);

        return result.Match(
            response => Ok(response),
            error => HandleError(error)
        );
    }

    [HttpGet("mis-cuestionarios")]
    [ProducesResponseType(typeof(List<QuizResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMyQuizzes()
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return Unauthorized(new { message = "Usuario no autenticado" });
        }

        logger.LogInformation("Get my quizzes for user: {UserId}", userId);

        var result = await quizService.GetByCreatorIdAsync(userId.Value);

        return result.Match(
            response => Ok(response),
            error => HandleError(error)
        );
    }

    [HttpPut("{id:long}")]
    [ProducesResponseType(typeof(QuizResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateQuizRequest request)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return Unauthorized(new { message = "Usuario no autenticado" });
        }

        logger.LogInformation("Update quiz {Id} by user: {UserId}", id, userId);

        var result = await quizService.UpdateAsync(id, request, userId.Value);

        return result.Match(
            response => Ok(response),
            error => HandleError(error)
        );
    }

    [HttpDelete("{id:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(long id)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return Unauthorized(new { message = "Usuario no autenticado" });
        }

        logger.LogInformation("Delete quiz {Id} by user: {UserId}", id, userId);

        var result = await quizService.DeleteAsync(id, userId.Value);

        if (result.IsSuccess)
        {
            return NoContent();
        }

        return HandleError(result.Error);
    }

    private long? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim != null && long.TryParse(userIdClaim.Value, out var userId))
        {
            return userId;
        }
        return null;
    }

    private IActionResult HandleError(QuizError error)
    {
        return error switch
        {
            QuizNotFoundError notFoundError => NotFound(new { message = notFoundError.Error }),
            QuizValidationError validationError => BadRequest(new { message = validationError.Error }),
            QuizForbiddenError forbiddenError => StatusCode(403, new { message = forbiddenError.Error }),
            _ => StatusCode(500, new { message = error.Error })
        };
    }
}
