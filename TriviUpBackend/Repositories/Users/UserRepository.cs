using Microsoft.EntityFrameworkCore;
using TriviUpBackend.Database;
using TriviUpBackend.Models.Auth;

namespace TriviUpBackend.Repositories.Users;

public class UserRepository(
    Context context,
    ILogger<UserRepository> logger
) : IUserRepository
{
    public async Task<User?> FindByIdAsync(long id)
    {
        return await context.Users.FindAsync(id);
    }
    
    public async Task<User?> FindByUsernameAsync(string username)
    {
        return await context.Users.FirstOrDefaultAsync(u => u.Username == username);
    }
    
    public async Task<User?> FindByEmailAsync(string email)
    {
        return await context.Users.FirstOrDefaultAsync(u => u.Email == email);
    }

    public async Task<User?> FindByGoogleIdAsync(string googleId)
    {
        return await context.Users.FirstOrDefaultAsync(u => u.GoogleId == googleId);
    }
    
    public async Task<IEnumerable<User>> FindAllAsync()
    {
        return await context.Users.ToListAsync();
    }
    
    public async Task<User> SaveAsync(User user)
    {
        context.Users.Add(user);
        await context.SaveChangesAsync();
        logger.LogInformation("Usuario creado con ID: {Id}", user.Id);
        return user;
    }
    
    public async Task<User> UpdateAsync(User user)
    {
        context.Users.Update(user);
        await context.SaveChangesAsync();
        logger.LogInformation("Usuario actualizado con ID: {Id}", user.Id);
        return user;
    }
    
    public async Task DeleteAsync(long id)
    {
        var user = await FindByIdAsync(id);
        if (user is not null)
        {
            user.IsDeleted = true;
            await context.SaveChangesAsync();
            logger.LogInformation("Usuario eliminado con ID: {Id}", id);
        }
    }
    
    public async Task<IEnumerable<User>> GetActiveUsersAsync()
    {
        logger.LogDebug("Obteniendo usuarios activos");
        return await context.Users
            .Where(u => !u.IsDeleted)
            .OrderBy(u => u.Email)
            .ToListAsync();
    }


}