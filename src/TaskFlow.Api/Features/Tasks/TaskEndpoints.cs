using TaskFlow.Api.Domain;
using TaskFlow.Api.Infrastructure.Repositories;

namespace TaskFlow.Api.Features.Tasks;

record CreateTaskRequest(
    string ProjectId,
    string Title,
    string Description,
    TaskPriority Priority,
    string? AssigneeId,
    DateTime? DueDate,
    List<string>? Tags);

record UpdateTaskRequest(
    string Title,
    string Description,
    TaskItemStatus Status,
    TaskPriority Priority,
    string? AssigneeId,
    DateTime? DueDate,
    List<string>? Tags);

public static class TaskEndpoints
{
    public static IEndpointRouteBuilder MapTaskEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/tasks").WithTags("Tasks");

        group.MapGet("/", async (ITaskRepository repo, string? projectId) =>
        {
            var tasks = projectId is not null
                ? await repo.GetByProjectIdAsync(projectId)
                : await repo.GetAllAsync();
            return Results.Ok(tasks);
        });

        group.MapGet("/{id}", async (string id, ITaskRepository repo) =>
        {
            var task = await repo.GetByIdAsync(id);
            return task is null ? Results.NotFound() : Results.Ok(task);
        });

        group.MapPost("/", async (CreateTaskRequest req, ITaskRepository repo) =>
        {
            var task = new TaskItem
            {
                ProjectId = req.ProjectId,
                Title = req.Title,
                Description = req.Description,
                Priority = req.Priority,
                AssigneeId = req.AssigneeId,
                DueDate = req.DueDate,
                Tags = req.Tags ?? []
            };
            await repo.CreateAsync(task);
            return Results.Created($"/api/tasks/{task.Id}", task);
        });

        group.MapPut("/{id}", async (string id, UpdateTaskRequest req, ITaskRepository repo) =>
        {
            var existing = await repo.GetByIdAsync(id);
            if (existing is null) return Results.NotFound();

            existing.Title = req.Title;
            existing.Description = req.Description;
            existing.Status = req.Status;
            existing.Priority = req.Priority;
            existing.AssigneeId = req.AssigneeId;
            existing.DueDate = req.DueDate;
            existing.Tags = req.Tags ?? [];
            existing.UpdatedAt = DateTime.UtcNow;

            await repo.UpdateAsync(id, existing);
            return Results.Ok(existing);
        });

        group.MapDelete("/{id}", async (string id, ITaskRepository repo) =>
        {
            var deleted = await repo.DeleteAsync(id);
            return deleted ? Results.NoContent() : Results.NotFound();
        });

        return app;
    }
}
