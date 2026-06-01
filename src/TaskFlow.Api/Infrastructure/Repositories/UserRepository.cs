using MongoDB.Driver;
using TaskFlow.Api.Domain;

namespace TaskFlow.Api.Infrastructure.Repositories;

public interface IUserRepository : IRepository<User>
{
    Task<User?> GetByEmailAsync(string email);
}

public class UserRepository(IMongoDatabase database)
    : MongoRepository<User>(database, "users"), IUserRepository
{
    public async Task<User?> GetByEmailAsync(string email)
    {
        var filter = Builders<User>.Filter.Eq(u => u.Email, email);
        return await Collection.Find(filter).FirstOrDefaultAsync();
    }
}
