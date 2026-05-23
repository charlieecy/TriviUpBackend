namespace TriviUpBackend.Errors;

public record AuthError(
    string Error)
{
    public string Error { get; set; } = Error;
}

public record AuthUnauthorizedError(string Error): AuthError(Error);

public record AuthConflictError(string Error):AuthError(Error);

public record AuthValidationError(string Error): AuthError(Error);