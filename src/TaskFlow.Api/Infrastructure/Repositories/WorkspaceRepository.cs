using MongoDB.Driver;
using TaskFlow.Api.Domain;

namespace TaskFlow.Api.Infrastructure.Repositories;

public interface IWorkspaceRepository : IRepository<Workspace> { }

public class WorkspaceRepository(IMongoDatabase database)
    : MongoRepository<Workspace>(database, "workspaces"), IWorkspaceRepository { }
