namespace TriviUpBackend.DTO.User;

public record AuthResponseDto(
    string Token,
    UserDto User
);