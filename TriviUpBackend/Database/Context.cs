using Microsoft.EntityFrameworkCore;
using TriviUpBackend.Data;
using TriviUpBackend.Models.Auth;
using TriviUpBackend.Cuestionarios.Entities;

namespace TriviUpBackend.Database;

public class Context(DbContextOptions options) : DbContext(options)
{
    public DbSet<User> Users { get; set; } = null!;
    public DbSet<Quiz> Quizzes { get; set; } = null!;
    public DbSet<Pregunta> Preguntas { get; set; } = null!;
    public DbSet<Respuesta> Respuestas { get; set; } = null!;
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasQueryFilter(u => !u.IsDeleted);
            entity.ConfigureTimestamps();
            entity.HasIndex(u => u.GoogleId).IsUnique();
        });

        modelBuilder.Entity<Quiz>(entity =>
        {
            entity.ConfigureTimestamps();
            entity.HasIndex(q => q.GameCode).IsUnique();
            entity.HasMany(q => q.Preguntas)
                .WithOne(p => p.Quiz)
                .HasForeignKey(p => p.QuizId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Pregunta>(entity =>
        {
            entity.ConfigureTimestamps();
            entity.HasMany(p => p.Respuestas)
                .WithOne(r => r.Pregunta)
                .HasForeignKey(r => r.PreguntaId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Respuesta>(entity =>
        {
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

        var testUser = new User
        {
            Id = 3,
            Username = "testuser",
            Email = "test@test.com",
            PasswordHash = "$2a$11$DhWxVW/CPdqAxqo.LPDcCeDwFoEpCoi0vy.7ZPYxbOrpDeOaNZrFu",
            Role = UserRoles.USER,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        modelBuilder.Entity<User>().HasData(adminUser, normalUser, testUser);
    }
}