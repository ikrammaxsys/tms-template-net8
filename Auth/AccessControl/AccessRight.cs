namespace tms_template_net8.Models;

/// <summary>
/// Mirrors the right tokens returned in the ACL <c>accessControls</c> payload.
/// </summary>
public enum AccessRight
{
    View,
    Add,
    Edit,
    Delete
}

public static class AccessRightExtensions
{
    /// <summary>Lowercase wire token used in the ACL payload (<c>view</c>/<c>add</c>/<c>edit</c>/<c>delete</c>).</summary>
    public static string ToToken(this AccessRight right) => right switch
    {
        AccessRight.View => "view",
        AccessRight.Add => "add",
        AccessRight.Edit => "edit",
        AccessRight.Delete => "delete",
        _ => right.ToString().ToLowerInvariant()
    };
}
