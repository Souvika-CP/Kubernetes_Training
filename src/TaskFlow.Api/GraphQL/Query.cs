using HotChocolate;
using HotChocolate.Data;
using TaskFlow.Api.Domain;
using TaskFlow.Api.Infrastructure.Repositories;

namespace TaskFlow.Api.GraphQL;

public class Query
{
    [UseFiltering]
    [UseSorting]
    public async Task<IEnumerable<Workspace>> GetWorkspaces(
        [Service] IWorkspaceRepository repo)
        => await repo.GetAllAsync();

    public async Task<Workspace?> GetWorkspace(
        string id,
        [Service] IWorkspaceRepository repo)
        => await repo.GetByIdAsync(id);

    [UseFiltering]
    [UseSorting]
    public async Task<IEnumerable<Project>> GetProjects(
        string workspaceId,
        [Service] IProjectRepository repo)
        => await repo.GetByWorkspaceIdAsync(workspaceId);

    public async Task<Project?> GetProject(
        string id,
        [Service] IProjectRepository repo)
        => await repo.GetByIdAsync(id);

    [UseFiltering]
    [UseSorting]
    public async Task<IEnumerable<TaskItem>> GetTasks(
        string projectId,
        [Service] ITaskRepository repo)
        => await repo.GetByProjectIdAsync(projectId);

    public async Task<TaskItem?> GetTask(
        string id,
        [Service] ITaskRepository repo)
        => await repo.GetByIdAsync(id);
}
