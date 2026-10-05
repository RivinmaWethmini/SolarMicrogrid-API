// ============================================================================
// File: IAuthDatabaseInitializer.cs
// Project: SolarAPI - Smart Solar Microgrid Trading System
// Module: SE4040 - Enterprise Application Development
// Description: Contract for database initialization and collection index configuration.
// ============================================================================

namespace SolarAPI.Services.Auth;

public interface IAuthDatabaseInitializer
{
    Task InitializeAsync();
}
