// Contract for database initialization and collection index configuration.

namespace SolarAPI.Services.Auth;

public interface IAuthDatabaseInitializer
{
    Task InitializeAsync();
}
