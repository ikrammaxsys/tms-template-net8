using System.Text.Json;

namespace tms_template_net8.Integrations;

public sealed class CoreAPIService : ICoreAPIService
{
    private const string JsonMediaType = "application/json";
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _coreApiBaseUrl;

    public CoreAPIService(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;

        var baseUrl = configuration["CoreApi:BaseUrl"];
        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new InvalidOperationException("Configuration 'CoreApi:BaseUrl' is required.");

        _coreApiBaseUrl = baseUrl.TrimEnd('/');
    }

    public async Task<string?> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var statusUri = new Uri($"{_coreApiBaseUrl}/api/v1/status");

        using var request = new HttpRequestMessage(HttpMethod.Get, statusUri);
        request.Headers.Accept.ParseAdd(JsonMediaType);

        using var client = _httpClientFactory.CreateClient();
        using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var responseText = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var payload = JsonSerializer.Deserialize<StringApiResponse>(responseText, JsonOptions);

        return payload?.Data;
    }

    private sealed class StringApiResponse
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public string? Data { get; set; }
        public string[]? Errors { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
