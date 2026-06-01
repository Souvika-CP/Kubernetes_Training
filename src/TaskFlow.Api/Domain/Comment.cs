using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace TaskFlow.Api.Domain;

public class Comment
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; init; } = ObjectId.GenerateNewId().ToString();

    [BsonElement("taskId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string TaskId { get; set; } = string.Empty;

    [BsonElement("body")]
    public string Body { get; set; } = string.Empty;

    [BsonElement("authorId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string AuthorId { get; set; } = string.Empty;

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}
