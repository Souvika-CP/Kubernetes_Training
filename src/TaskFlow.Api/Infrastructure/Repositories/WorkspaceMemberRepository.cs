using MongoDB.Driver;
using TaskFlow.Api.Domain;

namespace TaskFlow.Api.Infrastructure.Repositories;

public interface IWorkspaceMemberRepository : IRepository<WorkspaceMember>
{
    Task<bool> IsMemberAsync(string workspaceId, string userId);
    Task<WorkspaceMember?> GetMembershipAsync(string workspaceId, string userId);
    Task<IEnumerable<WorkspaceMember>> GetByWorkspaceIdAsync(string workspaceId);
    Task<IEnumerable<string>> GetWorkspaceIdsForUserAsync(string userId);
}

public class WorkspaceMemberRepository(IMongoDatabase database)
    : MongoRepository<WorkspaceMember>(database, "workspace_members"), IWorkspaceMemberRepository
{
    public async Task<bool> IsMemberAsync(string workspaceId, string userId)
    {
        var filter = Builders<WorkspaceMember>.Filter.And(
            Builders<WorkspaceMember>.Filter.Eq(m => m.WorkspaceId, workspaceId),
            Builders<WorkspaceMember>.Filter.Eq(m => m.UserId, userId));
        return await Collection.CountDocumentsAsync(filter) > 0;
    }

    public async Task<WorkspaceMember?> GetMembershipAsync(string workspaceId, string userId)
    {
        var filter = Builders<WorkspaceMember>.Filter.And(
            Builders<WorkspaceMember>.Filter.Eq(m => m.WorkspaceId, workspaceId),
            Builders<WorkspaceMember>.Filter.Eq(m => m.UserId, userId));
        return await Collection.Find(filter).FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<WorkspaceMember>> GetByWorkspaceIdAsync(string workspaceId)
    {
        var filter = Builders<WorkspaceMember>.Filter.Eq(m => m.WorkspaceId, workspaceId);
        return await Collection.Find(filter).ToListAsync();
    }

    public async Task<IEnumerable<string>> GetWorkspaceIdsForUserAsync(string userId)
    {
        var filter = Builders<WorkspaceMember>.Filter.Eq(m => m.UserId, userId);
        var memberships = await Collection.Find(filter).ToListAsync();
        return memberships.Select(m => m.WorkspaceId);
    }
}
