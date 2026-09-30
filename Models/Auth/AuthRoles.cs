namespace SolarAPI.Models.Auth;

public static class AuthRoles
{
    public const string Admin = "Admin";
    public const string Prosumer = "Prosumer";
    public const string Consumer = "Consumer";

    public static readonly IReadOnlyList<string> AllRoles = new[]
    {
        Admin,
        Prosumer,
        Consumer
    };

    public static bool IsValidRole(string role)
    {
        return AllRoles.Contains(role, StringComparer.OrdinalIgnoreCase);
    }
}
