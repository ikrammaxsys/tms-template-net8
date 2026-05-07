using Microsoft.AspNetCore.Http;
using tms_template_net8.Models;

namespace tms_template_net8.Services;

/// <summary>
/// Loads the user's roles + per-resource access controls from the auth API,
/// caches the snapshot in <c>HttpContext.Session</c>, and exposes a
/// <c>HasAccess(name, right)</c> check used by the page-access authorization filter.
/// </summary>
public interface IUserAccessControlService
{
    /// <summary>
    /// Calls the configured ACL endpoint, parses the standard
    /// <c>{ success, data: { user, roles, accessControls } }</c> envelope, and stores the result in session.
    /// Returns the loaded snapshot, or <c>null</c> if the call failed (the failure is non-fatal so
    /// the caller can still surface a friendly error).
    /// </summary>
    Task<UserAclData?> LoadAndStoreAsync(HttpContext context, string idAclUser, string? bearerToken, CancellationToken cancellationToken = default);

    /// <summary>Reads the cached snapshot from the current session, or <c>null</c> when not present.</summary>
    UserAclData? GetCurrent(HttpContext context);

    /// <summary>Convenience wrapper around <see cref="UserAclData.HasAccess"/> that returns <c>false</c> when no snapshot is cached.</summary>
    bool HasAccess(HttpContext context, string accessName, AccessRight right);

    /// <summary>Removes the cached snapshot from session (used on logout).</summary>
    void Clear(HttpContext context);
}
