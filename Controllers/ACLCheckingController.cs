using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Extensions;
using AuthACL.CentralAuth.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace tms_template_net8.Controllers.Web;

[Route("[controller]")]
public class ACLCheckingController : Controller
{
    public const string AclSessionKey = "AclCheckPassed";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ACLCheckingController> _logger;

    public ACLCheckingController(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<ACLCheckingController> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var tokenKey = _configuration["Auth:AccessTokenStorageKey"] ?? "authacl_access_token";
        var refreshKey = _configuration["Auth:RefreshTokenStorageKey"] ?? "authacl_refresh_token";

        var authCode = Request.Query["auth-code"].ToString().Trim();
        if (!string.IsNullOrEmpty(authCode))
        {
            var exchanged = await ExchangeAuthCodeAsync(authCode);
            if (!exchanged.Success)
            {
                ViewBag.Error = exchanged.Message ?? "Auth code exchange failed.";
                ViewBag.TokenKey = tokenKey;
                return View();
            }

            var cookieOptions = new CookieOptions
            {
                HttpOnly = false,
                Secure = Request.IsHttps,
                Path = "/",
                SameSite = SameSiteMode.Lax,
            };
            Response.Cookies.Delete(tokenKey, cookieOptions);
            Response.Cookies.Append(tokenKey, exchanged.AccessToken!, cookieOptions);
            Response.Cookies.Delete(refreshKey, cookieOptions);
            if (!string.IsNullOrWhiteSpace(exchanged.RefreshToken))
                Response.Cookies.Append(refreshKey, exchanged.RefreshToken, cookieOptions);

            var qb = new QueryBuilder();
            foreach (var kv in Request.Query)
            {
                if (string.Equals(kv.Key, "auth-code", StringComparison.OrdinalIgnoreCase))
                    continue;
                foreach (var v in kv.Value)
                    qb.Add(kv.Key, v ?? string.Empty);
            }

            return LocalRedirect((Request.PathBase + Request.Path + qb.ToQueryString()));
        }

        var token = Request.Query["access_token"].ToString();

        // One canonical cookie at Path=/ (replace). Avoids duplicate cookies when an older
        // cookie was issued with different options/path so ContainsKey missed it.
        if (!string.IsNullOrWhiteSpace(token))
        {
            var cookieOptions = new CookieOptions
            {
                HttpOnly = false,
                Secure = Request.IsHttps,
                Path = "/",
                SameSite = SameSiteMode.Lax,
            };
            Response.Cookies.Delete(tokenKey, cookieOptions);
            Response.Cookies.Append(tokenKey, token, cookieOptions);
        }

        ViewBag.Token = token;
        ViewBag.TokenKey = tokenKey;
        return View();
    }

    [HttpPost("verify")]
    public async Task<IActionResult> Verify([FromBody] AclTokenRequest? body)
    {
        var token = body?.Token?.Trim() ?? "";

        var verifyUrl = _configuration["Auth:BaseUrl"]?.Trim() + _configuration["Auth:VerifyTokenUrl"]?.Trim();
        if (string.IsNullOrWhiteSpace(verifyUrl))
        {
            _logger.LogWarning("Verify rejected: Auth verify URL is not configured.");
            return BadRequest(new { success = false, message = "Auth:VerifyTokenUrl is not configured", redirectUrl = (string?)null });
        }

        var verifyResult = await VerifyTokenExternalAsync(token, verifyUrl);
        if (!verifyResult.Success)
        {
            // Always include redirectUrl, even if null
            return BadRequest(new
            {
                success = false,
                message = verifyResult.Message ?? "invalid_token",
                redirectUrl = verifyResult.RedirectUrl
            });
        }

        string? userIdFromToken = null;
        DateTime? expiresAtUtc = null;
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var jwt = handler.ReadJwtToken(token);
            userIdFromToken = jwt.Claims.FirstOrDefault(x => x.Type == JwtRegisteredClaimNames.Sub)?.Value
                ?? jwt.Claims.FirstOrDefault(x => x.Type == "nameid")?.Value
                ?? jwt.Claims.FirstOrDefault(x => x.Type == "unique_name")?.Value;
            expiresAtUtc = jwt.ValidTo;
        }
        catch
        {
            // Token has already been verified externally; continue with minimal fallback values.
        }

        var userInfo = await RetrieveUserDetail(token, userIdFromToken);
        var sessionUserId = userInfo.UserId ?? userIdFromToken ?? string.Empty;

        HttpContext.Session.SetString(AclSessionKey, "1");
        HttpContext.Session.SetString("gstrUserID", sessionUserId);

        _logger.LogInformation("ACL verify succeeded; session established for user id length {UserIdLength}.", sessionUserId.Length);

