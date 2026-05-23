namespace TriviUpBackend.Errors;

public record QuizError(string Error);

public record QuizNotFoundError(string Error) : QuizError(Error);

public record QuizValidationError(string Error) : QuizError(Error);

public record QuizForbiddenError(string Error) : QuizError(Error);
