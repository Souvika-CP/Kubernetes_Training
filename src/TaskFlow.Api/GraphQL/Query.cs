using HotChocolate;
using HotChocolate.Data;
using TaskFlow.Api.Domain;
using TaskFlow.Api.Infrastructure;
using TaskFlow.Api.Infrastructure.Repositories;

namespace TaskFlow.Api.GraphQL;

public class Query
{
    // Returns only workspaces the authenticated user is a member of.
    [UseFiltering]
    [UseSorting]
    public async Task<IEnumerable<Workspace>> GetWorkspaces(
        [Service] IWorkspaceRepository repo,
        [Service] IWorkspaceMemberRepository memberRepo,
        [Service] ICurrentUserService currentUser)
    {
        var workspaceIds = await memberRepo.GetWorkspaceIdsForUserAsync(currentUser.UserId);
        var all = await repo.GetAllAsync();
        return all.Where(w => workspaceIds.Contains(w.Id));
    }

    public async Task<Workspace?> GetWorkspace(
        string id,
        [Service] IWorkspaceRepository repo,
        [Service] IWorkspaceMemberRepository memberRepo,
        [Service] ICurrentUserService currentUser)
    {
        if (!await memberRepo.IsMemberAsync(id, currentUser.UserId))
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage($"Workspace '{id}' not found or access denied.")
                    .SetCode("FORBIDDEN")
                    .Build());

        return await repo.GetByIdAsync(id);
    }

    [UseFiltering]
    [UseSorting]
    public async Task<IEnumerable<Project>> GetProjects(
        string workspaceId,
        [Service] IProjectRepository repo,
        [Service] IWorkspaceMemberRepository memberRepo,
        [Service] ICurrentUserService currentUser)
    {
        if (!await memberRepo.IsMemberAsync(workspaceId, currentUser.UserId))
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Access denied to this workspace.")
                    .SetCode("FORBIDDEN")
                    .Build());

        return await repo.GetByWorkspaceIdAsync(workspaceId);
    }

    public async Task<Project?> GetProject(
        string id,
        [Service] IProjectRepository repo,
        [Service] IWorkspaceMemberRepository memberRepo,
        [Service] ICurrentUserService currentUser)
    {
        var project = await repo.GetByIdAsync(id);
        if (project is null) return null;

        if (!await memberRepo.IsMemberAsync(project.WorkspaceId, currentUser.UserId))
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Access denied to this project.")
                    .SetCode("FORBIDDEN")
                    .Build());

        return project;
    }

    [UseFiltering]
    [UseSorting]
    public async Task<IEnumerable<TaskItem>> GetTasks(
        string projectId,
        [Service] ITaskRepository taskRepo,
        [Service] IProjectRepository projectRepo,
        [Service] IWorkspaceMemberRepository memberRepo,
        [Service] ICurrentUserService currentUser)
    {
        var project = await projectRepo.GetByIdAsync(projectId);
        if (project is null) return [];

        if (!await memberRepo.IsMemberAsync(project.WorkspaceId, currentUser.UserId))
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Access denied to this project's tasks.")
                    .SetCode("FORBIDDEN")
                    .Build());

        return await taskRepo.GetByProjectIdAsync(projectId);
    }

    public async Task<TaskItem?> GetTask(
        string id,
        [Service] ITaskRepository repo)
        => await repo.GetByIdAsync(id);
}
