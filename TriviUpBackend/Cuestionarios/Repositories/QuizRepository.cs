using Microsoft.EntityFrameworkCore;
using TriviUpBackend.Cuestionarios.Entities;
using TriviUpBackend.Database;

namespace TriviUpBackend.Cuestionarios.Repositories;

public class QuizRepository(
    Context context,
    ILogger<QuizRepository> logger
) : IQuizRepository
{
    public async Task<Quiz?> FindByIdAsync(long id)
    {
        return await context.Quizzes.FindAsync(id);
    }

    public async Task<Quiz?> FindByGameCodeAsync(string gameCode)
    {
        return await context.Quizzes
            .FirstOrDefaultAsync(q => q.GameCode == gameCode);
    }

    public async Task<Quiz?> FindByIdWithQuestionsAsync(long id)
    {
        return await context.Quizzes
            .Include(q => q.Preguntas)
                .ThenInclude(p => p.Respuestas)
            .FirstOrDefaultAsync(q => q.Id == id);
    }

    public async Task<Quiz?> FindByQuestionIdAsync(long questionId)
    {
        return await context.Quizzes
            .Include(q => q.Preguntas)
                .ThenInclude(p => p.Respuestas)
            .FirstOrDefaultAsync(q => q.Preguntas.Any(p => p.Id == questionId));
    }

    public async Task<IEnumerable<Quiz>> FindAllAsync(int page = 1, int pageSize = 10)
    {
        return await context.Quizzes
            .Include(q => q.Preguntas)
            .OrderByDescending(q => q.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<IEnumerable<Quiz>> FindByCreatorIdAsync(long creatorId)
    {
        return await context.Quizzes
            .Include(q => q.Preguntas)
            .Where(q => q.CreatorId == creatorId)
            .OrderByDescending(q => q.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<Quiz>> FindPublicQuizzesAsync(string? search, int page, int pageSize)
    {
        var query = context.Quizzes
            .Include(q => q.Creator)
            .Include(q => q.Preguntas)
            .Where(q => q.EsPublico);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchTrimmed = search.Trim();
            query = query.Where(q => q.Nombre.Contains(searchTrimmed));
        }

        return await query
            .OrderByDescending(q => q.Visitas)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<int> GetPublicQuizzesCountAsync(string? search)
    {
        var query = context.Quizzes.Where(q => q.EsPublico);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchTrimmed = search.Trim();
            query = query.Where(q => q.Nombre.Contains(searchTrimmed));
        }

        return await query.CountAsync();
    }

    public async Task<Quiz?> FindPublicByIdAsync(long id)
    {
        return await context.Quizzes
            .Include(q => q.Creator)
            .Include(q => q.Preguntas)
            .FirstOrDefaultAsync(q => q.Id == id && q.EsPublico);
    }

    public async Task<Quiz> IncrementVisitasAsync(long id)
    {
        var quiz = await FindByIdAsync(id);
        if (quiz == null)
        {
            throw new InvalidOperationException($"Quiz with ID {id} not found");
        }

        quiz.Visitas++;
        await context.SaveChangesAsync();
        logger.LogInformation("Quiz {Id} visitas incremented to {Visitas}", id, quiz.Visitas);
        return quiz;
    }

    public async Task<Quiz> IncrementLikesAsync(long id)
    {
        var quiz = await FindByIdAsync(id);
        if (quiz == null)
        {
            throw new InvalidOperationException($"Quiz with ID {id} not found");
        }

        quiz.Likes++;
        await context.SaveChangesAsync();
        logger.LogInformation("Quiz {Id} likes incremented to {Likes}", id, quiz.Likes);
        return quiz;
    }

    public async Task<Quiz> DecrementLikesAsync(long id)
    {
        var quiz = await FindByIdAsync(id);
        if (quiz == null)
        {
            throw new InvalidOperationException($"Quiz with ID {id} not found");
        }

        if (quiz.Likes > 0)
        {
            quiz.Likes--;
        }
        await context.SaveChangesAsync();
        logger.LogInformation("Quiz {Id} likes decremented to {Likes}", id, quiz.Likes);
        return quiz;
    }

    public async Task<Quiz> SaveAsync(Quiz quiz)
    {
        context.Quizzes.Add(quiz);
        await context.SaveChangesAsync();
        logger.LogInformation("Quiz creado con ID: {Id}, GameCode: {GameCode}", quiz.Id, quiz.GameCode);
        return quiz;
    }

    public async Task<Quiz> UpdateAsync(Quiz quiz)
    {
        context.Quizzes.Update(quiz);
        await context.SaveChangesAsync();
        logger.LogInformation("Quiz actualizado con ID: {Id}", quiz.Id);
        return quiz;
    }

    public async Task DeleteAsync(long id)
    {
        var quiz = await FindByIdAsync(id);
        if (quiz is not null)
        {
            context.Quizzes.Remove(quiz);
            await context.SaveChangesAsync();
            logger.LogInformation("Quiz eliminado con ID: {Id}", id);
        }
    }

    public async Task<int> GetTotalCountAsync()
    {
        return await context.Quizzes.CountAsync();
    }

    public async Task<List<Pregunta>> GetQuestionsWithAnswersAsync(long quizId)
    {
        return await context.Preguntas
            .Where(p => p.QuizId == quizId)
            .Include(p => p.Respuestas)
            .OrderBy(p => p.NumeroPregunta)
            .ToListAsync();
    }
}
