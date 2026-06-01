using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace TaskFlow.Api.Domain;

public class Project
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; init; } = ObjectId.GenerateNewId().ToString();

    [BsonElement("workspaceId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string WorkspaceId { get; set; } = string.Empty;

    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;

    [BsonElement("description")]
    public string Description { get; set; } = string.Empty;

    [BsonElement("status")]
    [BsonRepresentation(BsonType.String)]
    public ProjectStatus Status { get; set; } = ProjectStatus.Active;

    [BsonElement("ownerId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string OwnerId { get; set; } = string.Empty;

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}
