using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace TaskFlow.Api.Domain;

public class WorkspaceMember
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; init; } = ObjectId.GenerateNewId().ToString();

    [BsonElement("workspaceId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string WorkspaceId { get; set; } = string.Empty;

    [BsonElement("userId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string UserId { get; set; } = string.Empty;

    [BsonElement("role")]
    [BsonRepresentation(BsonType.String)]
    public WorkspaceMemberRole Role { get; set; } = WorkspaceMemberRole.Viewer;

    [BsonElement("joinedAt")]
    public DateTime JoinedAt { get; init; } = DateTime.UtcNow;
}
