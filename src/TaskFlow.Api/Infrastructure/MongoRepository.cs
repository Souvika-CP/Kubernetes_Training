using MongoDB.Bson;
using MongoDB.Driver;

namespace TaskFlow.Api.Infrastructure;

public abstract class MongoRepository<T>(IMongoDatabase database, string collectionName) : IRepository<T>
{
    protected readonly IMongoCollection<T> Collection = database.GetCollection<T>(collectionName);

    public async Task<T?> GetByIdAsync(string id)
    {
        var filter = Builders<T>.Filter.Eq("_id", ObjectId.Parse(id));
        return await Collection.Find(filter).FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<T>> GetAllAsync()
    {
        return await Collection.Find(Builders<T>.Filter.Empty).ToListAsync();
    }

    public async Task<T> CreateAsync(T entity)
    {
        await Collection.InsertOneAsync(entity);
        return entity;
    }

    public async Task<T> UpdateAsync(string id, T entity)
    {
        var filter = Builders<T>.Filter.Eq("_id", ObjectId.Parse(id));
        await Collection.ReplaceOneAsync(filter, entity);
        return entity;
    }

    public async Task<bool> DeleteAsync(string id)
    {
        var filter = Builders<T>.Filter.Eq("_id", ObjectId.Parse(id));
        var result = await Collection.DeleteOneAsync(filter);
        return result.DeletedCount > 0;
    }
}
