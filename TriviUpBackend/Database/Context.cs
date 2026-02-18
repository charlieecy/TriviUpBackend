using Microsoft.EntityFrameworkCore;
using TriviUpBackend.Data;
using TriviUpBackend.Models.Auth;

namespace TriviUpBackend.Database;

public class Context(DbContextOptions options) : DbContext(options)
{
    public DbSet<User> Users { get; set; } = null!;
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<User>(entity =>
        {
            //Filtro para ignorar los usuarios eliminados (soft delete)
            entity.HasQueryFilter(u => !u.IsDeleted);
            //Por ser un método de extensión personalizado
            entity.ConfigureTimestamps();
        });
        
        SeedData(modelBuilder); // Llamamos al metodo para poblar la BD
    }

    private static void SeedData(ModelBuilder modelBuilder)
    {
        //USUARIOS
        var adminUser = new User
        {
            Id = 1,
            Username = "admin",
            Email = "admin@funkoapi.com",
            PasswordHash = "$2a$12$n1uTaycq1Cq5uwwHCMSqa.dUDZZ3rU4B6.vZPDov4QJiCBgGvCcMy",
            Role = UserRoles.ADMIN,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var normalUser = new User
        {
            Id = 2,
            Username = "user",
            Email = "user@funkoapi.com",
            PasswordHash = "$2a$12$Vp5ZpZik9vTjMMLRblbDKu93ct9qZEK/3zMKdOrE7JBdFBBJEogGy",
            Role = UserRoles.USER,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        modelBuilder.Entity<User>().HasData(adminUser, normalUser);
    }
}