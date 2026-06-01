using MongoDB.Driver;
using TaskFlow.Api.Domain;

namespace TaskFlow.Api.Infrastructure.Repositories;

public interface ICommentRepository : IRepository<Comment>
{
    Task<IEnumerable<Comment>> GetByTaskIdAsync(string taskId);
}

public class CommentRepository(IMongoDatabase database)
    : MongoRepository<Comment>(database, "comments"), ICommentRepository
{
    public async Task<IEnumerable<Comment>> GetByTaskIdAsync(string taskId)
    {
        var filter = Builders<Comment>.Filter.Eq(c => c.TaskId, taskId);
        return await Collection.Find(filter).ToListAsync();
    }
}
