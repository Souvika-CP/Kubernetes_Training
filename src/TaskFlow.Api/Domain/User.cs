using HotChocolate;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace TaskFlow.Api.Domain;

public class User
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; init; } = ObjectId.GenerateNewId().ToString();

    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;

    [BsonElement("email")]
    public string Email { get; set; } = string.Empty;

    [BsonElement("passwordHash")]
    [GraphQLIgnore]
    public string PasswordHash { get; set; } = string.Empty;

    [BsonElement("role")]
    [BsonRepresentation(BsonType.String)]
    public UserRole Role { get; set; } = UserRole.Member;

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}
