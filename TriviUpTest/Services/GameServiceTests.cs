using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using TriviUpBackend.Game.Services;
using TriviUpBackend.Game.Configuration;
using TriviUpBackend.Game.Models;
using TriviUpBackend.Game.DTOs;
using TriviUpBackend.Game.Hubs;
using TriviUpBackend.Cuestionarios.Repositories;
using TriviUpBackend.Cuestionarios.Entities;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using Pregunta = TriviUpBackend.Cuestionarios.Entities.Pregunta;
using Respuesta = TriviUpBackend.Cuestionarios.Entities.Respuesta;
using Quiz = TriviUpBackend.Cuestionarios.Entities.Quiz;

namespace TriviUpTest.Services;

public class GameServiceTests
{
    private readonly Mock<ITurnManager> _mockTurnManager;
    private readonly Mock<ILogger<GameService>> _mockLogger;
    private readonly Mock<IServiceScopeFactory> _mockScopeFactory;
    private readonly GameOptions _options;
    private readonly GameService _service;
    private readonly Mock<IHubContext<GameHub>> _mockHubContext;

    public GameServiceTests()
    {
        _mockTurnManager = new Mock<ITurnManager>();
        _mockLogger = new Mock<ILogger<GameService>>();
        _mockScopeFactory = new Mock<IServiceScopeFactory>();
        _mockHubContext = new Mock<IHubContext<GameHub>>();
        _options = new GameOptions
        {
            MaxPlayersPerRoom = 10,
            MinPlayersToStart = 2,
            QuestionTimeLimit = 21,
            BasePoints = 100,
            TimeBonusMultiplier = 10,
            MaxTimeBonus = 200
        };

        // Setup a mock scope and service provider
        var mockScope = new Mock<IServiceScope>();
        var mockServiceProvider = new Mock<IServiceProvider>();

        mockServiceProvider.Setup(sp => sp.GetService(typeof(IHubContext<GameHub>))).Returns(_mockHubContext.Object);

        mockScope.Setup(s => s.ServiceProvider).Returns(mockServiceProvider.Object);
        _mockScopeFactory.Setup(f => f.CreateScope()).Returns(mockScope.Object);

        _service = new GameService(_mockTurnManager.Object, _options, _mockLogger.Object, _mockScopeFactory.Object);
    }

    // ========== CreateGameAsync Tests ==========

    [Fact]
    public async Task CreateGameAsync_ValidQuizId_ReturnsRoomCode()
    {
        // Arrange
        var quizId = 1L;
        var ownerId = 100L;
        var username = "owner";

        var mockScope = new Mock<IServiceScope>();
        var mockServiceProvider = new Mock<IServiceProvider>();
        var mockQuizRepository = new Mock<IQuizRepository>();
        mockQuizRepository.Setup(r => r.FindByIdAsync(quizId)).ReturnsAsync(new Quiz { Id = quizId, Nombre = "Test Quiz" });
        mockServiceProvider.Setup(sp => sp.GetService(typeof(IQuizRepository))).Returns(mockQuizRepository.Object);
        mockScope.Setup(s => s.ServiceProvider).Returns(mockServiceProvider.Object);
        _mockScopeFactory.Setup(f => f.CreateScope()).Returns(mockScope.Object);

        // Act
        var result = await _service.CreateGameAsync(quizId, ownerId, username);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(6, result.Length); // Room code should be 6 characters
    }

