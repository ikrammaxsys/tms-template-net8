namespace tms_template_net8.Services;

public interface IACLService
{
    /// <param name="bearerToken">Optional; many user APIs require the same access token used for verify.</param>
    Task<string?> GetUserByIdAsync(string id, string? bearerToken = null, CancellationToken cancellationToken = default);
}
