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
            entity.Property(q => q.EsPublico).HasDefaultValue(false);
            entity.Property(q => q.Visitas).HasDefaultValue(0);
            entity.Property(q => q.Likes).HasDefaultValue(0);
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

        // QUIZZES PÚBLICOS DE PRUEBA
        var publicQuizzes = new[]
        {
            new Quiz
            {
                Id = 1,
                Nombre = "Trivia de Historia General",
                GameCode = "HIST01",
                CreatorId = 1,
                EsPublico = true,
                Visitas = 150,
                Likes = 42,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new Quiz
            {
                Id = 2,
                Nombre = "Cultura General - Nivel Fácil",
                GameCode = "CULT02",
                CreatorId = 2,
                EsPublico = true,
                Visitas = 89,
                Likes = 23,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new Quiz
            {
                Id = 3,
                Nombre = "Ciencia y Naturaleza",
                GameCode = "SCIN03",
                CreatorId = 1,
                EsPublico = true,
                Visitas = 234,
                Likes = 67,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new Quiz
            {
                Id = 4,
                Nombre = "Geografía Mundial",
                GameCode = "GEOM04",
                CreatorId = 3,
                EsPublico = true,
                Visitas = 178,
                Likes = 51,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new Quiz
            {
                Id = 5,
                Nombre = "Entretenimiento y Cine",
                GameCode = "CINE05",
                CreatorId = 2,
                EsPublico = true,
                Visitas = 312,
                Likes = 95,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new Quiz
            {
                Id = 6,
                Nombre = "Quiz Privado de Prueba",
                GameCode = "PRIV06",
                CreatorId = 1,
                EsPublico = false,
                Visitas = 0,
                Likes = 0,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        };

        modelBuilder.Entity<Quiz>().HasData(publicQuizzes);
    }
}