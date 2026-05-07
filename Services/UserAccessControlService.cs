using System.Net.Http.Headers;
using System.Text.Json;
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
    private readonly IConfiguration _configuration;
    private readonly ILogger<UserAccessControlService> _logger;

    public UserAccessControlService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<UserAccessControlService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public string SessionKey => DefaultSessionKey;

    public async Task<UserAclData?> LoadAndStoreAsync(HttpContext context, string idAclUser, string? bearerToken, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (string.IsNullOrWhiteSpace(idAclUser))
            return null;
        var url = $"{_configuration["Vasp:BaseUrl"]?.Trim()}/api/users/{idAclUser}/role-access";
        if (string.IsNullOrWhiteSpace(url))
            return null;

        try
        {
            var client = _httpClientFactory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Accept.ParseAdd("application/json");
            if (!string.IsNullOrWhiteSpace(bearerToken))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken.Trim());

            using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var payload = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
                return null;

            var data = ParsePayload(payload);
            if (data == null)
                return null;

            await context.Session.LoadAsync(cancellationToken).ConfigureAwait(false);
            var json = JsonSerializer.Serialize(data);
            context.Session.SetString(SessionKey, json);
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

        var raw = context.Session.GetString(SessionKey);
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
        context.Session.Remove(SessionKey);
    }

    private static UserAclData? ParsePayload(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
            return null;

        using var doc = JsonDocument.Parse(payload);
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
            return null;

        var dataNode = root;
        if (root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object)
            dataNode = data;
        else if (!root.TryGetProperty("accessControls", out _))
            return null;

        var raw = dataNode.GetRawText();
        var parsed = JsonSerializer.Deserialize<UserAclData>(raw, SerializeOptions);
        if (parsed == null)
            return null;

        // Force case-insensitive lookups regardless of how System.Text.Json built the dictionary.
        if (parsed.AccessControls.Comparer != StringComparer.OrdinalIgnoreCase)
        {
            parsed.AccessControls = new Dictionary<string, List<string>>(parsed.AccessControls, StringComparer.OrdinalIgnoreCase);
        }

        return parsed;
    }
}
