using System.Collections.Concurrent;
using TriviUpBackend.Game.Configuration;
using TriviUpBackend.Game.Models;
using Microsoft.AspNetCore.SignalR;
using TriviUpBackend.Game.Hubs;
using CSharpFunctionalExtensions;
using TriviUpBackend.Game.DTOs;
using TriviUpBackend.Game.Repositories;
using TriviUpBackend.Cuestionarios.Repositories;
using Pregunta = TriviUpBackend.Cuestionarios.Entities.Pregunta;
using Respuesta = TriviUpBackend.Cuestionarios.Entities.Respuesta;

namespace TriviUpBackend.Game.Services;

/// <summary>
/// Implementación del servicio de gestión de partidas.
/// Controla la creación, unión, inicio y gestión de salas de juego en tiempo real.
/// </summary>
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

    /// <summary>
    /// Constructor del servicio de juego.
    /// </summary>
    /// <param name="turnManager">Gestor de turnos.</param>
    /// <param name="options">Configuración del juego.</param>
    /// <param name="logger">Logger para mensajes de diagnóstico.</param>
    /// <param name="scopeFactory">Fábrica de scopes para acceso a servicios scoped.</param>
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

    /// <inheritdoc cref="IGameService.CreateGameAsync"/>
    public async Task<string> CreateGameAsync(long quizId, long ownerId, string username)
    {
        _logger.LogInformation("Creating game room for quiz {QuizId} by owner {OwnerId} ({Username})", quizId, ownerId, username);

        // Obtener el título del quiz
        using var scope = _scopeFactory.CreateScope();
        var quizRepository = scope.ServiceProvider.GetRequiredService<IQuizRepository>();
        var quiz = await quizRepository.FindByIdAsync(quizId);
        var quizTitle = quiz?.Nombre ?? "Quiz Desconocido";

        var roomCode = GenerateRoomCode();

        var room = new GameRoom
        {
            RoomCode = roomCode,
            QuizId = quizId,
            QuizTitle = quizTitle,
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

    /// <inheritdoc cref="IGameService.JoinGameAsync"/>
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

    /// <inheritdoc cref="IGameService.LeaveGameAsync"/>
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

    /// <inheritdoc cref="IGameService.StartGameAsync"/>
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

        // Iniciar timer para el primer turno
        room.TurnStartedAt = DateTime.UtcNow;
        StartTurnTimer(roomCode, _options.QuestionTimeLimit);

        // Broadcast TurnStarted para el primer turno
        await BroadcastTurnStartedAsync(roomCode);

        return room;
    }

    /// <inheritdoc cref="IGameService.SubmitAnswerAsync"/>
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

    /// <summary>
    /// Avanza al siguiente turno, avanzando el índice de pregunta o terminando la partida.
    /// </summary>
    /// <param name="roomCode">Código de la sala.</param>
    private async Task AdvanceToNextTurnAsync(string roomCode)
    {
        _logger.LogInformation("[ADVANCE] AdvanceToNextTurnAsync called for room {RoomCode}", roomCode);

        if (!_rooms.TryGetValue(roomCode, out var room))
        {
            _logger.LogWarning("[ADVANCE] Room {RoomCode} not found", roomCode);
            return;
        }

        if (!_roomTurnManagers.TryGetValue(roomCode, out var turnManager))
        {
            _logger.LogWarning("[ADVANCE] TurnManager not found for room {RoomCode}", roomCode);
            return;
        }

        // Comprobar si la partida ha terminado
        room.CurrentQuestionIndex++;
        _logger.LogInformation("[ADVANCE] Incremented question index to {Index} of {Total} for room {RoomCode}",
            room.CurrentQuestionIndex, room.Questions.Count, roomCode);

        if (room.CurrentQuestionIndex >= room.Questions.Count)
        {
            _logger.LogInformation("[ADVANCE] No more questions ({Index} >= {Total}). Ending game for room {RoomCode}",
                room.CurrentQuestionIndex, room.Questions.Count, roomCode);
            // Game over - enviar GameFinished
            await EndGameAsync(roomCode);
            return;
        }

        // Mover al siguiente jugador
        var nextPlayerId = turnManager.GetNextPlayer();
        _logger.LogInformation("[ADVANCE] Next player ID: {PlayerId} for room {RoomCode}", nextPlayerId, roomCode);

        if (nextPlayerId == null)
        {
            _logger.LogInformation("[ADVANCE] No more players. Ending game for room {RoomCode}", roomCode);
            // No hay mas jugadores - game over
            await EndGameAsync(roomCode);
            return;
        }

        // Iniciar temporizador de turnos para el siguiente jugador
        room.TurnStartedAt = DateTime.UtcNow;
        _logger.LogInformation("[ADVANCE] Starting timer with {TimeLimit} seconds for room {RoomCode}",
            _options.QuestionTimeLimit, roomCode);
        StartTurnTimer(roomCode, _options.QuestionTimeLimit);

        // Broadcast TurnStarted para el nuevo turno
        _logger.LogInformation("[ADVANCE] Broadcasting TurnStarted for room {RoomCode}", roomCode);
        await BroadcastTurnStartedAsync(roomCode);
    }

    /// <summary>
    /// Maneja el timeout cuando un jugador no responde a tiempo.
    /// </summary>
    /// <param name="roomCode">Código de la sala.</param>
    private async Task HandleTurnTimeoutAsync(string roomCode)
    {
        _logger.LogWarning("[TIMEOUT] HandleTurnTimeoutAsync called for room {RoomCode}", roomCode);

        if (!_rooms.TryGetValue(roomCode, out var room))
        {
            _logger.LogWarning("[TIMEOUT] Room {RoomCode} not found", roomCode);
            return;
        }

        if (!_roomTurnManagers.TryGetValue(roomCode, out var turnManager))
        {
            _logger.LogWarning("[TIMEOUT] TurnManager not found for room {RoomCode}", roomCode);
            return;
        }

        var currentPlayerId = turnManager.GetCurrentPlayer();
        if (currentPlayerId == null)
        {
            _logger.LogWarning("[TIMEOUT] No current player in room {RoomCode}", roomCode);
            return;
        }

        var player = room.Players.FirstOrDefault(p => p.UserId == currentPlayerId);
        if (player == null)
        {
            _logger.LogWarning("[TIMEOUT] Player {PlayerId} not found in room {RoomCode}", currentPlayerId, roomCode);
            return;
        }

        // Marcar como respuesta incorrecta
        player.WrongAnswers++;

        _logger.LogInformation("[TIMEOUT] Turn timeout for user {UserId} (username: {Username}) in room {RoomCode}. CurrentQuestionIndex: {QuestionIndex}, TotalQuestions: {TotalQuestions}",
            currentPlayerId, player.Username, roomCode, room.CurrentQuestionIndex, room.Questions.Count);

        // Obtener pregunta actual para encontrar el indice de respuesta correcto
        if (room.CurrentQuestionIndex >= room.Questions.Count)
        {
            _logger.LogWarning("[TIMEOUT] CurrentQuestionIndex {Index} >= Questions.Count {Count}. Ending game.",
                room.CurrentQuestionIndex, room.Questions.Count);
            await EndGameAsync(roomCode);
            return;
        }

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

        _logger.LogInformation("[TIMEOUT] Broadcasting TurnTimeout for player {PlayerId} in room {RoomCode}", currentPlayerId, roomCode);

        // Crear TurnTimeoutDto
        await BroadcastTurnTimeoutAsync(roomCode, turnResult);

        _logger.LogInformation("[TIMEOUT] Calling AdvanceToNextTurnAsync for room {RoomCode}", roomCode);

        // Avanzar al siguiente turno
        await AdvanceToNextTurnAsync(roomCode);
    }

    /// <summary>
    /// Finaliza la partida, persiste el historial y notifica a los clientes.
    /// </summary>
    /// <param name="roomCode">Código de la sala.</param>
    private async Task EndGameAsync(string roomCode)
    {
        if (!_rooms.TryGetValue(roomCode, out var room))
            return;

        room.State = GameState.Finished;
        room.EndedAt = DateTime.UtcNow;

        // Cancelar cualquier temporizador pendiente
        CancelTurnTimer(roomCode);

        _logger.LogInformation("Game ended in room {RoomCode}", roomCode);

        // Obtener scope para servicios
        using var scope = _scopeFactory.CreateScope();
        var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<GameHub>>();

        // Filtrar players (excluir owner) y ordenar por score
        var filteredPlayers = room.Players
            .Where(p => p.UserId != room.OwnerId)
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
            filteredPlayers,
            room.Questions.Count,
            room.EndedAt!.Value - room.StartedAt!.Value
        );

        await hubContext.Clients.Group(roomCode).SendAsync("GameFinished", gameResult);

        // Persistir GameHistory
        try
        {
            var gameHistoryRepo = scope.ServiceProvider.GetRequiredService<IGameHistoryRepository>();
            var history = new GameHistory
            {
                GameId = room.QuizId * 1000 + room.Players.Count,
                QuizId = room.QuizId,
                OwnerId = room.OwnerId,
                QuizTitle = room.QuizTitle,
                StartedAt = room.StartedAt!.Value,
                EndedAt = room.EndedAt!.Value,
                PlayerResultsJson = System.Text.Json.JsonSerializer.Serialize(filteredPlayers)
            };
            await gameHistoryRepo.AddAsync(history);
            _logger.LogInformation("Game history persisted for room {RoomCode}", roomCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist game history for room {RoomCode}", roomCode);
        }

        _roomTurnManagers.TryRemove(roomCode, out _);
    }

    /// <summary>
    /// Inicia un temporizador para el turno actual.
    /// </summary>
    /// <param name="roomCode">Código de la sala.</param>
    /// <param name="timeLimitSeconds">Límite de tiempo en segundos.</param>
    private void StartTurnTimer(string roomCode, int timeLimitSeconds)
    {
        var cts = new CancellationTokenSource();
        _turnTimers[roomCode] = cts;

        _logger.LogInformation("[TIMER] Started turn timer for room {RoomCode} with {TimeLimit} seconds",
            roomCode, timeLimitSeconds);

        _ = Task.Run(async () =>
        {
            _logger.LogInformation("[TIMER] Timer task started for room {RoomCode}, will fire in {TimeLimit} seconds", roomCode, timeLimitSeconds);
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(timeLimitSeconds), cts.Token);
                _logger.LogWarning("[TIMER] Timer task completed without cancellation for room {RoomCode} - calling HandleTurnTimeoutAsync", roomCode);
                // Temporizador caducado - manejar timeout
                await HandleTurnTimeoutAsync(roomCode);
            }
            catch (TaskCanceledException)
            {
                _logger.LogInformation("[TIMER] Timer cancelled for room {RoomCode} - answer was submitted in time", roomCode);
                // Turno fue respondido a tiempo, temporizador cancelado
            }
        });
    }

    /// <summary>
    /// Cancela el temporizador del turno actual.
    /// </summary>
    /// <param name="roomCode">Código de la sala.</param>
    private void CancelTurnTimer(string roomCode)
    {
        if (_turnTimers.TryRemove(roomCode, out var cts))
        {
            _logger.LogInformation("[TIMER] Cancelling timer for room {RoomCode}", roomCode);
            cts.Cancel();
        }
        else
        {
            _logger.LogWarning("[TIMER] No timer found to cancel for room {RoomCode}", roomCode);
        }
    }

    /// <summary>
    /// Envía el evento TurnStarted a todos los clientes de la sala.
    /// </summary>
    /// <param name="roomCode">Código de la sala.</param>
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

        // Enviamos timeLimit - 1 para que el frontend muestre 20s mientras el backend cuenta 21s
        // así cuando el frontend llega a 0, queda 1 segundo extra de gracia
        var turnStartedDto = new TurnStartedDto(
            currentPlayerId.Value,
            false,
            questionDto,
            _options.QuestionTimeLimit - 1
        );

        using var scope = _scopeFactory.CreateScope();
        var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<GameHub>>();
        await hubContext.Clients.Group(roomCode).SendAsync("TurnStarted", turnStartedDto);
        _logger.LogDebug("Broadcast TurnStarted for player {PlayerId} in room {RoomCode}", currentPlayerId, roomCode);
    }

    /// <summary>
    /// Envía el resultado de un turno a todos los clientes de la sala.
    /// </summary>
    /// <param name="roomCode">Código de la sala.</param>
    /// <param name="turnResult">Resultado del turno.</param>
    private async Task BroadcastTurnResultAsync(string roomCode, TurnResultDto turnResult)
    {
        using var scope = _scopeFactory.CreateScope();
        var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<GameHub>>();
        await hubContext.Clients.Group(roomCode).SendAsync("TurnResult", turnResult);
        _logger.LogDebug("Broadcast TurnResult for player {PlayerId} in room {RoomCode}", turnResult.PlayerId, roomCode);
    }

    /// <summary>
    /// Envía el evento de timeout de turno a todos los clientes de la sala.
    /// </summary>
    /// <param name="roomCode">Código de la sala.</param>
    /// <param name="turnResult">Resultado del turno (sin puntos por timeout).</param>
    private async Task BroadcastTurnTimeoutAsync(string roomCode, TurnResultDto turnResult)
    {
        using var scope = _scopeFactory.CreateScope();
        var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<GameHub>>();
        await hubContext.Clients.Group(roomCode).SendAsync("TurnTimeout", turnResult);
        _logger.LogDebug("Broadcast TurnTimeout for player {PlayerId} in room {RoomCode}", turnResult.PlayerId, roomCode);
    }

    /// <inheritdoc cref="IGameService.PauseGameAsync"/>
    public async Task<Result> PauseGameAsync(string roomCode, long userId)
    {
        if (!_rooms.TryGetValue(roomCode, out var room))
        {
            _logger.LogWarning("Pause attempt on non-existent room: {RoomCode}", roomCode);
            return Result.Failure("Room not found.");
        }

        if (room.OwnerId != userId)
        {
            _logger.LogWarning("Non-owner {UserId} attempted to pause room {RoomCode}", userId, roomCode);
            return Result.Failure("Only the owner can pause the game.");
        }

        if (room.State != GameState.Playing)
        {
            _logger.LogWarning("Pause attempt on room {RoomCode} with state {State}", roomCode, room.State);
            return Result.Failure("Game can only be paused when playing.");
        }

        // Calcular tiempo restante del turno actual
        if (room.TurnStartedAt.HasValue)
        {
            var elapsed = (DateTime.UtcNow - room.TurnStartedAt.Value).TotalSeconds;
            var timeRemaining = Math.Max(0, _options.QuestionTimeLimit - (int)elapsed);
            room.PausedTimeRemaining = timeRemaining;
        }

        // Cancelar el temporizador del turno
        CancelTurnTimer(roomCode);

        // Cambiar estado a Paused
        room.State = GameState.Paused;

        _logger.LogInformation("Game paused in room {RoomCode} by owner {UserId}. Time remaining: {TimeRemaining}s",
            roomCode, userId, room.PausedTimeRemaining);

        // Broadcast GamePaused
        await BroadcastGamePausedAsync(roomCode);

        return Result.Success();
    }

    /// <inheritdoc cref="IGameService.ResumeGameAsync"/>
    public async Task<Result> ResumeGameAsync(string roomCode, long userId)
    {
        if (!_rooms.TryGetValue(roomCode, out var room))
        {
            _logger.LogWarning("Resume attempt on non-existent room: {RoomCode}", roomCode);
            return Result.Failure("Room not found.");
        }

        if (room.OwnerId != userId)
        {
            _logger.LogWarning("Non-owner {UserId} attempted to resume room {RoomCode}", userId, roomCode);
            return Result.Failure("Only the owner can resume the game.");
        }

        if (room.State != GameState.Paused)
        {
            _logger.LogWarning("Resume attempt on room {RoomCode} with state {State}", roomCode, room.State);
            return Result.Failure("Game can only be resumed when paused.");
        }

        // Obtener tiempo restante (o tiempo completo si no hay tiempo guardado)
        var timeToResume = room.PausedTimeRemaining ?? _options.QuestionTimeLimit;

        // Limpiar tiempo guardado
        room.PausedTimeRemaining = null;

        // Cambiar estado a Playing
        room.State = GameState.Playing;

        _logger.LogInformation("Game resumed in room {RoomCode} by owner {UserId}. Resuming with {TimeRemaining}s",
            roomCode, userId, timeToResume);

        // Reiniciar temporizador con el tiempo restante
        StartTurnTimer(roomCode, timeToResume);

        // Broadcast GameResumed
        await BroadcastGameResumedAsync(roomCode, timeToResume);

        return Result.Success();
    }

    /// <inheritdoc cref="IGameService.KickPlayerAsync"/>
    public async Task<Result<string?>> KickPlayerAsync(string roomCode, long ownerId, long playerIdToKick)
    {
        if (!_rooms.TryGetValue(roomCode, out var room))
        {
            _logger.LogWarning("Kick attempt on non-existent room: {RoomCode}", roomCode);
            return Result.Failure<string?>("Room not found.");
        }

        if (room.OwnerId != ownerId)
        {
            _logger.LogWarning("Non-owner {OwnerId} attempted to kick player in room {RoomCode}", ownerId, roomCode);
            return Result.Failure<string?>("Only the owner can kick players.");
        }

        if (ownerId == playerIdToKick)
        {
            _logger.LogWarning("Owner {OwnerId} attempted to kick themselves in room {RoomCode}", ownerId, roomCode);
            return Result.Failure<string?>("You cannot kick yourself.");
        }

        var playerToKick = room.Players.FirstOrDefault(p => p.UserId == playerIdToKick);
        if (playerToKick == null)
        {
            _logger.LogWarning("Player {PlayerIdToKick} not found in room {RoomCode}", playerIdToKick, roomCode);
            return Result.Failure<string?>("Player not found in this room.");
        }

        if (playerToKick.IsOwner)
        {
            _logger.LogWarning("Attempt to kick owner {PlayerIdToKick} in room {RoomCode}", playerIdToKick, roomCode);
            return Result.Failure<string?>("The owner cannot be kicked.");
        }

        // Store connectionId before removing player
        var connectionIdToRemove = playerToKick.ConnectionId;

        // Remove player from room
        room.Players.Remove(playerToKick);

        // Remove from user-to-room mapping
        _userToRoom.TryRemove(playerIdToKick, out _);

        // Remove from connection-to-user mapping if exists
        if (!string.IsNullOrEmpty(connectionIdToRemove))
        {
            _connectionToUser.TryRemove(connectionIdToRemove, out _);
        }

        _logger.LogInformation("Player {PlayerIdToKick} ({Username}) kicked from room {RoomCode} by owner {OwnerId}",
            playerIdToKick, playerToKick.Username, roomCode, ownerId);

        return Result.Success<string?>(connectionIdToRemove);
    }

    /// <summary>
    /// Envía el evento GamePaused a todos los clientes de la sala.
    /// </summary>
    /// <param name="roomCode">Código de la sala.</param>
    private async Task BroadcastGamePausedAsync(string roomCode)
    {
        using var scope = _scopeFactory.CreateScope();
        var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<GameHub>>();
        var pausedDto = new GamePausedDto(roomCode, DateTime.UtcNow);
        await hubContext.Clients.Group(roomCode).SendAsync("GamePaused", pausedDto);
        _logger.LogDebug("Broadcast GamePaused to room {RoomCode}", roomCode);
    }

    /// <summary>
    /// Envía el evento GameResumed a todos los clientes de la sala.
    /// </summary>
    /// <param name="roomCode">Código de la sala.</param>
    /// <param name="timeRemaining">Tiempo restante para el turno.</param>
    private async Task BroadcastGameResumedAsync(string roomCode, int timeRemaining)
    {
        using var scope = _scopeFactory.CreateScope();
        var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<GameHub>>();
        var resumedDto = new GameResumedDto(roomCode, timeRemaining);
        await hubContext.Clients.Group(roomCode).SendAsync("GameResumed", resumedDto);
        _logger.LogDebug("Broadcast GameResumed to room {RoomCode} with {TimeRemaining}s", roomCode, timeRemaining);
    }

    /// <inheritdoc cref="IGameService.HandleDisconnectionAsync"/>
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

    /// <inheritdoc cref="IGameService.GetRoomCodeByConnectionAsync"/>
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

    /// <summary>
    /// Genera un código único de sala de 6 caracteres alfanuméricos.
    /// </summary>
    /// <returns>Código de sala único.</returns>
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
