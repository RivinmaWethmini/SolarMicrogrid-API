// System role definitions (Admin, Prosumer, Consumer, GridOperator) for role-based access control.

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
        Consumer,
        "Operator"
    };

    public static bool IsValidRole(string role)
    {
        // Begin execution of IsValidRole method to validate role against allowed system roles
        return AllRoles.Contains(role, StringComparer.OrdinalIgnoreCase);
    }

    public static bool IsOperatorRole(string? role)
    {
        if (string.IsNullOrWhiteSpace(role)) return false;
        return string.Equals(role, GridOperator, StringComparison.OrdinalIgnoreCase)
            || string.Equals(role, "Operator", StringComparison.OrdinalIgnoreCase)
            || string.Equals(role, Admin, StringComparison.OrdinalIgnoreCase);
    }

    public static string NormalizeRole(string? role)
    {
        if (string.IsNullOrWhiteSpace(role)) return Consumer;
        if (string.Equals(role, "Operator", StringComparison.OrdinalIgnoreCase)) return GridOperator;
        if (string.Equals(role, GridOperator, StringComparison.OrdinalIgnoreCase)) return GridOperator;
        if (string.Equals(role, Prosumer, StringComparison.OrdinalIgnoreCase)) return Prosumer;
        if (string.Equals(role, Admin, StringComparison.OrdinalIgnoreCase)) return Admin;
        if (string.Equals(role, Backoffice, StringComparison.OrdinalIgnoreCase)) return Backoffice;
        if (string.Equals(role, Consumer, StringComparison.OrdinalIgnoreCase)) return Consumer;
        return role;
    }
}
