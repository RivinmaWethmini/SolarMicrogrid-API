// Custom ASP.NET Core authorization handler evaluating fine-grained permission claims on authenticated identities.

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using SolarAPI.Models.Auth;

namespace SolarAPI.Security;

public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        // Begin execution of HandleRequirementAsync method
        if (context.User?.Identity?.IsAuthenticated != true)
        {
            return Task.CompletedTask;
        }

        // Admin role has superuser privilege across all permissions (supports ClaimTypes.Role, role, Role)
        var isAdminOrBackoffice = context.User.IsInRole(AuthRoles.Admin) || 
                                  context.User.IsInRole(AuthRoles.Backoffice) ||
                                  context.User.Claims.Any(c => 
                                      (c.Type == ClaimTypes.Role || c.Type == "role" || c.Type == "Role") &&
                                      (string.Equals(c.Value, AuthRoles.Admin, StringComparison.OrdinalIgnoreCase) ||
                                       string.Equals(c.Value, AuthRoles.Backoffice, StringComparison.OrdinalIgnoreCase)));

        if (isAdminOrBackoffice)
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        // Check for specific permission claim
        var hasPermission = context.User.Claims.Any(c =>
            (c.Type == "permission" || c.Type == "Permission") &&
            string.Equals(c.Value, requirement.Permission, StringComparison.OrdinalIgnoreCase));

        if (hasPermission)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
