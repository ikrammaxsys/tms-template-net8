namespace tms_template_net8.AccessValidation;

public static class PublicPemSync
{
    public static async Task SyncAsync(IConfiguration configuration, IWebHostEnvironment environment)
    {
        var authBaseUrl = configuration["Auth:BaseUrl"]?.Trim();
        var pemPath = configuration["Jwt:RsaKeyPath"]?.Trim();
        var pemApiPath = "/api/index/public.pem";

        if (string.IsNullOrWhiteSpace(authBaseUrl) || string.IsNullOrWhiteSpace(pemPath))
            return;

        var fullPemPath = Path.IsPathRooted(pemPath)
            ? pemPath
            : Path.Combine(environment.ContentRootPath ?? ".", pemPath);

        var requestUrl = authBaseUrl.TrimEnd('/') + "/" + pemApiPath.TrimStart('/');

        try
        {
            using var client = new HttpClient();
            var pemContent = await client.GetStringAsync(requestUrl);
            if (string.IsNullOrWhiteSpace(pemContent))
                return;

            var directory = Path.GetDirectoryName(fullPemPath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            await File.WriteAllTextAsync(fullPemPath, pemContent);
        }
        catch
        {
            // Keep existing local PEM as fallback when remote fetch fails.
        }
    }
}
