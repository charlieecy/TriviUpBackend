namespace TriviUpBackend.Game.Models;

public enum GameState
{
    Waiting,      // Sala creada, esperando jugadores
    Starting,     // Partida a punto de comenzar (countdown)
    Playing,      // Partida en curso
    Finished      // Partida terminada
}
