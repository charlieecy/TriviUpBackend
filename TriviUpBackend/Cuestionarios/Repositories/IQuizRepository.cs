using TriviUpBackend.Cuestionarios.Entities;

namespace TriviUpBackend.Cuestionarios.Repositories;

public interface IQuizRepository
{
    Task<Quiz?> FindByIdAsync(long id);
    
    Task<Quiz?> FindByGameCodeAsync(string gameCode);
    
    Task<Quiz?> FindByIdWithQuestionsAsync(long id);
    
    Task<IEnumerable<Quiz>> FindAllAsync(int page = 1, int pageSize = 10);
    
    Task<IEnumerable<Quiz>> FindByCreatorIdAsync(long creatorId);
    
    Task<Quiz> SaveAsync(Quiz quiz);
    
    Task<Quiz> UpdateAsync(Quiz quiz);
    
    Task DeleteAsync(long id);
    
    Task<int> GetTotalCountAsync();
}