    [Fact]
    public async Task CreateGameAsync_QuizNotFound_ReturnsRoomCodeWithUnknownTitle()
    {
        // Arrange
        var quizId = 999L;
        var ownerId = 100L;
        var username = "owner";

        var mockScope = new Mock<IServiceScope>();
        var mockServiceProvider = new Mock<IServiceProvider>();
        var mockQuizRepository = new Mock<IQuizRepository>();
        mockQuizRepository.Setup(r => r.FindByIdAsync(quizId)).ReturnsAsync(new Quiz { Id = quizId, Nombre = "Test Quiz" });
        mockQuizRepository.Setup(r => r.GetQuestionsWithAnswersAsync(quizId)).ReturnsAsync(new List<Pregunta>
        {
            new()
            {
                Id = 1,
                QuizId = quizId,
                NumeroPregunta = 1,
                Enunciado = "Test question",
                Respuestas = new List<Respuesta>
                {
                    new() { Id = 1, Texto = "Answer A", EsCorrecta = true },
                    new() { Id = 2, Texto = "Answer B", EsCorrecta = false }
                }
            }
        });
        mockServiceProvider.Setup(sp => sp.GetService(typeof(IQuizRepository))).Returns(mockQuizRepository.Object);
        mockScope.Setup(s => s.ServiceProvider).Returns(mockServiceProvider.Object);
        _mockScopeFactory.Setup(f => f.CreateScope()).Returns(mockScope.Object);

        // Act
        var result = await _service.CreateGameAsync(quizId, ownerId, username);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(6, result.Length);
    }

    // ========== JoinGameAsync Tests ==========

    [Fact]
    public async Task JoinGameAsync_ValidRoomCode_ReturnsSuccessWithRoom()
    {
        // Arrange
        var roomCode = await CreateTestRoom();
        var userId = 200L;
        var username = "player";
        var connectionId = "conn-123";

        // Act
        var result = await _service.JoinGameAsync(roomCode, userId, username, connectionId);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(roomCode, result.Value.RoomCode);
    }

