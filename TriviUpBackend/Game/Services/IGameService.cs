using TriviUpBackend.Game.Models;

namespace TriviUpBackend.Game.Services;

public interface IGameService
{
    Task<string> CreateGameAsync(long quizId, long ownerId, string username);
    Task<bool> JoinGameAsync(string roomCode, long userId, string username, string connectionId);
    Task LeaveGameAsync(string roomCode, long userId);
    Task<bool> StartGameAsync(string roomCode, long userId);
    Task SubmitAnswerAsync(string roomCode, long userId, long questionId, int answerIndex, int timeRemaining);
    Task HandleDisconnectionAsync(string connectionId);
    Task<string?> GetRoomCodeByConnectionAsync(string connectionId);
}
