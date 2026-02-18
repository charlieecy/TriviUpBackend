namespace TriviUpBackend.Errors;

public record AuthError(
    string Error)
{
    public string Error { get; set; } = Error;
}

public record UnauthorizedError(string Error): AuthError(Error);

public record ConflictError(string Error):AuthError(Error);

public record ValidationError(string Error): AuthError(Error);