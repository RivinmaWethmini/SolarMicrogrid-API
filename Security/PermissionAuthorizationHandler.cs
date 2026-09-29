// ============================================================================
// File: PermissionAuthorizationHandler.cs
// Project: SolarAPI - Smart Solar Microgrid Trading System
// Module: SE4040 - Enterprise Application Development
// Description: Custom ASP.NET Core authorization handler evaluating fine-grained permission claims on authenticated identities.
// ============================================================================

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
        if (context.User?.Identity?.IsAuthenticated != true)
        {
            return Task.CompletedTask;
        }

        // Admin role has superuser privilege across all permissions
        if (context.User.IsInRole(AuthRoles.Admin))
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
