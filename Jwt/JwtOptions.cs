namespace tms_template_net8.Jwt;

/// <summary>
/// Bound from the <c>Jwt</c> configuration section. Used by <see cref="RsaKeyLoader"/>
/// at startup and by <c>TokenService</c> at runtime.
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>Path (absolute or relative to ContentRoot) of the RS256 PEM file. Required.</summary>
    public string RsaKeyPath { get; set; } = string.Empty;

    public string Issuer { get; set; } = "authapi";
    public string Audience { get; set; } = "authapi-client";
}
