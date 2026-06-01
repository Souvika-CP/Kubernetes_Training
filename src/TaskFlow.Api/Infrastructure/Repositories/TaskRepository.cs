using MongoDB.Driver;
using TaskFlow.Api.Domain;

namespace TaskFlow.Api.Infrastructure.Repositories;

public interface ITaskRepository : IRepository<TaskItem>
{
    Task<IEnumerable<TaskItem>> GetByProjectIdAsync(string projectId);
}

public class TaskRepository(IMongoDatabase database)
    : MongoRepository<TaskItem>(database, "tasks"), ITaskRepository
{
    public async Task<IEnumerable<TaskItem>> GetByProjectIdAsync(string projectId)
    {
        var filter = Builders<TaskItem>.Filter.Eq(t => t.ProjectId, projectId);
        return await Collection.Find(filter).ToListAsync();
    }
}
