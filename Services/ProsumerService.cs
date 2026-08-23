using MongoDB.Driver;
using SolarAPI.Models;

namespace SolarAPI.Services;

public class ProsumerService : IProsumerService
{
    private readonly IMongoCollection<Prosumer> _prosumers;

    public ProsumerService(IMongoDatabase database)
    {
        _prosumers = database.GetCollection<Prosumer>("Prosumers");
    }

    public async Task<IEnumerable<Prosumer>> GetAllAsync()
    {
        return await _prosumers.Find(_ => true).ToListAsync();
    }

    public async Task<IEnumerable<Prosumer>> GetAvailableAsync()
    {
        return await _prosumers.Find(p => p.IsAvailable && p.AvailableEnergyKw > 0).ToListAsync();
    }

    public async Task<Prosumer?> GetByIdAsync(string id)
    {
        return await _prosumers.Find(p => p.Id == id).FirstOrDefaultAsync();
    }

    public async Task<Prosumer> CreateAsync(Prosumer prosumer)
    {
        // Business logic: enforce clean creation state
        prosumer.Id = null; // Let MongoDB generate the ObjectId
        prosumer.CreatedAt = DateTime.UtcNow;
        prosumer.IsAvailable = true;

        await _prosumers.InsertOneAsync(prosumer);
        return prosumer;
    }

    public async Task<bool> UpdateAsync(string id, Prosumer updatedProsumer)
    {
        // Business logic: verify existence before update
        var existing = await GetByIdAsync(id);
        if (existing == null)
        {
            return false;
        }

        updatedProsumer.Id = id;
        updatedProsumer.CreatedAt = existing.CreatedAt; // Preserve original creation timestamp

        var result = await _prosumers.ReplaceOneAsync(p => p.Id == id, updatedProsumer);
        return result.IsAcknowledged && result.ModifiedCount > 0;
    }

    public async Task<bool> DeactivateAsync(string id)
    {
        // Business logic: set availability to false and zero out active available energy offer
        var updateDefinition = Builders<Prosumer>.Update
            .Set(p => p.IsAvailable, false)
            .Set(p => p.AvailableEnergyKw, 0);

        var result = await _prosumers.UpdateOneAsync(p => p.Id == id, updateDefinition);
        return result.IsAcknowledged && result.ModifiedCount > 0;
    }

    public async Task<bool> DeleteAsync(string id)
    {
        var result = await _prosumers.DeleteOneAsync(p => p.Id == id);
        return result.IsAcknowledged && result.DeletedCount > 0;
    }
}
