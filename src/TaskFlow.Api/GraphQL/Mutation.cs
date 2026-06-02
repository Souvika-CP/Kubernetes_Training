using HotChocolate;
using HotChocolate.Subscriptions;
using Prometheus;
using TaskFlow.Api.Domain;
using TaskFlow.Api.Infrastructure;
using TaskFlow.Api.Infrastructure.Repositories;

namespace TaskFlow.Api.GraphQL;

// ── Input types ──────────────────────────────────────────────────────────────

public record CreateWorkspaceInput(string Name);

public record AddWorkspaceMemberInput(string WorkspaceId, string UserId, WorkspaceMemberRole Role);
public record RemoveWorkspaceMemberInput(string WorkspaceId, string UserId);

public record CreateProjectInput(string WorkspaceId, string Name, string Description, string OwnerId);
public record UpdateProjectInput(string Id, string Name, string Description, ProjectStatus Status);

public record CreateTaskInput(
    string ProjectId,
    string Title,
    string Description,
    TaskPriority Priority,
    string? AssigneeId = null,
    DateTime? DueDate = null,
    List<string>? Tags = null);

public record UpdateTaskInput(
    string Id,
    string Title,
    string Description,
    TaskItemStatus Status,
    TaskPriority Priority,
    string? AssigneeId = null,
    DateTime? DueDate = null,
    List<string>? Tags = null);

public record AddCommentInput(string TaskId, string Body, string AuthorId);

// ── Mutation type ─────────────────────────────────────────────────────────────

public class Mutation
{
    // Creates a workspace and automatically adds the authenticated user as Owner.
    public async Task<Workspace> CreateWorkspace(
        CreateWorkspaceInput input,
        [Service] IWorkspaceRepository repo,
        [Service] IWorkspaceMemberRepository memberRepo,
        [Service] ICurrentUserService currentUser)
    {
        var userId = currentUser.UserId;
        var workspace = new Workspace { Name = input.Name, OwnerId = userId };
        await repo.CreateAsync(workspace);

        await memberRepo.CreateAsync(new WorkspaceMember
        {
            WorkspaceId = workspace.Id,
            UserId = userId,
            Role = WorkspaceMemberRole.Owner
        });

        return workspace;
    }

    // Adds a user to a workspace. Only the workspace Owner can do this.
    public async Task<WorkspaceMember> AddWorkspaceMember(
        AddWorkspaceMemberInput input,
        [Service] IWorkspaceMemberRepository memberRepo,
        [Service] ICurrentUserService currentUser)
    {
        var requesterId = currentUser.UserId;
        var requesterMembership = await memberRepo.GetMembershipAsync(input.WorkspaceId, requesterId);

        if (requesterMembership?.Role != WorkspaceMemberRole.Owner)
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Only workspace owners can add members.")
                    .SetCode("FORBIDDEN")
                    .Build());

        var existing = await memberRepo.GetMembershipAsync(input.WorkspaceId, input.UserId);
        if (existing is not null)
            throw new GraphQLException($"User '{input.UserId}' is already a member of this workspace.");

