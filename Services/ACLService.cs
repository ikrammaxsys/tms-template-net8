using System.Net;
using System.Net.Http.Headers;

namespace tms_template_net8.Services;

public sealed class ACLService : IACLService
{
    private const string VaspClientName = "Vasp";

    private readonly IHttpClientFactory _httpClientFactory;

    public ACLService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<string?> GetUserByIdAsync(string id, string? bearerToken = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        var client = _httpClientFactory.CreateClient(VaspClientName);
        var path = $"api/users/{Uri.EscapeDataString(id)}";

        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Accept.ParseAdd("application/json");
        if (!string.IsNullOrWhiteSpace(bearerToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken.Trim());

        using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
    }
}
