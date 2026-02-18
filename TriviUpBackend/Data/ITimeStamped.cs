namespace TriviUpBackend.Data;

public interface ITimestamped
{
    DateTime CreatedAt { get; }

    DateTime UpdatedAt { get; }
}