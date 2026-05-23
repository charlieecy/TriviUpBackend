using Xunit;
using TriviUpBackend.Game.Services;

namespace TriviUpTest.Services;

public class TurnManagerTests
{
    [Fact]
    public void InitializeQueue_WithPlayerIds_CreatesShuffledQueue()
    {
        // Arrange
        var turnManager = new TurnManager();
        var playerIds = new List<long> { 1, 2, 3, 4, 5 };

        // Act
        turnManager.InitializeQueue(playerIds);

        // Assert
        Assert.True(turnManager.HasPlayers());
        Assert.NotNull(turnManager.GetCurrentPlayer());
    }

    [Fact]
    public void InitializeQueue_WithEmptyList_CreatesEmptyQueue()
    {
        // Arrange
        var turnManager = new TurnManager();
        var playerIds = new List<long>();

        // Act
        turnManager.InitializeQueue(playerIds);

        // Assert
        Assert.False(turnManager.HasPlayers());
        Assert.Null(turnManager.GetCurrentPlayer());
    }

    [Fact]
    public void GetCurrentPlayer_WithPlayers_ReturnsFirstPlayer()
    {
        // Arrange
        var turnManager = new TurnManager();
        var playerIds = new List<long> { 1, 2, 3 };
        turnManager.InitializeQueue(playerIds);

        // Act
        var currentPlayer = turnManager.GetCurrentPlayer();

        // Assert
        Assert.NotNull(currentPlayer);
        Assert.Contains(currentPlayer.Value, playerIds);
    }

    [Fact]
    public void GetCurrentPlayer_EmptyQueue_ReturnsNull()
    {
        // Arrange
        var turnManager = new TurnManager();
        turnManager.InitializeQueue(new List<long>());

        // Act
        var currentPlayer = turnManager.GetCurrentPlayer();

        // Assert
        Assert.Null(currentPlayer);
    }

    [Fact]
    public void GetNextPlayer_WithPlayers_ReturnsCurrentAndKeepsOrder()
    {
        // Arrange
        var turnManager = new TurnManager();
        var playerIds = new List<long> { 1, 2, 3 };
        turnManager.InitializeQueue(playerIds);

        // Act
        var first = turnManager.GetCurrentPlayer();
        var next = turnManager.GetNextPlayer();

        // Assert
        Assert.NotNull(first);
        Assert.NotNull(next);
        Assert.Equal(first.Value, next.Value); // GetNextPlayer returns current player
    }

    [Fact]
    public void GetNextPlayer_EmptyQueue_ReturnsNull()
    {
        // Arrange
        var turnManager = new TurnManager();
        turnManager.InitializeQueue(new List<long>());

        // Act
        var nextPlayer = turnManager.GetNextPlayer();

        // Assert
        Assert.Null(nextPlayer);
    }

    [Fact]
    public void HasPlayers_WithPlayers_ReturnsTrue()
    {
        // Arrange
        var turnManager = new TurnManager();
        turnManager.InitializeQueue(new List<long> { 1, 2, 3 });

        // Act
        var hasPlayers = turnManager.HasPlayers();

        // Assert
        Assert.True(hasPlayers);
    }

    [Fact]
    public void HasPlayers_EmptyQueue_ReturnsFalse()
    {
        // Arrange
        var turnManager = new TurnManager();
        turnManager.InitializeQueue(new List<long>());

        // Act
        var hasPlayers = turnManager.HasPlayers();

        // Assert
        Assert.False(hasPlayers);
    }

    [Fact]
    public void Reset_WithNewPlayers_ReinitializesQueue()
    {
        // Arrange
        var turnManager = new TurnManager();
        turnManager.InitializeQueue(new List<long> { 1, 2, 3 });

        // Act
        turnManager.Reset(new List<long> { 4, 5, 6 });

        // Assert
        Assert.True(turnManager.HasPlayers());
        var currentPlayer = turnManager.GetCurrentPlayer();
        Assert.NotNull(currentPlayer);
        Assert.Contains(currentPlayer.Value, new List<long> { 4, 5, 6 });
    }

    [Fact]
    public void Reset_WithEmptyList_CreatesEmptyQueue()
    {
        // Arrange
        var turnManager = new TurnManager();
        turnManager.InitializeQueue(new List<long> { 1, 2, 3 });

        // Act
        turnManager.Reset(new List<long>());

        // Assert
        Assert.False(turnManager.HasPlayers());
        Assert.Null(turnManager.GetCurrentPlayer());
    }

    [Fact]
    public void GetNextPlayer_AdvancesThroughAllPlayers()
    {
        // Arrange
        var turnManager = new TurnManager();
        var playerIds = new List<long> { 1, 2, 3 };
        turnManager.InitializeQueue(playerIds);

        // Act - GetNextPlayer multiple times
        var results = new HashSet<long>();
        for (int i = 0; i < playerIds.Count; i++)
        {
            var player = turnManager.GetNextPlayer();
            if (player.HasValue)
            {
                results.Add(player.Value);
            }
        }

        // Assert - All players should be returned
        Assert.Equal(playerIds.Count, results.Count);
        foreach (var id in playerIds)
        {
            Assert.Contains(id, results);
        }
    }
}
