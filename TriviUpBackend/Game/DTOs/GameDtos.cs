namespace TriviUpBackend.Game.DTOs;

public record CreateGameRequest(
    long QuizId
);

public record JoinGameRequest(
    string RoomCode
);

public record AnswerSubmission(
    string RoomCode,
    long QuestionId,
    int AnswerIndex,
    int TimeRemaining
);

public record GameStateDto(
    string RoomCode,
    string State,
    List<PlayerDto> Players,
    int CurrentQuestionIndex,
    int TotalQuestions
);

public record PlayerDto(
    long UserId,
    string Username,
    int Score,
    int CorrectAnswers,
    int WrongAnswers,
    bool IsCurrentTurn,
    bool IsOwner,
    bool IsConnected
);

public record TurnResultDto(
    long PlayerId,
    bool IsCorrect,
    int CorrectAnswerIndex,
    int PointsEarned,
    int NewTotalScore
);

public record GameResultDto(
    string RoomCode,
    string QuizTitle,
    List<PlayerResultDto> PlayerResults,
    int TotalQuestions,
    TimeSpan GameDuration
);

public record PlayerResultDto(
    long UserId,
    string Username,
    int Rank,
    int FinalScore,
    int CorrectAnswers,
    int WrongAnswers,
    int CorrectPercentage
);

public record TurnStartedDto(
    long CurrentPlayerId,
    bool IsMyTurn,
    QuestionDto Question,
    int TimeLimit
);

public record QuestionDto(
    long Id,
    string Text,
    List<string> Options,
    string? ImageUrl
);

public record GameHistoryDto(
    long GameId,
    long QuizId,
    string QuizTitle,
    DateTime StartedAt,
    DateTime EndedAt,
    long OwnerId,
    List<HistoryPlayerResultDto> PlayerResults
);

public record HistoryPlayerResultDto(
    long UserId,
    string Username,
    int FinalScore,
    int CorrectAnswers,
    int WrongAnswers,
    int Rank
);

public record GamePausedDto(
    string RoomCode,
    DateTime PausedAt
);

public record GameResumedDto(
    string RoomCode,
    int TimeRemaining
);

// Admin Stats DTOs
public record AdminStatsDto(
    int TotalGamesPlayed,
    int TotalQuizzes,
    int TotalUsers,
    int ActiveUsersLast24h,
    QuizWithMostFavoritesDto? MostFavoritesQuiz,
    QuizWithMostVisitsDto? MostVisitsQuiz,
    List<DailyGamesDto> GamesPerDay,
    List<ActiveUsersDto> ActiveUsersPerDay
);

public record QuizWithMostFavoritesDto(
    long Id,
    string Nombre,
    int Favorites
);

public record QuizWithMostVisitsDto(
    long Id,
    string Nombre,
    int Visitas
);

public record DailyGamesDto(
    DateTime Date,
    int Count
);

public record ActiveUsersDto(
    DateTime Date,
    int Count
);
