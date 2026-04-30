namespace tms_template_net8.Tokens;

/// <summary>
/// Result of validating an access token beyond a simple valid/invalid split.
/// </summary>
public enum AuthTokenValidationKind
{
    /// <summary>Token is valid and within lifetime.</summary>
    Valid,

    /// <summary>Lifetime validation failed (typically expired).</summary>
    Expired,

    /// <summary>Malformed, bad signature, wrong issuer/audience, or other error.</summary>
    Invalid
}