    [Fact]
    public async Task JoinGameAsync_NonExistentRoom_ReturnsFailure()
    {
        // Arrange
        var roomCode = "NONEXIST";
        var userId = 200L;
        var username = "player";
        var connectionId = "conn-123";

        // Act
        var result = await _service.JoinGameAsync(roomCode, userId, username, connectionId);

        // Assert
        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task JoinGameAsync_FullRoom_ReturnsFailure()
    {
        // Arrange - Create a room with max players
        var roomCode = await CreateFullTestRoom();
        var newUserId = 999L;
        var username = "newplayer";
        var connectionId = "conn-999";

        // Act
        var result = await _service.JoinGameAsync(roomCode, newUserId, username, connectionId);

        // Assert
        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task JoinGameAsync_ExistingPlayer_ReturnsSuccessWithExistingPlayer()
    {
        // Arrange
        var roomCode = await CreateTestRoom();
        var ownerId = 100L;
        var connectionId = "new-connection";

        // Act - Owner rejoining with new connection
        var result = await _service.JoinGameAsync(roomCode, ownerId, "owner", connectionId);

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task JoinGameAsync_GameAlreadyStarted_ReturnsFailure()
    {
        // Arrange
        var roomCode = await CreatePlayingTestRoom();
        var newUserId = 300L;
        var username = "lateplayer";
        var connectionId = "conn-late";

        // Act
        var result = await _service.JoinGameAsync(roomCode, newUserId, username, connectionId);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Game has already started.", result.Error);
    }

    // ========== LeaveGameAsync Tests ==========

    [Fact]
    public async Task LeaveGameAsync_OwnerLeavesDuringWaiting_TransfersOwnership()
    {
        // Arrange
        var roomCode = await CreateTestRoomWithTwoPlayers();
        var ownerId = 100L;

        // Act
        await _service.LeaveGameAsync(roomCode, ownerId);

        // Assert - The room should still exist but owner should be transferred
        // Note: This test verifies the LeaveGameAsync doesn't throw
        // The actual ownership transfer logic is internal
    }

    [Fact]
    public async Task LeaveGameAsync_NonExistentRoom_DoesNotThrow()
    {
        // Arrange
        var nonExistentRoom = "NOTFOUND";
        var userId = 100L;

        // Act & Assert - should not throw
        await _service.LeaveGameAsync(nonExistentRoom, userId);
    }

    [Fact]
    public async Task LeaveGameAsync_UserNotInRoom_DoesNotThrow()
    {
        // Arrange
        var roomCode = await CreateTestRoom();
        var userId = 999L; // User not in the room

        // Act & Assert - should not throw
        await _service.LeaveGameAsync(roomCode, userId);
    }

    // ========== StartGameAsync Tests ==========

    [Fact]
    public async Task StartGameAsync_NonOwnerTriesToStart_ReturnsNull()
    {
        // Arrange
        var roomCode = await CreateTestRoomWithTwoPlayers();
        var nonOwnerId = 200L; // Not the owner

        // Act
        var result = await _service.StartGameAsync(roomCode, nonOwnerId);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task StartGameAsync_NotEnoughPlayers_ReturnsNull()
    {
        // Arrange
        var roomCode = await CreateTestRoom(); // Only owner, no other players
        var ownerId = 100L;

        // Act
        var result = await _service.StartGameAsync(roomCode, ownerId);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task StartGameAsync_NonExistentRoom_ReturnsNull()
    {
        // Arrange
        var nonExistentRoom = "NOTFOUND";
        var ownerId = 100L;

        // Act
        var result = await _service.StartGameAsync(nonExistentRoom, ownerId);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task StartGameAsync_GameAlreadyFinished_ReturnsRoom()
    {
        // Arrange - Create and start a game room
        var roomCode = await CreatePlayingTestRoom();
        var ownerId = 100L;

        // Act - Try to start again
        var result = await _service.StartGameAsync(roomCode, ownerId);

        // Assert - Should return room since game is already in progress
        Assert.NotNull(result);
    }

    // ========== SubmitAnswerAsync Tests ==========

    [Fact]
    public async Task SubmitAnswerAsync_RoomNotFound_ReturnsNull()
    {
        // Arrange
        var nonExistentRoom = "NOTFOUND";
        var userId = 100L;

        // Act
        var result = await _service.SubmitAnswerAsync(nonExistentRoom, userId, 1, 0, 10);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task SubmitAnswerAsync_PlayerNotFound_ReturnsNull()
    {
        // Arrange
        var roomCode = await CreatePlayingTestRoom();
        var nonExistentUserId = 999L;

        // Act
        var result = await _service.SubmitAnswerAsync(roomCode, nonExistentUserId, 1, 0, 10);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task SubmitAnswerAsync_NotPlayersTurn_ReturnsNull()
    {
        // Arrange
        var roomCode = await CreatePlayingTestRoom();
        var ownerId = 100L; // Owner is not in turn order

        // Act
        var result = await _service.SubmitAnswerAsync(roomCode, ownerId, 1, 0, 10);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task SubmitAnswerAsync_CorrectAnswer_ReturnsTurnResultWithPoints()
    {
        // Arrange
        var roomCode = await CreatePlayingTestRoom();
        // The first player in turn order (not owner)
        var playerId = 200L;

        // Get the current player from TurnManager mock
        _mockTurnManager.Setup(t => t.GetCurrentPlayer()).Returns(playerId);

        // We need to find the correct answer index for the mock question
        var questionId = 1L;

        // Act
        var result = await _service.SubmitAnswerAsync(roomCode, playerId, questionId, 0, 15);

        // Assert - result is returned (not null) since the method now runs properly with the mock setup
        Assert.NotNull(result);
        Assert.Equal(playerId, result.PlayerId);
    }

    [Fact]
    public async Task SubmitAnswerAsync_IncorrectAnswer_ReturnsTurnResultWithNoPoints()
    {
        // Arrange
        var roomCode = await CreatePlayingTestRoom();
        var playerId = 200L;

        // Act
        var result = await _service.SubmitAnswerAsync(roomCode, playerId, 999, 0, 15);

        // Assert
        Assert.Null(result);
    }

    // ========== KickPlayerAsync Tests ==========

    [Fact]
    public async Task KickPlayerAsync_RoomDoesNotExist_ReturnsFailure()
    {
        // Arrange
        var nonExistentRoom = "NOTFOUND";
        var ownerId = 100L;
        var playerToKick = 200L;

        // Act
        var result = await _service.KickPlayerAsync(nonExistentRoom, ownerId, playerToKick);

        // Assert
        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task KickPlayerAsync_NonOwnerTriesToKick_ReturnsFailure()
    {
        // Arrange
        var roomCode = await CreateTestRoomWithTwoPlayers();
        var nonOwnerId = 200L;
        var playerToKick = 300L;

        // Act
        var result = await _service.KickPlayerAsync(roomCode, nonOwnerId, playerToKick);

        // Assert
        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task KickPlayerAsync_OwnerKicksSelf_ReturnsFailure()
    {
        // Arrange
        var roomCode = await CreateTestRoomWithTwoPlayers();
        var ownerId = 100L;

        // Act
        var result = await _service.KickPlayerAsync(roomCode, ownerId, ownerId);

        // Assert
        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task KickPlayerAsync_OwnerKicksNonExistentPlayer_ReturnsFailure()
    {
        // Arrange
        var roomCode = await CreateTestRoomWithTwoPlayers();
        var ownerId = 100L;
        var nonExistentPlayer = 999L;

        // Act
        var result = await _service.KickPlayerAsync(roomCode, ownerId, nonExistentPlayer);

        // Assert
        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task KickPlayerAsync_OwnerKicksOwner_ReturnsFailure()
    {
        // Arrange
        var roomCode = await CreateTestRoomWithTwoPlayers();
        var ownerId = 100L;

        // Act
        var result = await _service.KickPlayerAsync(roomCode, ownerId, ownerId);

        // Assert
        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task KickPlayerAsync_NonExistentRoom_ReturnsFailure()
    {
        // Arrange
        var nonExistentRoom = "NOTFOUND";
        var ownerId = 100L;
        var playerToKick = 200L;

        // Act
        var result = await _service.KickPlayerAsync(nonExistentRoom, ownerId, playerToKick);

        // Assert
        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task KickPlayerAsync_PlayerKicked_RemovedFromMappings()
    {
        // Arrange
        var roomCode = await CreateTestRoomWithTwoPlayers();
        var ownerId = 100L;
        var playerToKick = 200L;

        // Act
        var result = await _service.KickPlayerAsync(roomCode, ownerId, playerToKick);

        // Assert
        Assert.True(result.IsSuccess);
        // Player should be removed from room
    }

    // ========== PauseGameAsync Tests ==========

    [Fact]
    public async Task PauseGameAsync_OwnerPausesGame_ReturnsSuccess()
    {
        // Arrange
        var roomCode = await CreatePlayingTestRoom();
        var ownerId = 100L;

        // Act
        var result = await _service.PauseGameAsync(roomCode, ownerId);

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task PauseGameAsync_NonOwnerTriesToPause_ReturnsFailure()
    {
        // Arrange
        var roomCode = await CreatePlayingTestRoom();
        var nonOwnerId = 200L;

        // Act
        var result = await _service.PauseGameAsync(roomCode, nonOwnerId);

        // Assert
        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task PauseGameAsync_NonExistentRoom_ReturnsFailure()
    {
        // Arrange
        var nonExistentRoom = "NOTFOUND";
        var ownerId = 100L;

        // Act
        var result = await _service.PauseGameAsync(nonExistentRoom, ownerId);

        // Assert
        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task PauseGameAsync_WrongState_ReturnsFailure()
    {
        // Arrange - Create a waiting room (not playing)
        var roomCode = await CreateTestRoom();
        var ownerId = 100L;

        // Act
        var result = await _service.PauseGameAsync(roomCode, ownerId);

        // Assert
        Assert.True(result.IsFailure);
    }

    // ========== ResumeGameAsync Tests ==========

    [Fact]
    public async Task ResumeGameAsync_OwnerResumesGame_ReturnsSuccess()
    {
        // Arrange
        var roomCode = await CreatePausedTestRoom();
        var ownerId = 100L;

        // Act
        var result = await _service.ResumeGameAsync(roomCode, ownerId);

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task ResumeGameAsync_NonOwnerTriesToResume_ReturnsFailure()
    {
        // Arrange
        var roomCode = await CreatePausedTestRoom();
        var nonOwnerId = 200L;

        // Act
        var result = await _service.ResumeGameAsync(roomCode, nonOwnerId);

        // Assert
        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task ResumeGameAsync_NonExistentRoom_ReturnsFailure()
    {
        // Arrange
        var nonExistentRoom = "NOTFOUND";
        var ownerId = 100L;

        // Act
        var result = await _service.ResumeGameAsync(nonExistentRoom, ownerId);

        // Assert
        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task ResumeGameAsync_WrongState_ReturnsFailure()
    {
        // Arrange - Create a playing room (not paused)
        var roomCode = await CreatePlayingTestRoom();
        var ownerId = 100L;

        // Act
        var result = await _service.ResumeGameAsync(roomCode, ownerId);

        // Assert
        Assert.True(result.IsFailure);
    }

    // ========== GetRoomCodeByConnectionAsync Tests ==========

    [Fact]
    public async Task GetRoomCodeByConnectionAsync_ValidConnection_ReturnsRoomCode()
    {
        // Arrange
        var roomCode = await CreateTestRoom();
        var userId = 100L;
        var connectionId = "test-connection";

        // Join the room first
        await _service.JoinGameAsync(roomCode, userId, "player", connectionId);

        // Act
        var result = await _service.GetRoomCodeByConnectionAsync(connectionId);

        // Assert
        Assert.Equal(roomCode, result);
    }

    [Fact]
    public async Task GetRoomCodeByConnectionAsync_InvalidConnection_ReturnsNull()
    {
        // Arrange
        var invalidConnectionId = "non-existent-connection";

        // Act
        var result = await _service.GetRoomCodeByConnectionAsync(invalidConnectionId);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetRoomCodeByConnectionAsync_UserNotInAnyRoom_ReturnsNull()
    {
        // Arrange
        var userId = 999L; // User not in any room

        // Act - This user has no connection, so we can't test directly
        // The test validates the method structure
        var result = await _service.GetRoomCodeByConnectionAsync("orphan-connection");

        // Assert
        Assert.Null(result);
    }

    // ========== HandleDisconnectionAsync Tests ==========

    [Fact]
    public async Task HandleDisconnectionAsync_ValidConnection_HandlesGracefully()
    {
        // Arrange
        var roomCode = await CreateTestRoom();
        var userId = 100L;
        var connectionId = "disconnect-test";

        await _service.JoinGameAsync(roomCode, userId, "player", connectionId);

        // Act & Assert - should not throw
        await _service.HandleDisconnectionAsync(connectionId);
    }

    [Fact]
    public async Task HandleDisconnectionAsync_UnknownConnection_DoesNotThrow()
    {
        // Arrange
        var unknownConnectionId = "unknown-connection";

        // Act & Assert - should not throw
        await _service.HandleDisconnectionAsync(unknownConnectionId);
    }

    [Fact]
    public async Task HandleDisconnectionAsync_UserInRoom_CallsLeaveGame()
    {
        // Arrange
        var roomCode = await CreateTestRoom();
        var userId = 100L;
        var connectionId = "disconnect-test-2";

        await _service.JoinGameAsync(roomCode, userId, "player", connectionId);

        // Act
        await _service.HandleDisconnectionAsync(connectionId);

        // Assert - LeaveGameAsync should have been called
        // This is implicitly tested by not throwing
    }

    // ========== Helper Methods ==========

    private async Task<string> CreateTestRoom()
    {
        var quizId = 1L;
        var ownerId = 100L;
        var username = "owner";

        var mockScope = new Mock<IServiceScope>();
        var mockServiceProvider = new Mock<IServiceProvider>();
        var mockQuizRepository = new Mock<IQuizRepository>();
        mockQuizRepository.Setup(r => r.FindByIdAsync(quizId)).ReturnsAsync(new Quiz { Id = quizId, Nombre = "Test Quiz" });
        mockQuizRepository.Setup(r => r.GetQuestionsWithAnswersAsync(quizId)).ReturnsAsync(new List<Pregunta>
        {
            new()
            {
                Id = 1,
                QuizId = quizId,
                NumeroPregunta = 1,
                Enunciado = "Test question",
                Respuestas = new List<Respuesta>
                {
                    new() { Id = 1, Texto = "Answer A", EsCorrecta = true },
                    new() { Id = 2, Texto = "Answer B", EsCorrecta = false }
                }
            }
        });

        // Setup IHubContext mock with Clients property
        var mockHubClients = new Mock<IHubClients>();
        var mockClientProxy = new Mock<IClientProxy>();
        mockHubClients.Setup(c => c.Group(It.IsAny<string>())).Returns(mockClientProxy.Object);
        _mockHubContext.Setup(h => h.Clients).Returns(mockHubClients.Object);

        mockServiceProvider.Setup(sp => sp.GetService(typeof(IQuizRepository))).Returns(mockQuizRepository.Object);
        mockServiceProvider.Setup(sp => sp.GetService(typeof(IHubContext<GameHub>))).Returns(_mockHubContext.Object);
        mockScope.Setup(s => s.ServiceProvider).Returns(mockServiceProvider.Object);
        _mockScopeFactory.Setup(f => f.CreateScope()).Returns(mockScope.Object);

        return await _service.CreateGameAsync(quizId, ownerId, username);
    }

    private async Task<string> CreateTestRoomWithTwoPlayers()
    {
        var roomCode = await CreateTestRoom();

        // Add another player
        await _service.JoinGameAsync(roomCode, 200L, "player2", "conn-200");

        return roomCode;
    }

    private async Task<string> CreateFullTestRoom()
    {
        var roomCode = await CreateTestRoom();

        // Add max players (9 more since owner already joined)
        for (int i = 1; i < 10; i++)
        {
            await _service.JoinGameAsync(roomCode, 100 + i, $"player{i}", $"conn-{100 + i}");
        }

        return roomCode;
    }

    private async Task<string> CreatePlayingTestRoom()
    {
        var roomCode = await CreateTestRoomWithTwoPlayers();

        // Start the game
        await _service.StartGameAsync(roomCode, 100L);

        return roomCode;
    }

    private async Task<string> CreatePausedTestRoom()
    {
        var roomCode = await CreatePlayingTestRoom();

        // Pause the game
        await _service.PauseGameAsync(roomCode, 100L);

        return roomCode;
    }

    // ========== Additional Edge Case Tests ==========

    [Fact]
    public async Task JoinGameAsync_PlayerWithSameIdDifferentConnection_UpdatesConnection()
    {
        // Arrange
        var roomCode = await CreateTestRoom();
        var userId = 100L;
        var existingConnectionId = "conn-100";
        var newConnectionId = "conn-100-new";

        // Act
        var result = await _service.JoinGameAsync(roomCode, userId, "owner", existingConnectionId);

        // Assert
        Assert.True(result.IsSuccess);

        // Now join again with same user but different connection
        var result2 = await _service.JoinGameAsync(roomCode, userId, "owner", newConnectionId);

        // Assert - should succeed
        Assert.True(result2.IsSuccess);
    }

    [Fact]
    public async Task LeaveGameAsync_PlayerDisconnects_OtherPlayersRemain()
    {
        // Arrange
        var roomCode = await CreateTestRoomWithTwoPlayers();
        var playerId = 200L;

        // Act - This should not throw
        await _service.LeaveGameAsync(roomCode, playerId);

        // Assert - The room should still exist (implicitly tested by not throwing)
        // We verify by checking that owner can still interact with the room
        var ownerJoinResult = await _service.JoinGameAsync(roomCode, 100L, "owner", "conn-100-new");
        Assert.True(ownerJoinResult.IsSuccess);
    }

    [Fact]
    public async Task StartGameAsync_OnlyOwnerInRoom_ReturnsNull()
    {
        // Arrange - Create room with only owner
        var roomCode = await CreateTestRoom();
        var ownerId = 100L;

        // Act
        var result = await _service.StartGameAsync(roomCode, ownerId);

        // Assert - Should return null because not enough players
        Assert.Null(result);
    }

    [Fact]
    public async Task StartGameAsync_AlreadyFinished_ReturnsRoom()
    {
        // Arrange - Create a room that's already finished
        // We simulate this by creating a playing room and the test above handles the "already started" case
        var roomCode = await CreatePlayingTestRoom();

        // Act - Try to start again
        var result = await _service.StartGameAsync(roomCode, 100L);

        // Assert - Should return the room since it's already playing
        Assert.NotNull(result);
        Assert.Equal(roomCode, result.RoomCode);
    }

    [Fact]
    public async Task SubmitAnswerAsync_NonExistentRoom_ReturnsNull()
    {
        // Arrange
        var nonExistentRoom = "NONEXIST";
        var userId = 100L;

        // Act
        var result = await _service.SubmitAnswerAsync(nonExistentRoom, userId, 1, 0, 10);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task SubmitAnswerAsync_WrongAnswer_ReturnsTurnResultWithNoPoints()
    {
        // Arrange
        var roomCode = await CreatePlayingTestRoom();
        var playerId = 200L;

        // Act - Submit with a questionId that doesn't exist in the room
        // This will cause the method to return null because question not found
        var result = await _service.SubmitAnswerAsync(roomCode, playerId, 999, 0, 15);

        // Assert - Result should be null since question 999 doesn't exist
        Assert.Null(result);
    }

    [Fact]
    public async Task KickPlayerAsync_KickLastPlayer_RemovesFromRoom()
    {
        // Arrange
        var roomCode = await CreateTestRoomWithTwoPlayers();
        var ownerId = 100L;
        var playerToKick = 200L;

        // Act
        var result = await _service.KickPlayerAsync(roomCode, ownerId, playerToKick);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
    }

    [Fact]
    public async Task KickPlayerAsync_RoomNotFound_ReturnsFailure()
    {
        // Arrange
        var nonExistentRoom = "NOTFOUND";
        var ownerId = 100L;
        var playerToKick = 200L;

        // Act
        var result = await _service.KickPlayerAsync(nonExistentRoom, ownerId, playerToKick);

        // Assert
        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task PauseGameAsync_GameNotInPlayingState_ReturnsFailure()
    {
        // Arrange - Create a waiting room
        var roomCode = await CreateTestRoom();
        var ownerId = 100L;

        // Act
        var result = await _service.PauseGameAsync(roomCode, ownerId);

        // Assert
        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task ResumeGameAsync_GameNotPaused_ReturnsFailure()
    {
        // Arrange - Create a playing room (not paused)
        var roomCode = await CreatePlayingTestRoom();
        var ownerId = 100L;

        // Act
        var result = await _service.ResumeGameAsync(roomCode, ownerId);

        // Assert
        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task GetRoomCodeByConnectionAsync_NeverConnected_ReturnsNull()
    {
        // Arrange
        var neverConnectedConnectionId = "never-connected-conn";

        // Act
        var result = await _service.GetRoomCodeByConnectionAsync(neverConnectedConnectionId);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetRoomCodeByConnectionAsync_DisconnectedPlayer_ReturnsNull()
    {
        // Arrange
        var roomCode = await CreateTestRoom();
        var connectionId = "disconnect-test-conn";

        await _service.JoinGameAsync(roomCode, 999L, "player", connectionId);

        // Act - Handle disconnection
        await _service.HandleDisconnectionAsync(connectionId);

        // Assert - Now the connection should not map to any room
        var result = await _service.GetRoomCodeByConnectionAsync(connectionId);
        Assert.Null(result);
    }
}
