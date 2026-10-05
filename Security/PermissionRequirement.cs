// ============================================================================
// File: PermissionRequirement.cs
// Project: SolarAPI - Smart Solar Microgrid Trading System
// Module: SE4040 - Enterprise Application Development
// Description: IAuthorizationRequirement implementation encapsulating required permission keys for route access.
// ============================================================================

using Microsoft.AspNetCore.Authorization;

namespace SolarAPI.Security;

public class PermissionRequirement : IAuthorizationRequirement
{
    public string Permission { get; }

    public PermissionRequirement(string permission)
    {
        Permission = permission ?? throw new ArgumentNullException(nameof(permission));
    }
}
