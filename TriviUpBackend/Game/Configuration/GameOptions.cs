namespace TriviUpBackend.Game.Configuration;

public class GameOptions
{
    public int MaxPlayersPerRoom { get; set; } = 10;
    public int MinPlayersToStart { get; set; } = 2;
    public int QuestionTimeLimit { get; set; } = 21;        // segundos (1 extra para gracia visual)
    public int TurnTransitionDelay { get; set; } = 2;        // segundos
    public int CountdownSeconds { get; set; } = 3;
    public int DisconnectTimeout { get; set; } = 30;        // segundos
    public int BasePoints { get; set; } = 100;
    public int TimeBonusMultiplier { get; set; } = 10;
    public int MaxTimeBonus { get; set; } = 200;
}
