using TaskFlow.Api.Domain;
using TaskFlow.Api.Infrastructure.Repositories;

namespace TaskFlow.Api.Features.Projects;

record CreateProjectRequest(string WorkspaceId, string Name, string Description, string OwnerId);
record UpdateProjectRequest(string Name, string Description, ProjectStatus Status);

public static class ProjectEndpoints
{
    public static IEndpointRouteBuilder MapProjectEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/projects").WithTags("Projects");

        group.MapGet("/", async (IProjectRepository repo) =>
        {
            var projects = await repo.GetAllAsync();
            return Results.Ok(projects);
        });

        group.MapGet("/{id}", async (string id, IProjectRepository repo) =>
        {
            var project = await repo.GetByIdAsync(id);
            return project is null ? Results.NotFound() : Results.Ok(project);
        });

        group.MapPost("/", async (CreateProjectRequest req, IProjectRepository repo) =>
        {
            var project = new Project
            {
                WorkspaceId = req.WorkspaceId,
                Name = req.Name,
                Description = req.Description,
                OwnerId = req.OwnerId
            };
            await repo.CreateAsync(project);
            return Results.Created($"/api/projects/{project.Id}", project);
        });

        group.MapPut("/{id}", async (string id, UpdateProjectRequest req, IProjectRepository repo) =>
        {
            var existing = await repo.GetByIdAsync(id);
            if (existing is null) return Results.NotFound();

            existing.Name = req.Name;
            existing.Description = req.Description;
            existing.Status = req.Status;

            await repo.UpdateAsync(id, existing);
            return Results.Ok(existing);
        });

        group.MapDelete("/{id}", async (string id, IProjectRepository repo) =>
        {
            var deleted = await repo.DeleteAsync(id);
            return deleted ? Results.NoContent() : Results.NotFound();
        });

        return app;
    }
}
