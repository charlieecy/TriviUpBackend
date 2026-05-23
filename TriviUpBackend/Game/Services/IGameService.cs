using CSharpFunctionalExtensions;
using TriviUpBackend.Game.Models;
using TriviUpBackend.Game.DTOs;

namespace TriviUpBackend.Game.Services;

public interface IGameService
{
    Task<string> CreateGameAsync(long quizId, long ownerId, string username);
    Task<Result<GameRoom>> JoinGameAsync(string roomCode, long userId, string username, string connectionId);
    Task LeaveGameAsync(string roomCode, long userId);
    Task<GameRoom?> StartGameAsync(string roomCode, long userId);
    Task<TurnResultDto?> SubmitAnswerAsync(string roomCode, long userId, long questionId, int answerIndex, int timeRemaining);
    Task<Result> PauseGameAsync(string roomCode, long userId);
    Task<Result> ResumeGameAsync(string roomCode, long userId);
    Task<Result<string?>> KickPlayerAsync(string roomCode, long ownerId, long playerIdToKick);
    Task HandleDisconnectionAsync(string connectionId);
    Task<string?> GetRoomCodeByConnectionAsync(string connectionId);
}
