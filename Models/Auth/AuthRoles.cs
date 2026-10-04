// ============================================================================
// File: AuthRoles.cs
// Project: SolarAPI - Smart Solar Microgrid Trading System
// Module: SE4040 - Enterprise Application Development
// Description: System role definitions (Admin, Prosumer, Consumer, GridOperator) for role-based access control.
// ============================================================================

namespace SolarAPI.Models.Auth;

public static class AuthRoles
{
    public const string Admin = "Admin";
    public const string Backoffice = "Backoffice";
    public const string GridOperator = "GridOperator";
    public const string Prosumer = "Prosumer";
    public const string Consumer = "Consumer";

    public static readonly IReadOnlyList<string> AllRoles = new[]
    {
        Admin,
        Backoffice,
        GridOperator,
        Prosumer,
        Consumer
    };

    public static bool IsValidRole(string role)
    {
        // Inline comment: Begin execution of IsValidRole method to validate role against allowed system roles
        return AllRoles.Contains(role, StringComparer.OrdinalIgnoreCase);
    }
}
