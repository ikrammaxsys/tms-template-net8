using System.Text.Json.Serialization;

namespace tms_template_net8.Models;

/// <summary>
/// Snapshot of the current user's roles and per-resource access controls,
/// stored server-side in <c>HttpContext.Session</c> after the ACL verify step.
/// </summary>
public sealed class UserAclData
{
    [JsonPropertyName("user")]
    public UserAclInfo? User { get; set; }

    [JsonPropertyName("roles")]
    public List<string> Roles { get; set; } = new();

    /// <summary>
    /// Map of access-control name (e.g. "PAB Sites") to the set of granted rights
    /// (any of <c>view</c>, <c>add</c>, <c>edit</c>, <c>delete</c>). Keys are stored case-insensitively
    /// when looked up via <see cref="HasAccess"/>.
    /// </summary>
    [JsonPropertyName("accessControls")]
    public Dictionary<string, List<string>> AccessControls { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Returns true when the current user holds <paramref name="right"/> on
    /// <paramref name="accessName"/>. Both name and right matching are case-insensitive.
    /// </summary>
    public bool HasAccess(string accessName, AccessRight right)
    {
        if (string.IsNullOrWhiteSpace(accessName))
            return false;

        if (!AccessControls.TryGetValue(accessName, out var rights) || rights == null)
            return false;

        var token = right.ToToken();
        foreach (var r in rights)
        {
            if (string.Equals(r?.Trim(), token, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}

public sealed class UserAclInfo
{
    [JsonPropertyName("idAclUser")]
    public long? IdAclUser { get; set; }

    [JsonPropertyName("idAclCompany")]
    public long? IdAclCompany { get; set; }

    [JsonPropertyName("userId")]
    public string? UserId { get; set; }

    [JsonPropertyName("empName")]
    public string? EmpName { get; set; }

    [JsonPropertyName("empNo")]
    public string? EmpNo { get; set; }

    [JsonPropertyName("usrEmail")]
    public string? UsrEmail { get; set; }

    [JsonPropertyName("statusInd")]
    public string? StatusInd { get; set; }

    [JsonPropertyName("idAclRole")]
    public long? IdAclRole { get; set; }

    [JsonPropertyName("idAclResource")]
    public long? IdAclResource { get; set; }

    [JsonPropertyName("company")]
    public string? Company { get; set; }

    [JsonPropertyName("companyDesc")]
    public string? CompanyDesc { get; set; }

    [JsonPropertyName("companyType")]
    public string? CompanyType { get; set; }
}
