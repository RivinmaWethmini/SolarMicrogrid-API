// ============================================================================
// File: MongoDBSettings.cs
// Project: SolarAPI - Smart Solar Microgrid Trading System
// Module: SE4040 - Enterprise Application Development
// Description: Configuration settings model for MongoDB connection string and target database name.
// ============================================================================

namespace SolarAPI.Configurations;

public class MongoDBSettings
{
    public string ConnectionString { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = string.Empty;
}