        return Ok(new
        {
            success = true,
            redirectUrl = Url.Action("Index", "Home"),
            userId = sessionUserId,
            expiresAtUtc = expiresAtUtc?.ToUniversalTime().ToString("o")
        });
    }

    /// <summary>
    /// Shows a full-page signing-out screen; the browser then POSTs to <see cref="Logout"/> to end the session.
    /// </summary>
    [HttpGet("logout")]
    public IActionResult LogoutPage()
    {
        var vaspBaseUrl = _configuration["Vasp:BaseUrl"]?.Trim();
        if (string.IsNullOrEmpty(vaspBaseUrl))
            vaspBaseUrl = "/";

        ViewBag.TokenKey = _configuration["Auth:AccessTokenStorageKey"] ?? "authacl_access_token";
        ViewBag.VaspBaseUrl = vaspBaseUrl;
        ViewBag.LogoutPostUrl = Url.Action(nameof(Logout), "ACLChecking");
        return View();
    }

    /// <summary>
    /// Ends the app session, calls the external auth logout API with the current bearer token, clears cookies, and returns a redirect URL for the browser.
    /// </summary>
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        _logger.LogInformation("Logout requested.");

        var tokenKey = _configuration["Auth:AccessTokenStorageKey"] ?? "authacl_access_token";
        var token = Request.Cookies[tokenKey]?.Trim() ?? "";

        if (string.IsNullOrEmpty(token))
        {
            var auth = Request.Headers.Authorization.ToString();
            const string bearer = "Bearer ";
            if (auth.StartsWith(bearer, StringComparison.OrdinalIgnoreCase))
                token = auth.Substring(bearer.Length).Trim();
        }

        var authLogoutOk = await LogoutAsync(token);
        if (!authLogoutOk)
            _logger.LogWarning("External auth logout did not report success; local session will still be cleared.");

        HttpContext.Session.Clear();

        var cookieOptions = new CookieOptions
        {
            HttpOnly = false,
            Secure = Request.IsHttps,
            Path = "/",
            SameSite = SameSiteMode.Lax,
        };
        Response.Cookies.Delete(tokenKey, cookieOptions);

        var refreshKey = _configuration["Auth:RefreshTokenStorageKey"] ?? "authacl_refresh_token";
        Response.Cookies.Delete(refreshKey, cookieOptions);

        _logger.LogInformation("Logout completed; session and access token cookie cleared.");

        return Ok(new
        {
            success = true,
            redirectUrl = Url.Action("SessionExpired", "Home")
        });
    }

    private async Task<(bool Success, string? Message, string? AccessToken, string? RefreshToken)> ExchangeAuthCodeAsync(string authCode)
    {
        var baseUrl = _configuration["Auth:BaseUrl"]?.Trim();
        var path = _configuration["Auth:ExchangeAuthCodeUrl"]?.Trim() ?? "/api/auth/exchange-auth-code";
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            _logger.LogWarning("Exchange auth code skipped: Auth:BaseUrl is not configured.");
            return (false, "Auth service URL is not configured.", null, null);
        }

        var url = baseUrl.TrimEnd('/') + "/" + path.TrimStart('/');
        try
        {
            var client = _httpClientFactory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Accept.ParseAdd("application/json");
            var bodyJson = JsonSerializer.Serialize(new { authCode });
            request.Content = new StringContent(bodyJson, Encoding.UTF8, "application/json");

            using var response = await client.SendAsync(request);
            var payload = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Exchange auth code returned {Status}: {Body}", (int)response.StatusCode, payload);
                return (false, TryParseApiErrorMessage(payload) ?? "Auth code exchange failed.", null, null);
            }

            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;
            if (root.TryGetProperty("success", out var successNode) && successNode.ValueKind == JsonValueKind.False)
            {
                var msg = TryParseApiErrorMessage(payload);
                return (false, msg ?? "Auth code exchange was not successful.", null, null);
            }

            string? access = null;
            string? refresh = null;
            if (root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object)
            {
                if (data.TryGetProperty("accessToken", out var at) && at.ValueKind == JsonValueKind.String)
                    access = at.GetString();
                if (data.TryGetProperty("refreshToken", out var rt) && rt.ValueKind == JsonValueKind.String)
                    refresh = rt.GetString();
            }

            if (string.IsNullOrWhiteSpace(access))
                return (false, "Exchange response did not contain an access token.", null, null);

            return (true, null, access.Trim(), string.IsNullOrWhiteSpace(refresh) ? null : refresh.Trim());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exchange auth code request failed.");
            return (false, $"Auth code exchange error: {ex.Message}", null, null);
        }
    }

    private static string? TryParseApiErrorMessage(string payload)
    {
        try
        {
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return null;
            if (root.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String)
                return m.GetString();
            if (root.TryGetProperty("errors", out var errs) && errs.ValueKind == JsonValueKind.Array && errs.GetArrayLength() > 0)
            {
                var first = errs[0];
                if (first.ValueKind == JsonValueKind.String)
                    return first.GetString();
            }
        }
        catch
        {
            // ignore
        }

        return null;
    }

    private async Task<(bool Success, string? Message, string? RedirectUrl)> VerifyTokenExternalAsync(string token, string verifyUrl)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Get, verifyUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.Accept.ParseAdd("application/json");

            using var response = await client.SendAsync(request);
            var payload = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
                return (true, null, null);

            string? message = null;
            string? redirectUrl = null;
            try
            {
                using var doc = JsonDocument.Parse(payload);
                var root = doc.RootElement;
                if (root.TryGetProperty("message", out var msg))
                    message = msg.GetString();
                else if (root.TryGetProperty("error_description", out var desc))
                    message = desc.GetString();
                else if (root.TryGetProperty("error", out var err))
                    message = err.GetString();

                if (root.TryGetProperty("data", out var dataNode) && dataNode.ValueKind == JsonValueKind.Object)
                {
                    if (dataNode.TryGetProperty("redirectUrl", out var ru))
                        redirectUrl = ru.GetString();
                    else if (dataNode.TryGetProperty("RedirectUrl", out var ru2))
                        redirectUrl = ru2.GetString();
                }
            }
            catch
            {
                // keep fallback message
            }

            _logger.LogWarning("Verify API returned {StatusCode}: {Message}", (int)response.StatusCode, message);
            return (false, message ?? $"Verify API failed with status {(int)response.StatusCode}", redirectUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Verify API request failed.");
            return (false, $"Verify API error: {ex.Message}", null);
        }
    }

    /// <summary>
    /// TODO: Replace with subsystem-specific user lookup logic.
    /// This method should query your local database, call your user service API, or use any other
    /// subsystem-specific mechanism to retrieve user details based on the userId from the JWT token.
    /// 
    /// Example implementations:
    /// - Query local database: var user = await _userRepository.GetByIdAsync(userId);
    /// - Call user service API: var user = await _userService.GetUserByIdAsync(userId);
    /// - Use Auth:UserInfoUrl endpoint (see commented code below)
    /// </summary>
    private async Task<UserDetail> RetrieveUserDetail(string token, string? userId)
    {
        // Default implementation - returns minimal user detail
        // Replace this with your subsystem-specific user lookup logic
        return new UserDetail 
        { 
            UserId = userId, 
            Username = userId 
        };

        /* Example: Using Auth:UserInfoUrl endpoint
        var userInfoUrlTemplate = _configuration["Auth:UserInfoUrl"]?.Trim();
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(userInfoUrlTemplate))
            return new UserDetail { UserId = userId, Username = userId };

        var url = userInfoUrlTemplate.Replace("{userId}", Uri.EscapeDataString(userId), StringComparison.OrdinalIgnoreCase);
        try
        {
            var client = _httpClientFactory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.Accept.ParseAdd("application/json");

            using var response = await client.SendAsync(request);
            if (!response.IsSuccessStatusCode)
                return new UserDetail { UserId = userId, Username = userId };

            var payload = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;
            
            var target = root;
            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("data", out var dataNode))
                target = dataNode;

            var userIdValue = JsonStringOrNumber(target, "userId", "UserId");
            var usernameValue = JsonStringOrNumber(target, "username", "Username");

            return new UserDetail
            {
                UserId = userIdValue ?? userId,
                Username = usernameValue ?? userId
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "User info request failed; using fallback.");
            return new UserDetail { UserId = userId, Username = userId };
        }
        */
    }

    private async Task<bool> LogoutAsync(string token)
    {
        var logoutUrl = _configuration["Auth:BaseUrl"]?.Trim() + _configuration["Auth:LogoutApiUrl"]?.Trim();
        if (string.IsNullOrWhiteSpace(logoutUrl))
        {
            _logger.LogWarning("LogoutApiUrl not configured or empty.");
            return false;
        }

        var client = _httpClientFactory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, logoutUrl);
        request.Headers.Accept.ParseAdd("application/json");
        if (!string.IsNullOrWhiteSpace(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        try
        {
            _logger.LogInformation("Sending logout request to {LogoutUrl}", logoutUrl);
            using var response = await client.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Logout successful (HTTP {StatusCode})", response.StatusCode);
                return true;
            }
            else
            {
                _logger.LogWarning("Logout failed with status code: {StatusCode}", response.StatusCode);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred during logout request.");
            return false;
        }
    }

    private static string? JsonStringOrNumber(JsonElement target, params string[] propertyNames)
    {
        if (target.ValueKind != JsonValueKind.Object)
            return null;
        foreach (var name in propertyNames)
        {
            if (!target.TryGetProperty(name, out var prop))
                continue;
            return prop.ValueKind switch
            {
                JsonValueKind.String => prop.GetString(),
                JsonValueKind.Number => prop.ToString(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => null
            };
        }
        return null;
    }
}
