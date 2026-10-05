
// Implementation of prosumer business operations, MongoDB persistence, and availability filtering.

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
        return await _prosumers
            .Find(_ => true)
            .ToListAsync();
    }

    public async Task<IEnumerable<Prosumer>> GetAvailableAsync()
    {
        return await _prosumers
            .Find(p => p.IsAvailable && p.AvailableEnergyKw > 0)
            .ToListAsync();
    }

    public async Task<Prosumer?> GetByIdAsync(string nic)
    {
        // Begin execution of GetByIdAsync method to query prosumer by NIC or ID
        if (string.IsNullOrWhiteSpace(nic))
        {
            return null;
        }

        var normalizedNic = nic.Trim();

        var builder = Builders<Prosumer>.Filter;
        var filter = builder.Eq(p => p.NIC, normalizedNic);

        if (MongoDB.Bson.ObjectId.TryParse(normalizedNic, out _))
        {
            filter |= builder.Eq(p => p.Id, normalizedNic) | builder.Eq(p => p.UserId, normalizedNic);
        }

        return await _prosumers
            .Find(filter)
            .FirstOrDefaultAsync();
    }

    public async Task<Prosumer> CreateAsync(Prosumer prosumer)
    {
        // Begin execution of CreateAsync method
        prosumer.CreatedAt = DateTime.UtcNow;
        prosumer.IsAvailable = true;

        if (string.IsNullOrWhiteSpace(prosumer.NIC) &&
            !string.IsNullOrWhiteSpace(prosumer.UserId))
        {
            prosumer.NIC = prosumer.UserId;
        }

        await _prosumers.InsertOneAsync(prosumer);

        return prosumer;
    }

    public async Task<bool> UpdateAsync(string nic, Prosumer updatedProsumer)
    {
        // Begin execution of UpdateAsync method
        var existing = await GetByIdAsync(nic);

        if (existing == null)
        {
            return false;
        }

        updatedProsumer.Id = existing.Id;
        updatedProsumer.NIC = existing.NIC;
        updatedProsumer.CreatedAt = existing.CreatedAt;

        var result = await _prosumers.ReplaceOneAsync(
            p => p.Id == existing.Id,
            updatedProsumer);

        return result.IsAcknowledged &&
               (result.ModifiedCount > 0 || result.MatchedCount > 0);
    }

    public async Task<bool> DeactivateAsync(string nic)
    {
        // Begin execution of DeactivateAsync method
        var existing = await GetByIdAsync(nic);

        if (existing == null)
        {
            return false;
        }

        var updateDefinition = Builders<Prosumer>.Update
            .Set(p => p.IsAvailable, false)
            .Set(p => p.AvailableEnergyKw, 0);

        var result = await _prosumers.UpdateOneAsync(
            p => p.Id == existing.Id,
            updateDefinition);

        return result.IsAcknowledged &&
               (result.ModifiedCount > 0 || result.MatchedCount > 0);
    }

    public async Task<bool> ReactivateAsync(string nic)
    {
        // Begin execution of ReactivateAsync method
        var existing = await GetByIdAsync(nic);

        if (existing == null)
        {
            return false;
        }

        var updateDefinition = Builders<Prosumer>.Update
            .Set(p => p.IsAvailable, true);

        var result = await _prosumers.UpdateOneAsync(
            p => p.Id == existing.Id,
            updateDefinition);

        return result.IsAcknowledged &&
               (result.ModifiedCount > 0 || result.MatchedCount > 0);
    }

    public async Task<bool> DeleteAsync(string nic)
    {
        // Begin execution of DeleteAsync method
        var existing = await GetByIdAsync(nic);

        if (existing == null)
        {
            return false;
        }

        var result = await _prosumers.DeleteOneAsync(
            p => p.Id == existing.Id);

        return result.IsAcknowledged && result.DeletedCount > 0;
    }
}
