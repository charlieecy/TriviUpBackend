using TriviUpBackend.Database;
using TriviUpBackend.Game.Models;
using Microsoft.EntityFrameworkCore;

namespace TriviUpBackend.Game.Repositories;

public interface IGameHistoryRepository
{
    Task AddAsync(GameHistory gameHistory);
    Task<List<GameHistory>> GetByUserIdAsync(long userId);
    Task<GameHistory?> GetByIdAsync(long gameId);
}

public class GameHistoryRepository(Context context) : IGameHistoryRepository
{
    private readonly Context _context = context;

    public async Task AddAsync(GameHistory gameHistory)
    {
        await _context.GameHistories.AddAsync(gameHistory);
        await _context.SaveChangesAsync();
    }

    public async Task<List<GameHistory>> GetByUserIdAsync(long userId)
    {
        return await _context.GameHistories
            .Where(g => g.OwnerId == userId || g.PlayerResultsJson.Contains($"\"UserId\":{userId}"))
            .OrderByDescending(g => g.EndedAt)
            .ToListAsync();
    }

    public async Task<GameHistory?> GetByIdAsync(long gameId)
    {
        return await _context.GameHistories
            .FirstOrDefaultAsync(g => g.GameId == gameId);
    }
}
