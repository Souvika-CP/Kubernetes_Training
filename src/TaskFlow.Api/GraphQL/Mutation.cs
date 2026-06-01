using HotChocolate;
using HotChocolate.Subscriptions;
using Prometheus;
using TaskFlow.Api.Domain;
using TaskFlow.Api.Infrastructure;
using TaskFlow.Api.Infrastructure.Repositories;

namespace TaskFlow.Api.GraphQL;

// ── Input types ──────────────────────────────────────────────────────────────

public record CreateWorkspaceInput(string Name, string OwnerId);

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
    public async Task<Workspace> CreateWorkspace(
        CreateWorkspaceInput input,
        [Service] IWorkspaceRepository repo)
    {
        var workspace = new Workspace { Name = input.Name, OwnerId = input.OwnerId };
        return await repo.CreateAsync(workspace);
    }

    public async Task<Project> CreateProject(
        CreateProjectInput input,
        [Service] IProjectRepository repo)
    {
        var project = new Project
        {
            WorkspaceId = input.WorkspaceId,
            Name = input.Name,
            Description = input.Description,
            OwnerId = input.OwnerId
        };
        await repo.CreateAsync(project);
        AppMetrics.ActiveProjects.Inc();
        return project;
    }

    public async Task<Project> UpdateProject(
        UpdateProjectInput input,
        [Service] IProjectRepository repo)
    {
        var existing = await repo.GetByIdAsync(input.Id)
            ?? throw new GraphQLException($"Project '{input.Id}' not found.");

        existing.Name = input.Name;
        existing.Description = input.Description;
        existing.Status = input.Status;

        return await repo.UpdateAsync(input.Id, existing);
    }

    public async Task<bool> DeleteProject(
        string id,
        [Service] IProjectRepository repo)
    {
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
