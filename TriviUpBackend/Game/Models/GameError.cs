namespace TriviUpBackend.Game.Models;

public enum GameError
{
    RoomNotFound,
    RoomFull,
    AlreadyJoined,
    NotOwner,
    NotYourTurn,
    GameAlreadyStarted,
    GameNotStarted,
    InvalidAnswer,
    ConnectionLost,
    MinPlayersNotReached
}
