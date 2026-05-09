namespace TriviUpBackend.Game.Models;

public class GameRoom
{
    public string RoomCode { get; set; } = string.Empty;
    public long QuizId { get; set; }
    public long OwnerId { get; set; }
    public GameState State { get; set; } = GameState.Waiting;
    public List<Player> Players { get; set; } = new();
    public Queue<long> TurnOrder { get; set; } = new();
    public int CurrentQuestionIndex { get; set; }
    public List<Pregunta> Questions { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
}
