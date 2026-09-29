// ============================================================================
// File: PermissionPolicyProvider.cs
// Project: SolarAPI - Smart Solar Microgrid Trading System
// Module: SE4040 - Enterprise Application Development
// Description: Dynamic policy provider mapping permission requirements to ASP.NET Core authorization policies.
// ============================================================================

using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace SolarAPI.Security;

public class PermissionPolicyProvider : DefaultAuthorizationPolicyProvider
{
    private readonly AuthorizationOptions _options;

    public PermissionPolicyProvider(IOptions<AuthorizationOptions> options) : base(options)
    {
        _options = options.Value;
    }

    public override async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        var existingPolicy = await base.GetPolicyAsync(policyName);
        if (existingPolicy != null)
        {
            return existingPolicy;
        }

        if (policyName.StartsWith(RequirePermissionAttribute.PolicyPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var permission = policyName.Substring(RequirePermissionAttribute.PolicyPrefix.Length);
            var policy = new AuthorizationPolicyBuilder();
            policy.RequireAuthenticatedUser();
            policy.AddRequirements(new PermissionRequirement(permission));
            return policy.Build();
        }

        return null;
    }
}
