namespace TriviUpBackend.Game.Services;

public interface ITurnManager
{
    void InitializeQueue(IEnumerable<long> playerIds);
    long? GetCurrentPlayer();
    long? GetNextPlayer();
    bool HasPlayers();
    void Reset(IEnumerable<long> playerIds);
}
