// ============================================================================
// File: IProsumerService.cs
// Project: SolarAPI - Smart Solar Microgrid Trading System
// Module: SE4040 - Enterprise Application Development
// Description: Service contract defining business operations for prosumer lifecycle management and query retrieval.
// ============================================================================

using SolarAPI.Models;

namespace SolarAPI.Services;

public interface IProsumerService
{
    Task<IEnumerable<Prosumer>> GetAllAsync();
    Task<IEnumerable<Prosumer>> GetAvailableAsync();
    Task<Prosumer?> GetByIdAsync(string id);
    Task<Prosumer> CreateAsync(Prosumer prosumer);
    Task<bool> UpdateAsync(string id, Prosumer updatedProsumer);
    Task<bool> DeactivateAsync(string id);
    Task<bool> ReactivateAsync(string id);
    Task<bool> DeleteAsync(string id);
}
