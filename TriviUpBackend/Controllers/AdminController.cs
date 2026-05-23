using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using TriviUpBackend.Database;
using TriviUpBackend.Game.DTOs;
using TriviUpBackend.Services.Cache;

namespace TriviUpBackend.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "ADMIN")]
public class AdminController : ControllerBase
{
    private readonly Context _context;
    private readonly ILogger<AdminController> _logger;
    private readonly ICacheService _cacheService;
    private static readonly TimeSpan StatsCacheDuration = TimeSpan.FromMinutes(2);

    public AdminController(Context context, ILogger<AdminController> logger, ICacheService cacheService)
    {
        _context = context;
        _logger = logger;
        _cacheService = cacheService;
    }

    [HttpGet("stats")]
    public async Task<ActionResult<AdminStatsDto>> GetStats()
    {
        const string cacheKey = "stats:admin";
        var cached = await _cacheService.GetAsync<AdminStatsDto>(cacheKey);
        if (cached is not null)
        {
            _logger.LogDebug("Cache hit for admin stats");
            return Ok(cached);
        }

        // Total games played (from GameHistories)
        var totalGames = await _context.GameHistories.CountAsync();

        // Total quizzes
        var totalQuizzes = await _context.Quizzes.CountAsync();

        // Total users
        var totalUsers = await _context.Users.CountAsync(u => !u.IsDeleted);

        // Active users in last 7 days (users who logged in)
        var activeUsersThreshold = DateTime.UtcNow.AddDays(-7);
        var activeUsersLast24h = await _context.Users
            .Where(u => !u.IsDeleted && u.LastLoginAt >= activeUsersThreshold)
            .CountAsync();

        // Quiz with most favorites
        var mostFavoritesQuiz = await _context.Quizzes
            .OrderByDescending(q => q.Likes)
            .Select(q => new QuizWithMostFavoritesDto(q.Id, q.Nombre, q.Likes))
            .FirstOrDefaultAsync();

        // Quiz with most visits
        var mostVisitsQuiz = await _context.Quizzes
            .OrderByDescending(q => q.Visitas)
            .Select(q => new QuizWithMostVisitsDto(q.Id, q.Nombre, q.Visitas))
            .FirstOrDefaultAsync();

        // Games per day (last 7 days)
        var sevenDaysAgo = DateTime.UtcNow.AddDays(-7);
        var gamesPerDay = await _context.GameHistories
            .Where(g => g.EndedAt >= sevenDaysAgo)
            .GroupBy(g => g.EndedAt.Date)
            .Select(g => new DailyGamesDto(g.Key, g.Count()))
            .ToListAsync();

        // Active users per day (last 7 days) - based on LastLoginAt
        var activeUsersPerDay = await _context.Users
            .Where(u => !u.IsDeleted && u.LastLoginAt >= sevenDaysAgo)
            .GroupBy(u => u.LastLoginAt!.Value.Date)
            .Select(g => new ActiveUsersDto(g.Key, g.Count()))
            .ToListAsync();

        var stats = new AdminStatsDto(
            totalGames,
            totalQuizzes,
            totalUsers,
            activeUsersLast24h,
            mostFavoritesQuiz,
            mostVisitsQuiz,
            gamesPerDay,
            activeUsersPerDay
        );

        await _cacheService.SetAsync(cacheKey, stats, StatsCacheDuration);

        return Ok(stats);
    }
}
