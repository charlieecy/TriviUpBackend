using System.Collections.Concurrent;
using TriviUpBackend.Game.Configuration;
using TriviUpBackend.Game.Models;
using Microsoft.AspNetCore.SignalR;
using TriviUpBackend.Game.Hubs;
using CSharpFunctionalExtensions;
using TriviUpBackend.Game.DTOs;
using TriviUpBackend.Cuestionarios.Repositories;
using Pregunta = TriviUpBackend.Cuestionarios.Entities.Pregunta;
using Respuesta = TriviUpBackend.Cuestionarios.Entities.Respuesta;

namespace TriviUpBackend.Game.Services;

public class GameService : IGameService
{
    private readonly ConcurrentDictionary<string, GameRoom> _rooms = new();
    private readonly ConcurrentDictionary<string, long> _connectionToUser = new();
    private readonly ConcurrentDictionary<long, string> _userToRoom = new();
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _turnTimers = new();
    private readonly ITurnManager _turnManager;
    private readonly GameOptions _options;
    private readonly ILogger<GameService> _logger;
    private readonly ConcurrentDictionary<string, TurnManager> _roomTurnManagers = new();
    private readonly IServiceScopeFactory _scopeFactory;

    public GameService(
        ITurnManager turnManager,
        GameOptions options,
        ILogger<GameService> logger,
        IServiceScopeFactory scopeFactory)
    {
        _turnManager = turnManager;
        _options = options;
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    public async Task<string> CreateGameAsync(long quizId, long ownerId, string username)
    {
        _logger.LogInformation("Creating game room for quiz {QuizId} by owner {OwnerId} ({Username})", quizId, ownerId, username);

        var roomCode = GenerateRoomCode();

        var room = new GameRoom
        {
            RoomCode = roomCode,
            QuizId = quizId,
            OwnerId = ownerId,
            State = GameState.Waiting,
            Players = new List<Player>(),
            TurnOrder = new Queue<long>()
        };

        // Owner entra como primer jugador
        var ownerPlayer = new Player
        {
            UserId = ownerId,
            Username = username,
            IsConnected = true,
            IsOwner = true,
            Score = 0,
            CorrectAnswers = 0,
            WrongAnswers = 0
        };

        room.Players.Add(ownerPlayer);

        _rooms[roomCode] = room;
        _userToRoom[ownerId] = roomCode;

        _logger.LogInformation("Game room created: {RoomCode} for quiz {QuizId} by owner {OwnerId} ({Username})", roomCode, quizId, ownerId, username);

        return await Task.FromResult(roomCode);
    }

    public Task<Result<GameRoom>> JoinGameAsync(string roomCode, long userId, string username, string connectionId)
    {
        if (!_rooms.TryGetValue(roomCode, out var room))
        {
            _logger.LogWarning("Join attempt to non-existent room: {RoomCode}", roomCode);
            return Task.FromResult(Result.Failure<GameRoom>("Room not found."));
        }

        if (room.State != GameState.Waiting)
        {
            _logger.LogWarning("Join attempt to non-waiting room: {RoomCode}", roomCode);
            return Task.FromResult(Result.Failure<GameRoom>("Game has already started."));
        }

        if (room.Players.Count >= _options.MaxPlayersPerRoom)
        {
            _logger.LogWarning("Join attempt to full room: {RoomCode}", roomCode);
            return Task.FromResult(Result.Failure<GameRoom>("Room is full."));
        }

        var existingPlayer = room.Players.FirstOrDefault(p => p.UserId == userId);
        if (existingPlayer != null)
        {
            existingPlayer.IsConnected = true;
            existingPlayer.ConnectionId = connectionId;
        }
        else
        {
            var player = new Player
            {
                UserId = userId,
                Username = username,
                ConnectionId = connectionId,
                IsConnected = true,
                IsOwner = false,
                Score = 0,
                CorrectAnswers = 0,
                WrongAnswers = 0
            };
            room.Players.Add(player);
        }

        _connectionToUser[connectionId] = userId;
        _userToRoom[userId] = roomCode;

        _logger.LogInformation("User {UserId} ({Username}) joined room {RoomCode}", userId, username, roomCode);

        return Task.FromResult(Result.Success(room));
    }

    public async Task LeaveGameAsync(string roomCode, long userId)
    {
        if (!_rooms.TryGetValue(roomCode, out var room))
            return;

        var player = room.Players.FirstOrDefault(p => p.UserId == userId);
        if (player != null)
        {
            player.IsConnected = false;

            // Si el owner abandona, transferir propiedad
            if (player.IsOwner && room.State == GameState.Waiting)
            {
                var newOwner = room.Players.FirstOrDefault(p => p.UserId != userId && p.IsConnected);
                if (newOwner != null)
                {
                    newOwner.IsOwner = true;
                    room.OwnerId = newOwner.UserId;
                    player.IsOwner = false;
                    _logger.LogInformation("Owner transferred to {NewOwnerId} in room {RoomCode}", newOwner.UserId, roomCode);
                }
            }
        }

        _userToRoom.TryRemove(userId, out _);

        _logger.LogInformation("User {UserId} left room {RoomCode}", userId, roomCode);
    }

    public async Task<GameRoom?> StartGameAsync(string roomCode, long userId)
    {
        if (!_rooms.TryGetValue(roomCode, out var room))
            return null;

        if (room.OwnerId != userId)
        {
            _logger.LogWarning("Non-owner {UserId} attempted to start room {RoomCode}", userId, roomCode);
            return null;
        }

        // Si la partida ya ha empezado (o esta empezando), devuelve la sala
        if (room.State == GameState.Playing || room.State == GameState.Finished || room.State == GameState.Starting)
        {
            _logger.LogInformation("Game in room {RoomCode} already started, finishing, or starting", roomCode);
            return room;
        }

        if (room.State != GameState.Waiting)
        {
            _logger.LogWarning("Start attempt on non-waiting room: {RoomCode}", roomCode);
            return null;
        }

        var activePlayers = room.Players.Where(p => p.IsConnected).ToList();
        if (activePlayers.Count < _options.MinPlayersToStart)
        {
            _logger.LogWarning("Not enough players to start room {RoomCode}", roomCode);
            return null;
        }

        room.State = GameState.Starting;

        // Cargar preguntas de la base de datos
        List<Pregunta> questions;
        using (var scope = _scopeFactory.CreateScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<IQuizRepository>();
            questions = await repository.GetQuestionsWithAnswersAsync(room.QuizId);
        }
        if (questions == null || questions.Count == 0)
        {
            _logger.LogWarning("No questions found for quiz {QuizId} in room {RoomCode}", room.QuizId, roomCode);
            return null;
        }

        // Barajar preguntas aleatoriamente
        var random = new Random();
        questions = questions.OrderBy(_ => random.Next()).ToList();

        // Para cada pregunta ordenar respuestas aleatoriamente
        foreach (var question in questions)
        {
            question.Respuestas = question.Respuestas.OrderBy(_ => random.Next()).ToList();
        }

        room.Questions = questions;

        // Inicializar cola de turnos (solo jugadores activos, excluyendo owner que observa)
        var playerIds = activePlayers.Where(p => !p.IsOwner).Select(p => p.UserId).ToList();
        if (playerIds.Count == 0)
        {
            // Solo el owner, no hay turnos posibles
            _logger.LogWarning("No players available for turns in room {RoomCode}", roomCode);
            return null;
        }

        // Crea un TurnManager por sala
        var roomTurnManager = new TurnManager();
        roomTurnManager.InitializeQueue(playerIds);
        _roomTurnManagers[roomCode] = roomTurnManager;

        room.TurnOrder = new Queue<long>(new[] { roomTurnManager.GetCurrentPlayer()!.Value });

        room.State = GameState.Playing;
        room.StartedAt = DateTime.UtcNow;
        room.CurrentQuestionIndex = 0;

        _logger.LogInformation("Game started in room {RoomCode} with {QuestionCount} questions", roomCode, questions.Count);

        // Broadcast TurnStarted para el primer turno
        await BroadcastTurnStartedAsync(roomCode);

        return room;
    }

    public async Task<TurnResultDto?> SubmitAnswerAsync(string roomCode, long userId, long questionId, int answerIndex, int timeRemaining)
    {
        if (!_rooms.TryGetValue(roomCode, out var room))
            return null;

        // obtener TurnManager 
        if (!_roomTurnManagers.TryGetValue(roomCode, out var turnManager))
            return null;

        var player = room.Players.FirstOrDefault(p => p.UserId == userId);
        if (player == null) return null;

        var currentPlayerId = turnManager.GetCurrentPlayer();
        if (currentPlayerId != userId)
        {
            _logger.LogWarning("User {UserId} attempted to answer but it's not their turn in room {RoomCode}", userId, roomCode);
            return null;
        }

        var question = room.Questions.FirstOrDefault(q => q.Id == questionId);
        if (question == null) return null;

        // Cancelar temporizador de turno
        CancelTurnTimer(roomCode);

        // Debug logging para validacion de respuesta
        _logger.LogDebug("[SubmitAnswer] Question {QuestionId}, Player {PlayerId}", questionId, userId);
        _logger.LogDebug("[SubmitAnswer] Answers received: {Answers}", 
            string.Join(", ", question.Respuestas.Select((a, i) => $"[{i}]=\"{a.Texto}\"(EsCorrecta={a.EsCorrecta})")));
        
        // Validar respuesta
        var correctAnswer = question.Respuestas.FirstOrDefault(r => r.EsCorrecta);
        int correctAnswerIndex = correctAnswer != null ? question.Respuestas.IndexOf(correctAnswer) : -1;
        
        _logger.LogDebug("[SubmitAnswer] Correct answer index: {CorrectAnswerIndex}, Correct answer text: {CorrectAnswerText}", 
            correctAnswerIndex, correctAnswer?.Texto ?? "NULL");
        _logger.LogDebug("[SubmitAnswer] Player selected index: {SelectedIndex}", answerIndex);
        
        var selectedAnswer = question.Respuestas.ElementAtOrDefault(answerIndex);
        var isCorrect = correctAnswer != null && selectedAnswer != null && 
                        string.Equals(correctAnswer.Texto, selectedAnswer.Texto, StringComparison.OrdinalIgnoreCase);
        
        _logger.LogDebug("[SubmitAnswer] Selected answer text: {SelectedAnswerText}, IsCorrect: {IsCorrect}", 
            selectedAnswer?.Texto ?? "NULL", isCorrect);

        // Caalcular puntos
        int pointsEarned = 0;
        if (isCorrect)
        {
            pointsEarned = _options.BasePoints + (timeRemaining * _options.TimeBonusMultiplier);
            pointsEarned = Math.Min(pointsEarned, _options.BasePoints + _options.MaxTimeBonus);
            player.Score += pointsEarned;
            player.CorrectAnswers++;
        }
        else
        {
            player.WrongAnswers++;
        }

        _logger.LogInformation("Answer submitted by {UserId} in room {RoomCode}: question={QuestionId}, isCorrect={IsCorrect}, points={Points}",
            userId, roomCode, questionId, isCorrect, pointsEarned);
        
        _logger.LogDebug("[SubmitAnswer] Final result - Player {PlayerId}: IsCorrect={IsCorrect}, PointsEarned={Points}, TotalScore={TotalScore}",
            userId, isCorrect, pointsEarned, player.Score);

        // Crear TurnResultDto
        var turnResult = new TurnResultDto(
            userId,
            isCorrect,
            correctAnswerIndex,
            pointsEarned,
            player.Score
        );

        // Broadcast TurnResult
        await BroadcastTurnResultAsync(roomCode, turnResult);

        // Avanzar al siguiente turno
        await AdvanceToNextTurnAsync(roomCode);

        return turnResult;
    }

    private async Task AdvanceToNextTurnAsync(string roomCode)
    {
        if (!_rooms.TryGetValue(roomCode, out var room))
            return;

        if (!_roomTurnManagers.TryGetValue(roomCode, out var turnManager))
            return;

        // Comprobar si la partida ha terminado
        room.CurrentQuestionIndex++;

        if (room.CurrentQuestionIndex >= room.Questions.Count)
        {
            // Game over - enviar GameFinished
            await EndGameAsync(roomCode);
            return;
        }

        // Mover al siguiente jugador
        var nextPlayerId = turnManager.GetNextPlayer();
        if (nextPlayerId == null)
        {
            // No hay mas jugadores - game over
            await EndGameAsync(roomCode);
            return;
        }

        // Iniciar temporizador de turnos para el siguiente jugador
        StartTurnTimer(roomCode, _options.QuestionTimeLimit);

        // Broadcast TurnStarted para el nuevo turno
        await BroadcastTurnStartedAsync(roomCode);
    }

    private async Task HandleTurnTimeoutAsync(string roomCode)
    {
        if (!_rooms.TryGetValue(roomCode, out var room))
            return;

        if (!_roomTurnManagers.TryGetValue(roomCode, out var turnManager))
            return;

        var currentPlayerId = turnManager.GetCurrentPlayer();
        if (currentPlayerId == null)
            return;

        var player = room.Players.FirstOrDefault(p => p.UserId == currentPlayerId);
        if (player == null)
            return;

        // Marcar como respuesta incorrecta
        player.WrongAnswers++;

        _logger.LogInformation("Turn timeout for user {UserId} in room {RoomCode}", currentPlayerId, roomCode);

        // Obtener pregunta actual para encontrar el indice de respuesta correcto
        var question = room.Questions[room.CurrentQuestionIndex];
        var correctAnswer = question.Respuestas.FirstOrDefault(r => r.EsCorrecta);
        int correctAnswerIndex = correctAnswer != null ? question.Respuestas.IndexOf(correctAnswer) : -1;

        // Crear TurnResultDto para timeout
        var turnResult = new TurnResultDto(
            currentPlayerId.Value,
            false,
            correctAnswerIndex,
            0,
            player.Score
        );

        // Crear TurnTimeoutDto
        await BroadcastTurnTimeoutAsync(roomCode, turnResult);

        // Avanzar al siguiente turno
        await AdvanceToNextTurnAsync(roomCode);
    }

    private async Task EndGameAsync(string roomCode)
    {
        if (!_rooms.TryGetValue(roomCode, out var room))
            return;

        room.State = GameState.Finished;
        room.EndedAt = DateTime.UtcNow;

        // Cancelar cualquier temporizador pendiente
        CancelTurnTimer(roomCode);

        _logger.LogInformation("Game ended in room {RoomCode}", roomCode);

        // Broadcast GameFinished
        using var scope = _scopeFactory.CreateScope();
        var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<GameHub>>();

        var playerResults = room.Players
            .OrderByDescending(p => p.Score)
            .Select((p, index) => new PlayerResultDto(
                p.UserId,
                p.Username,
                index + 1,
                p.Score,
                p.CorrectAnswers,
                p.WrongAnswers,
                p.CorrectAnswers + p.WrongAnswers > 0
                    ? (int)Math.Round((double)p.CorrectAnswers / (p.CorrectAnswers + p.WrongAnswers) * 100)
                    : 0
            ))
            .ToList();

        var gameResult = new GameResultDto(
            roomCode,
            string.Empty,
            playerResults,
            room.Questions.Count,
            room.EndedAt!.Value - room.StartedAt!.Value
        );

        await hubContext.Clients.Group(roomCode).SendAsync("GameFinished", gameResult);

        _roomTurnManagers.TryRemove(roomCode, out _);
    }

    private void StartTurnTimer(string roomCode, int timeLimitSeconds)
    {
        var cts = new CancellationTokenSource();
        _turnTimers[roomCode] = cts;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(timeLimitSeconds), cts.Token);
                // Temporizador caducado - manejar timeout
                await HandleTurnTimeoutAsync(roomCode);
            }
            catch (TaskCanceledException)
            {
                // Turno fue respondido a tiempo, temporizador cancelado
            }
        });
    }

    private void CancelTurnTimer(string roomCode)
    {
        if (_turnTimers.TryRemove(roomCode, out var cts))
        {
            cts.Cancel();
        }
    }

    private async Task BroadcastTurnStartedAsync(string roomCode)
    {
        if (!_rooms.TryGetValue(roomCode, out var room))
            return;

        if (!_roomTurnManagers.TryGetValue(roomCode, out var turnManager))
            return;

        var currentPlayerId = turnManager.GetCurrentPlayer();
        if (currentPlayerId == null)
            return;

        var question = room.Questions[room.CurrentQuestionIndex];
        var questionDto = new QuestionDto(
            question.Id,
            question.Enunciado,
            question.Respuestas.Select(r => r.Texto).ToList(),
            question.ImagenUrl
        );

        var turnStartedDto = new TurnStartedDto(
            currentPlayerId.Value,
            false, 
            questionDto,
            _options.QuestionTimeLimit
        );

        using var scope = _scopeFactory.CreateScope();
        var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<GameHub>>();
        await hubContext.Clients.Group(roomCode).SendAsync("TurnStarted", turnStartedDto);
        _logger.LogDebug("Broadcast TurnStarted for player {PlayerId} in room {RoomCode}", currentPlayerId, roomCode);
    }

    private async Task BroadcastTurnResultAsync(string roomCode, TurnResultDto turnResult)
    {
        using var scope = _scopeFactory.CreateScope();
        var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<GameHub>>();
        await hubContext.Clients.Group(roomCode).SendAsync("TurnResult", turnResult);
        _logger.LogDebug("Broadcast TurnResult for player {PlayerId} in room {RoomCode}", turnResult.PlayerId, roomCode);
    }

    private async Task BroadcastTurnTimeoutAsync(string roomCode, TurnResultDto turnResult)
    {
        using var scope = _scopeFactory.CreateScope();
        var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<GameHub>>();
        await hubContext.Clients.Group(roomCode).SendAsync("TurnTimeout", turnResult);
        _logger.LogDebug("Broadcast TurnTimeout for player {PlayerId} in room {RoomCode}", turnResult.PlayerId, roomCode);
    }

    public async Task HandleDisconnectionAsync(string connectionId)
    {
        _logger.LogInformation("Handling disconnection for connection {ConnectionId}", connectionId);

        if (!_connectionToUser.TryRemove(connectionId, out var userId))
        {
            _logger.LogWarning("Connection {ConnectionId} not found in connection mapping", connectionId);
            return;
        }

        _logger.LogInformation("Connection {ConnectionId} mapped to user {UserId}", connectionId, userId);

        if (_userToRoom.TryGetValue(userId, out var roomCode))
        {
            _logger.LogInformation("User {UserId} disconnected from room {RoomCode}", userId, roomCode);
            await LeaveGameAsync(roomCode, userId);
        }
    }

    public Task<string?> GetRoomCodeByConnectionAsync(string connectionId)
    {
        if (_connectionToUser.TryGetValue(connectionId, out var userId))
        {
            if (_userToRoom.TryGetValue(userId, out var roomCode))
            {
                return Task.FromResult<string?>(roomCode);
            }
        }
        return Task.FromResult<string?>(null);
    }

    private string GenerateRoomCode()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var random = new Random();
        string code;

        do
        {
            code = new string(Enumerable.Range(0, 6)
                .Select(_ => chars[random.Next(chars.Length)])
                .ToArray());
        } while (_rooms.ContainsKey(code));

        return code;
    }
}
