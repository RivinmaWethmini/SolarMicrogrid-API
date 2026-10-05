// Custom authorize attribute allowing declarative permission enforcement on controllers and actions.

using Microsoft.AspNetCore.Authorization;

namespace SolarAPI.Security;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public class RequirePermissionAttribute : AuthorizeAttribute
{
    public const string PolicyPrefix = "PERMISSION_";

    public RequirePermissionAttribute(string permission)
    {
        Permission = permission ?? throw new ArgumentNullException(nameof(permission));
        Policy = $"{PolicyPrefix}{permission}";
    }

    public string Permission { get; }
}
