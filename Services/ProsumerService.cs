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
        return await _prosumers
            .Find(p => p.IsAvailable && p.AvailableEnergyKw > 0)
            .ToListAsync();
    }

    public async Task<Prosumer?> GetByIdAsync(string nic)
    {
        return await _prosumers
            .Find(p => p.NIC == nic)
            .FirstOrDefaultAsync();
    }

    public async Task<Prosumer> CreateAsync(Prosumer prosumer)
    {
        prosumer.CreatedAt = DateTime.UtcNow;
        prosumer.IsAvailable = true;

        await _prosumers.InsertOneAsync(prosumer);
        return prosumer;
    }

    public async Task<bool> UpdateAsync(string nic, Prosumer updatedProsumer)
    {
        var existing = await GetByIdAsync(nic);

        if (existing == null)
        {
            return false;
        }

        updatedProsumer.NIC = nic;
        updatedProsumer.CreatedAt = existing.CreatedAt;

        var result = await _prosumers.ReplaceOneAsync(
            p => p.NIC == nic,
            updatedProsumer);

        return result.IsAcknowledged && result.ModifiedCount > 0;
    }

    public async Task<bool> DeactivateAsync(string nic)
    {
        var updateDefinition = Builders<Prosumer>.Update
            .Set(p => p.IsAvailable, false)
            .Set(p => p.AvailableEnergyKw, 0);

        var result = await _prosumers.UpdateOneAsync(
            p => p.NIC == nic,
            updateDefinition);

        return result.IsAcknowledged && result.ModifiedCount > 0;
    }
    public async Task<bool> ReactivateAsync(string nic)
    {
        var updateDefinition = Builders<Prosumer>.Update
            .Set(p => p.IsAvailable, true);

        var result = await _prosumers.UpdateOneAsync(
            p => p.NIC == nic,
            updateDefinition);

        return result.IsAcknowledged && result.ModifiedCount > 0;
    }

    public async Task<bool> DeleteAsync(string nic)
    {
        var result = await _prosumers.DeleteOneAsync(
            p => p.NIC == nic);

        return result.IsAcknowledged && result.DeletedCount > 0;
    }
}