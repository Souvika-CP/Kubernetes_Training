using MongoDB.Driver;
using TaskFlow.Api.Domain;

namespace TaskFlow.Api.Infrastructure.Repositories;

public interface IProjectRepository : IRepository<Project>
{
    Task<IEnumerable<Project>> GetByWorkspaceIdAsync(string workspaceId);
}

public class ProjectRepository(IMongoDatabase database)
    : MongoRepository<Project>(database, "projects"), IProjectRepository
{
    public async Task<IEnumerable<Project>> GetByWorkspaceIdAsync(string workspaceId)
    {
        var filter = Builders<Project>.Filter.Eq(p => p.WorkspaceId, workspaceId);
        return await Collection.Find(filter).ToListAsync();
    }
}
