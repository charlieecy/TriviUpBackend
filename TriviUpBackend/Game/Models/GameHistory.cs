namespace TriviUpBackend.Game.Models;

public class GameHistory
{
    public long Id { get; set; }
    public long GameId { get; set; }
    public long QuizId { get; set; }
    public long OwnerId { get; set; }
    public string QuizTitle { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime EndedAt { get; set; }
    public string PlayerResultsJson { get; set; } = "[]";

    // Computed property to deserialize PlayerResults
    public List<PlayerResult> PlayerResults =>
        string.IsNullOrEmpty(PlayerResultsJson)
            ? new List<PlayerResult>()
            : System.Text.Json.JsonSerializer.Deserialize<List<PlayerResult>>(PlayerResultsJson) ?? new List<PlayerResult>();
}

public class PlayerResult
{
    public long UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public int FinalScore { get; set; }
    public int CorrectAnswers { get; set; }
    public int WrongAnswers { get; set; }
    public int Rank { get; set; }
}
