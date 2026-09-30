// ============================================================================
// File: AuthPermissions.cs
// Project: SolarAPI - Smart Solar Microgrid Trading System
// Module: SE4040 - Enterprise Application Development
// Description: Constants and definitions for fine-grained application authorization permissions.
// ============================================================================

namespace SolarAPI.Models.Auth;

public static class AuthPermissions
{
    // Microgrid Permissions
    public const string MicrogridRead = "microgrid:read";
    public const string MicrogridManage = "microgrid:manage";

    // Prosumer Permissions
    public const string ProsumerRead = "prosumer:read";
    public const string ProsumerManage = "prosumer:manage";

    // Reservation Permissions
    public const string ReservationRead = "reservation:read";
    public const string ReservationCreate = "reservation:create";
    public const string ReservationCancel = "reservation:cancel";
    public const string ReservationManage = "reservation:manage";

    // User & Session Management
    public const string UserManage = "user:manage";
    public const string AuditRead = "audit:read";
    public const string SessionRevoke = "session:revoke";

    public static List<string> GetDefaultPermissionsForRole(string role)
    {
        // Inline comment: Begin execution of GetDefaultPermissionsForRole method to map user role to authorization claims
        return role switch
        {
            AuthRoles.Admin or AuthRoles.Backoffice => new List<string>
            {
                MicrogridRead,
                MicrogridManage,
                ProsumerRead,
                ProsumerManage,
                ReservationRead,
                ReservationCreate,
                ReservationCancel,
                ReservationManage,
                UserManage,
                AuditRead,
                SessionRevoke
            },
            AuthRoles.GridOperator => new List<string>
            {
                MicrogridRead,
                MicrogridManage,
                ProsumerRead,
                ReservationRead,
                ReservationCreate,
                ReservationCancel,
                ReservationManage,
                SessionRevoke
            },
            AuthRoles.Prosumer => new List<string>
            {
                MicrogridRead,
                ProsumerRead,
                ProsumerManage,
                ReservationRead,
                ReservationCancel,
                SessionRevoke
            },
            AuthRoles.Consumer => new List<string>
            {
                MicrogridRead,
                ProsumerRead,
                ReservationRead,
                ReservationCreate,
                ReservationCancel,
                SessionRevoke
            },
            _ => new List<string>
            {
                MicrogridRead,
                ReservationRead
            }
        };
    }
}
