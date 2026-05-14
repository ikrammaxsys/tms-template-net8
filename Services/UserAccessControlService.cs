using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Options;
using tms_template_net8.AccessValidation;
using tms_template_net8.Models;

namespace tms_template_net8.Services;

public sealed class UserAccessControlService : IUserAccessControlService
{
    public const string DefaultSessionKey = "UserAclData";

    private static readonly JsonSerializerOptions SerializeOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AuthOptions _auth;
    private readonly ILogger<UserAccessControlService> _logger;

    public UserAccessControlService(
        IHttpClientFactory httpClientFactory,
        IOptions<AuthOptions> authOptions,
        ILogger<UserAccessControlService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _auth = authOptions.Value;
        _logger = logger;
    }

    public async Task<UserAclData?> LoadAndStoreAsync(HttpContext context, string idAclUser, string? bearerToken, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (string.IsNullOrWhiteSpace(idAclUser))
            return null;

        try
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
            if (!string.IsNullOrWhiteSpace(bearerToken))
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken.Trim());

            var url = $"{_auth.BaseUrl}/api/users/{Uri.EscapeDataString(idAclUser)}/roles-and-access";
            using var response = await client.GetAsync(url, cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("ACL endpoint {Url} returned {Status}.", url, (int)response.StatusCode);
                return null;
            }

            var payload = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var data = JsonSerializer.Deserialize<AclApiResponse>(payload, SerializeOptions)?.Data;
            if (data == null)
                return null;

            await context.Session.LoadAsync(cancellationToken).ConfigureAwait(false);
            context.Session.SetString(DefaultSessionKey, JsonSerializer.Serialize(data));
            await context.Session.CommitAsync(cancellationToken).ConfigureAwait(false);

            return data;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load user roles and access for idAclUser={IdAclUser}.", idAclUser);
            return null;
        }
    }

    public UserAclData? GetCurrent(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var raw = context.Session.GetString(DefaultSessionKey);
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        try
        {
            return JsonSerializer.Deserialize<UserAclData>(raw, SerializeOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize cached UserAclData from session; treating as empty.");
            return null;
        }
    }

    public bool HasAccess(HttpContext context, string accessName, AccessRight right)
    {
        var snapshot = GetCurrent(context);
        return snapshot != null && snapshot.HasAccess(accessName, right);
    }

    public void Clear(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Session.Remove(DefaultSessionKey);
    }

    private sealed class AclApiResponse
    {
        public UserAclData? Data { get; set; }
    }
}
