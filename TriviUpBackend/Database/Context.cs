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
            PasswordHash = "$2a$12$1/N3ZlYzumBVj/ER32yoWOETCZixGGIVFKR9aQBF2qwvjim0fj/QW",
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
            PasswordHash = "$2a$12$FL6hv5d1QI1wAYn61xdbGeqPH5q8tlPhTdElOH1Z0vi3wt2YvGWgi",
            Role = UserRoles.USER,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        modelBuilder.Entity<User>().HasData(adminUser, normalUser);
    }
}