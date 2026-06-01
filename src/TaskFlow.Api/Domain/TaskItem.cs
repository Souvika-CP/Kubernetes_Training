using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace TaskFlow.Api.Domain;

public class TaskItem
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; init; } = ObjectId.GenerateNewId().ToString();

    [BsonElement("projectId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string ProjectId { get; set; } = string.Empty;

    [BsonElement("title")]
    public string Title { get; set; } = string.Empty;

    [BsonElement("description")]
    public string Description { get; set; } = string.Empty;

    [BsonElement("status")]
    [BsonRepresentation(BsonType.String)]
    public TaskItemStatus Status { get; set; } = TaskItemStatus.Todo;

    [BsonElement("priority")]
    [BsonRepresentation(BsonType.String)]
    public TaskPriority Priority { get; set; } = TaskPriority.Medium;

    [BsonElement("assigneeId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? AssigneeId { get; set; }

    [BsonElement("dueDate")]
    public DateTime? DueDate { get; set; }

    [BsonElement("tags")]
    public List<string> Tags { get; set; } = [];

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
