using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;
using TriviUpBackend.Game.Services;
using TriviUpBackend.Game.Models;
using TriviUpBackend.Game.DTOs;
using TriviUpBackend.Services.Auth;

namespace TriviUpBackend.Game.Hubs;

public class GameHub : Hub
{
    private readonly IGameService _gameService;
    private readonly IJwtTokenExtractor _jwtTokenExtractor;
    private readonly ILogger<GameHub> _logger;

    public GameHub(
        IGameService gameService,
        IJwtTokenExtractor jwtTokenExtractor,
        ILogger<GameHub> logger)
    {
        _gameService = gameService;
        _jwtTokenExtractor = jwtTokenExtractor;
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

    /// <summary>
    /// Obtiene el userId del usuario autenticado desde Context.User.
    /// SignalR pops el token del query string o header y lo-valida contra el middleware de autenticación.
    /// </summary>
    private long? GetAuthenticatedUserIdFromContext()
    {
        // Context.User está populated si la autenticación funcionó
        var userIdClaim = Context.User?.FindFirst(ClaimTypes.NameIdentifier)
                          ?? Context.User?.FindFirst("sub");

        if (userIdClaim != null && long.TryParse(userIdClaim.Value, out var userId))
        {
            return userId;
        }

        return null;
    }

    /// <summary>
    /// Obtiene el userId autenticado o lanza HubException si no está autenticado.
    /// </summary>
    private long GetAuthenticatedUserId()
    {
        var userId = GetAuthenticatedUserIdFromContext();
        if (userId == null)
        {
            _logger.LogWarning("Unauthorized access attempt to CreateGame. User: {User}", Context.User?.Identity?.Name);
            throw new HubException("Unauthorized");
        }
        return userId.Value;
    }

    /// <summary>
    /// Crea una nueva sala de juego. Requiere usuario autenticado.
    /// </summary>
    public async Task<string> CreateGame(long quizId)
    {
        var userId = GetAuthenticatedUserId();
        _logger.LogInformation("User {UserId} creating game for quiz {QuizId}", userId, quizId);

        // TODO: Obtener username del usuario autenticado (del token o base de datos)
        var username = $"Player_{userId}";

        var roomCode = await _gameService.CreateGameAsync(quizId, userId, username);

        _logger.LogInformation("Game {RoomCode} created by user {UserId}", roomCode, userId);

        // Add caller to the group first so they receive the group broadcast
        await Groups.AddToGroupAsync(Context.ConnectionId, roomCode);
        _logger.LogInformation("User {UserId} added to group {RoomCode}", userId, roomCode);

        // Create owner player DTO
        var ownerPlayer = new PlayerDto(
            userId,
            username,
            0,  // score
            0,  // correctAnswers
            0,  // wrongAnswers
            false,  // isCurrentTurn
            true,   // isOwner
            true    // isConnected
        );

        // Broadcast GameCreated to the entire group (including caller)
        await Clients.Group(roomCode).SendAsync("GameCreated", new
        {
            RoomCode = roomCode,
            QuizId = quizId,
            Players = new List<PlayerDto> { ownerPlayer },
            IsOwner = true,
            MyUserId = userId,
            MyUsername = username
        });

        _logger.LogInformation("Broadcasted GameCreated event to group {RoomCode}", roomCode);

        return roomCode;
    }

    /// <summary>
    /// Une a un jugador anónimo a la sala. El userId se pasa como parámetro.
    /// </summary>
    public async Task JoinGame(string roomCode, long userId, string username)
    {
        _logger.LogInformation("User {UserId} ({Username}) joining game {RoomCode}", userId, username, roomCode);

        var result = await _gameService.JoinGameAsync(roomCode, userId, username, Context.ConnectionId);
        if (result.IsFailure)
        {
            _logger.LogWarning("Failed to join game {RoomCode} for user {UserId} ({Username}): {Error}",
                roomCode, userId, username, result.Error);
            throw new HubException(result.Error);
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, roomCode);
        _logger.LogInformation("User {UserId} ({Username}) joined game {RoomCode} and added to group", userId, username, roomCode);

        var room = result.Value;
        var playerDto = new PlayerDto(
            userId,
            username,
            0,  // score
            0,  // correctAnswers
            0,  // wrongAnswers
            false,  // isCurrentTurn
            false,  // isOwner
            true    // isConnected
        );

        // Send current players list to the new player first
        var playersList = room.Players.Select(p => new PlayerDto(
            p.UserId,
            p.Username,
            p.Score,
            p.CorrectAnswers,
            p.WrongAnswers,
            false,  // isCurrentTurn - not stored on Player model
            p.IsOwner,
            p.IsConnected
        )).ToList();

        await Clients.Caller.SendAsync("PlayersList", playersList);
        _logger.LogInformation("Sent PlayersList with {Count} players to user {UserId} in room {RoomCode}",
            playersList.Count, userId, roomCode);

        // Broadcast PlayerJoined event to all clients in the room (including the new player)
        await Clients.Group(roomCode).SendAsync("PlayerJoined", playerDto);
        _logger.LogInformation("Broadcasted PlayerJoined event for user {UserId} ({Username}) in room {RoomCode}",
            userId, username, roomCode);
    }

    /// <summary>
    /// Elimina a un jugador de la sala. El userId se pasa como parámetro.
    /// </summary>
    public async Task LeaveGame(string roomCode, long userId)
    {
        _logger.LogInformation("User {UserId} leaving game {RoomCode}", userId, roomCode);

        await _gameService.LeaveGameAsync(roomCode, userId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomCode);
    }

    /// <summary>
    /// Inicia el juego. Requiere usuario autenticado y ser el owner de la sala.
    /// </summary>
    public async Task StartGame(string roomCode)
    {
        var userId = GetAuthenticatedUserId();
        _logger.LogInformation("User {UserId} attempting to start game {RoomCode}", userId, roomCode);

        var room = await _gameService.StartGameAsync(roomCode, userId);
        if (room == null)
        {
            throw new HubException("Failed to start game. You may not be the owner or game already started.");
        }

        // Broadcast GameStarted to all clients in the room
        var playerDtos = room.Players.Select(p => new PlayerDto(
            p.UserId,
            p.Username,
            p.Score,
            p.CorrectAnswers,
            p.WrongAnswers,
            false, // isCurrentTurn
            p.IsOwner,
            p.IsConnected
        )).ToList();

        var gameStateDto = new GameStateDto(
            room.RoomCode,
            room.State.ToString(),
            playerDtos,
            room.CurrentQuestionIndex,
            room.Questions?.Count ?? 0
        );

        await Clients.Group(roomCode).SendAsync("GameStarted", gameStateDto);
        _logger.LogInformation("Broadcasted GameStarted event to room {RoomCode}", roomCode);
    }

    /// <summary>
    /// Envía una respuesta. Método anónimo con userId como parámetro.
    /// </summary>
    public async Task<TurnResultDto> SubmitAnswer(string roomCode, long userId, long questionId, int answerIndex, int timeRemaining)
    {
        _logger.LogDebug("User {UserId} submitting answer for question {QuestionId} in room {RoomCode}",
            userId, questionId, roomCode);

        var result = await _gameService.SubmitAnswerAsync(roomCode, userId, questionId, answerIndex, timeRemaining);
        if (result == null)
        {
            throw new HubException("Failed to submit answer. It may not be your turn or the room doesn't exist.");
        }

        return result;
    }
}
