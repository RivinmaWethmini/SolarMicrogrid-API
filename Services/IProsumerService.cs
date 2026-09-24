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
    Task<bool> ReactivateAsync(string nic);
    Task<bool> DeleteAsync(string id);
}
