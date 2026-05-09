using Microsoft.AspNetCore.SignalR;
using TriviUpBackend.Game.Services;
using TriviUpBackend.Game.Models;

namespace TriviUpBackend.Game.Hubs;

public class GameHub : Hub
{
    private readonly IGameService _gameService;
    private readonly ILogger<GameHub> _logger;

    public GameHub(IGameService gameService, ILogger<GameHub> logger)
    {
        _gameService = gameService;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (exception != null)
        {
            _logger.LogWarning(exception, "Client disconnected with error: {ConnectionId}", Context.ConnectionId);
        }
        else
        {
            _logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);
        }

        await _gameService.HandleDisconnectionAsync(Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    // TODO: Implementar métodos del hub
    // - CreateGame(quizId)
    // - JoinGame(roomCode)
    // - LeaveGame(roomCode)
    // - StartGame(roomCode)
    // - SubmitAnswer(roomCode, questionId, answerIndex)
}
