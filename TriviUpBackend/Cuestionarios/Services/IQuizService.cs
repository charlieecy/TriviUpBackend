using CSharpFunctionalExtensions;
using TriviUpBackend.Cuestionarios.DTOs;
using TriviUpBackend.Errors;

namespace TriviUpBackend.Cuestionarios.Services;

public interface IQuizService
{
    Task<Result<QuizResponse, QuizError>> CreateAsync(CreateQuizRequest request, long creatorId);

    Task<Result<QuizResponse, QuizError>> GetByIdAsync(long id);

    Task<Result<QuizResponse, QuizError>> GetByGameCodeAsync(string gameCode);

    Task<Result<(List<QuizResponse> Quizzes, int TotalCount), QuizError>> GetAllAsync(int page = 1, int pageSize = 10);

    Task<Result<List<QuizResponse>, QuizError>> GetByCreatorIdAsync(long creatorId);

    Task<Result<QuizResponse, QuizError>> UpdateAsync(long id, UpdateQuizRequest request, long userId);

    Task<UnitResult<QuizError>> DeleteAsync(long id, long userId);

    Task<Result<(List<PublicQuizResponse> Quizzes, int TotalCount), QuizError>> GetPublicQuizzesAsync(string? search, int page, int pageSize);

    Task<Result<int, QuizError>> IncrementLikesAsync(long id);

    Task<Result<int, QuizError>> DecrementLikesAsync(long id);

    Task<Result<int, QuizError>> IncrementVisitasAsync(long id);
}
