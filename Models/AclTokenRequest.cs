namespace tms_template_net8.Models;

public class AclTokenRequest
{
    public string? Token { get; set; }

    /// <summary>Optional; same value as login redirect <c>ID_ACL_USER</c>. Sent from the gate page so verify does not rely only on the session cookie.</summary>
    public string? IdAclUser { get; set; }
}
