using TriviUpBackend.Models.Auth;

namespace TriviUpBackend.Repositories.Users;

public interface IUserRepository
{
    Task<User?> FindByIdAsync(long id);

    Task<User?> FindByUsernameAsync(string username);

    Task<User?> FindByEmailAsync(string email);

    Task<User?> FindByGoogleIdAsync(string googleId);
    
    Task<IEnumerable<User>> FindAllAsync();
    
    Task<User> SaveAsync(User user);
    
    Task<User> UpdateAsync(User user);
    
    Task DeleteAsync(long id);
    
    Task<IEnumerable<User>> GetActiveUsersAsync();
}