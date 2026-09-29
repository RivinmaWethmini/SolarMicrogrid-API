// ============================================================================
// File: ProsumerService.cs
// Project: SolarAPI - Smart Solar Microgrid Trading System
// Module: SE4040 - Enterprise Application Development
// Description: Implementation of prosumer business operations, MongoDB persistence, and availability filtering.
// ============================================================================

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
        // Inline comment: Begin execution of GetAllAsync method
        return await _prosumers.Find(_ => true).ToListAsync();
    }

    public async Task<IEnumerable<Prosumer>> GetAvailableAsync()
    {
        // Inline comment: Begin execution of GetAvailableAsync method
        return await _prosumers.Find(p => p.IsAvailable && p.AvailableEnergyKw > 0).ToListAsync();
    }

    public async Task<Prosumer?> GetByIdAsync(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier)) return null;
        var trimmed = identifier.Trim();
        return await _prosumers
            .Find(p => p.NIC == trimmed || p.Id == trimmed || p.UserId == trimmed)
            .FirstOrDefaultAsync();
    }

    public async Task<Prosumer> CreateAsync(Prosumer prosumer)
    {
        // Business logic: enforce clean creation state
        prosumer.Id = null; // Let MongoDB generate the ObjectId
        prosumer.CreatedAt = DateTime.UtcNow;
        prosumer.IsAvailable = true;

        if (string.IsNullOrWhiteSpace(prosumer.NIC) && !string.IsNullOrWhiteSpace(prosumer.UserId))
        {
            prosumer.NIC = prosumer.UserId;
        }

        await _prosumers.InsertOneAsync(prosumer);
        return prosumer;
    }

    public async Task<bool> UpdateAsync(string identifier, Prosumer updatedProsumer)
    {
        // Business logic: verify existence before update
        var existing = await GetByIdAsync(identifier);
        if (existing == null)
        {
            return false;
        }

        updatedProsumer.Id = existing.Id;
        if (string.IsNullOrWhiteSpace(updatedProsumer.NIC))
        {
            updatedProsumer.NIC = existing.NIC;
        }
        updatedProsumer.CreatedAt = existing.CreatedAt; // Preserve original creation timestamp

        var result = await _prosumers.ReplaceOneAsync(p => p.Id == existing.Id, updatedProsumer);
        return result.IsAcknowledged && (result.ModifiedCount > 0 || result.MatchedCount > 0);
    }

    public async Task<bool> DeactivateAsync(string identifier)
    {
        var existing = await GetByIdAsync(identifier);
        if (existing == null) return false;

        // Business logic: set availability to false and zero out active available energy offer
        var updateDefinition = Builders<Prosumer>.Update
            .Set(p => p.IsAvailable, false)
            .Set(p => p.AvailableEnergyKw, 0);

        var result = await _prosumers.UpdateOneAsync(p => p.Id == existing.Id, updateDefinition);
        return result.IsAcknowledged && (result.ModifiedCount > 0 || result.MatchedCount > 0);
    }

    public async Task<bool> ReactivateAsync(string identifier)
    {
        var existing = await GetByIdAsync(identifier);
        if (existing == null) return false;

        var updateDefinition = Builders<Prosumer>.Update
            .Set(p => p.IsAvailable, true);

        var result = await _prosumers.UpdateOneAsync(p => p.Id == existing.Id, updateDefinition);
        return result.IsAcknowledged && (result.ModifiedCount > 0 || result.MatchedCount > 0);
    }

    public async Task<bool> DeleteAsync(string identifier)
    {
        var existing = await GetByIdAsync(identifier);
        if (existing == null) return false;

        var result = await _prosumers.DeleteOneAsync(p => p.Id == existing.Id);
        return result.IsAcknowledged && result.DeletedCount > 0;
    }
}