        var member = new WorkspaceMember
        {
            WorkspaceId = input.WorkspaceId,
            UserId = input.UserId,
            Role = input.Role
        };
        return await memberRepo.CreateAsync(member);
    }

    // Removes a member from a workspace. Only the workspace Owner can do this.
    // An owner cannot remove themselves (workspace must always have an owner).
    public async Task<bool> RemoveWorkspaceMember(
        RemoveWorkspaceMemberInput input,
        [Service] IWorkspaceMemberRepository memberRepo,
        [Service] ICurrentUserService currentUser)
    {
        var requesterId = currentUser.UserId;

        if (input.UserId == requesterId)
            throw new GraphQLException("You cannot remove yourself from a workspace.");

        var requesterMembership = await memberRepo.GetMembershipAsync(input.WorkspaceId, requesterId);
        if (requesterMembership?.Role != WorkspaceMemberRole.Owner)
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Only workspace owners can remove members.")
                    .SetCode("FORBIDDEN")
                    .Build());

        var target = await memberRepo.GetMembershipAsync(input.WorkspaceId, input.UserId);
        if (target is null) return false;

        return await memberRepo.DeleteAsync(target.Id);
    }

    public async Task<IEnumerable<WorkspaceMember>> GetWorkspaceMembers(
        string workspaceId,
        [Service] IWorkspaceMemberRepository memberRepo,
        [Service] ICurrentUserService currentUser)
    {
        if (!await memberRepo.IsMemberAsync(workspaceId, currentUser.UserId))
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Access denied to this workspace.")
                    .SetCode("FORBIDDEN")
                    .Build());

        return await memberRepo.GetByWorkspaceIdAsync(workspaceId);
    }

    public async Task<Project> CreateProject(
        CreateProjectInput input,
        [Service] IProjectRepository repo,
        [Service] IWorkspaceMemberRepository memberRepo,
        [Service] ICurrentUserService currentUser)
    {
        if (!await memberRepo.IsMemberAsync(input.WorkspaceId, currentUser.UserId))
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Access denied to this workspace.")
                    .SetCode("FORBIDDEN")
                    .Build());

        var project = new Project
        {
            WorkspaceId = input.WorkspaceId,
            Name = input.Name,
            Description = input.Description,
            OwnerId = currentUser.UserId
        };
        await repo.CreateAsync(project);
        AppMetrics.ActiveProjects.Inc();
        return project;
    }

    public async Task<Project> UpdateProject(
        UpdateProjectInput input,
        [Service] IProjectRepository repo,
        [Service] IWorkspaceMemberRepository memberRepo,
        [Service] ICurrentUserService currentUser)
    {
        var existing = await repo.GetByIdAsync(input.Id)
            ?? throw new GraphQLException($"Project '{input.Id}' not found.");

        if (!await memberRepo.IsMemberAsync(existing.WorkspaceId, currentUser.UserId))
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Access denied to this workspace.")
                    .SetCode("FORBIDDEN")
                    .Build());

        existing.Name = input.Name;
        existing.Description = input.Description;
        existing.Status = input.Status;

        return await repo.UpdateAsync(input.Id, existing);
    }

    public async Task<bool> DeleteProject(
        string id,
        [Service] IProjectRepository repo,
        [Service] IWorkspaceMemberRepository memberRepo,
        [Service] ICurrentUserService currentUser)
    {
        var existing = await repo.GetByIdAsync(id);
        if (existing is null) return false;

        if (!await memberRepo.IsMemberAsync(existing.WorkspaceId, currentUser.UserId))
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Access denied to this workspace.")
                    .SetCode("FORBIDDEN")
                    .Build());

        var result = await repo.DeleteAsync(id);
        if (result) AppMetrics.ActiveProjects.Dec();
        return result;
    }

    public async Task<TaskItem> CreateTask(
        CreateTaskInput input,
        [Service] ITaskRepository repo,
        [Service] ITopicEventSender sender)
    {
        using var timer = AppMetrics.GraphQlRequestDuration.NewTimer();
        var task = new TaskItem
        {
            ProjectId = input.ProjectId,
            Title = input.Title,
            Description = input.Description,
            Priority = input.Priority,
            AssigneeId = input.AssigneeId,
            DueDate = input.DueDate,
            Tags = input.Tags ?? []
        };
        await repo.CreateAsync(task);
        await sender.SendAsync($"taskUpdated_{task.ProjectId}", task);
        AppMetrics.TasksCreated.Inc();
        return task;
    }

    public async Task<TaskItem> UpdateTask(
        UpdateTaskInput input,
        [Service] ITaskRepository repo,
        [Service] ITopicEventSender sender)
    {
        var existing = await repo.GetByIdAsync(input.Id)
            ?? throw new GraphQLException($"Task '{input.Id}' not found.");

        existing.Title = input.Title;
        existing.Description = input.Description;
        existing.Status = input.Status;
        existing.Priority = input.Priority;
        existing.AssigneeId = input.AssigneeId;
        existing.DueDate = input.DueDate;
        existing.Tags = input.Tags ?? [];
        existing.UpdatedAt = DateTime.UtcNow;

        await repo.UpdateAsync(input.Id, existing);
        await sender.SendAsync($"taskUpdated_{existing.ProjectId}", existing);
        return existing;
    }

    public async Task<bool> DeleteTask(
        string id,
        [Service] ITaskRepository repo)
        => await repo.DeleteAsync(id);

    public async Task<Comment> AddComment(
        AddCommentInput input,
        [Service] ICommentRepository repo,
        [Service] ITopicEventSender sender)
    {
        var comment = new Comment
        {
            TaskId = input.TaskId,
            Body = input.Body,
            AuthorId = input.AuthorId
        };
        await repo.CreateAsync(comment);
        await sender.SendAsync($"commentAdded_{comment.TaskId}", comment);
        return comment;
    }
}
