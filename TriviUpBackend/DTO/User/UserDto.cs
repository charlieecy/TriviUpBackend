namespace TriviUpBackend.DTO.User;

public record UserDto(
    long Id,
    
    string Username,
    
    string Email,
    
    string Role,
    
    DateTime CreatedAt
);