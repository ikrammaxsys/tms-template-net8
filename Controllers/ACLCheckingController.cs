using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using WebModels = tms_template_net8.Models;

namespace tms_template_net8.Controllers.Web;

[Route("[controller]")]
public class ACLCheckingController : Controller
{
    public const string AclSessionKey = "AclCheckPassed";
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    #region ACLCheckingController
    public ACLCheckingController(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }
    #endregion

    #region Index
    /// <summary>
    /// Entry URL from the external login app (canonical path), e.g.
    /// <c>https://host/ACLChecking/?ID_ACL_USER=7171&amp;auth-code=...</c>
    /// Requests to <c>/</c> with the same query are redirected here from <c>Program.cs</c>.
    /// <list type="number">
    /// <item><c>auth-code</c> is exchanged for JWT cookies, then stripped from the URL (other query params are kept).</item>
    /// <item>The view POSTs the access token to <see cref="Verify"/> for server-side validation and session.</item>
    /// </list>
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var tokenKey = _configuration["Auth:AccessTokenStorageKey"] ?? "authacl_access_token";
        var refreshKey = _configuration["Auth:RefreshTokenStorageKey"] ?? "authacl_refresh_token";
        var cookieOptions = CreateAuthCookieOptions(Request);

        var idAclUser = Request.Query["ID_ACL_USER"].ToString().Trim();
        if (!string.IsNullOrEmpty(idAclUser))
            HttpContext.Session.SetString("ID_ACL_USER", idAclUser);

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

            Response.Cookies.Delete(tokenKey, cookieOptions);
            Response.Cookies.Append(tokenKey, exchanged.AccessToken!, cookieOptions);
            Response.Cookies.Delete(refreshKey, cookieOptions);
            Response.Cookies.Append(refreshKey, exchanged.RefreshToken!, cookieOptions);

            var qb = new QueryBuilder();
            foreach (var kv in Request.Query)
            {
                if (string.Equals(kv.Key, "auth-code", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(kv.Key, "access_token", StringComparison.OrdinalIgnoreCase))
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
            Response.Cookies.Delete(tokenKey, cookieOptions);
            Response.Cookies.Append(tokenKey, token, cookieOptions);

            var qb = new QueryBuilder();
            foreach (var kv in Request.Query)
            {
                if (string.Equals(kv.Key, "access_token", StringComparison.OrdinalIgnoreCase))
                    continue;
                foreach (var v in kv.Value)
                    qb.Add(kv.Key, v ?? string.Empty);
            }

            return LocalRedirect(Request.PathBase + Request.Path + qb.ToQueryString());
        }

        ViewBag.IdAclUser = Request.Query["ID_ACL_USER"].ToString().Trim();
        return View();
    }
    #endregion

    #region Verify
    [HttpPost("verify")]
    public async Task<IActionResult> Verify([FromBody] WebModels.AclTokenRequest? body)
    {
        await HttpContext.Session.LoadAsync(HttpContext.RequestAborted);

        var tokenKey = _configuration["Auth:AccessTokenStorageKey"] ?? "authacl_access_token";
        var token = body?.Token?.Trim();
        if (string.IsNullOrWhiteSpace(token))
            token = Request.Cookies[tokenKey]?.Trim();
        if (string.IsNullOrWhiteSpace(token))
            return BadRequest(new { success = false, message = "Access token is missing.", redirectUrl = (string?)null });

        var authBaseUrl = _configuration["Auth:BaseUrl"]?.Trim();
        var verifyPath = _configuration["Auth:VerifyTokenUrl"]?.Trim();
        if (string.IsNullOrWhiteSpace(authBaseUrl) || string.IsNullOrWhiteSpace(verifyPath))
            return BadRequest(new { success = false, message = "Auth:VerifyTokenUrl is not configured", redirectUrl = (string?)null });
        
        var verifyUrl = authBaseUrl.TrimEnd('/') + "/" + verifyPath.TrimStart('/');

        var verifyResult = await VerifyTokenExternalAsync(token, verifyUrl);
        if (!verifyResult.Success)
        {
            // Always include redirectUrl, even if null
            return BadRequest(new
            {
                success = false,
                message = verifyResult.Message ?? "invalid_token",
                redirectUrl = SafeRedirectUrlOrNull(verifyResult.RedirectUrl)
            });
        }

        // Prefer explicit ID from POST (survives missing/stale session cookie), then session.
        var idAclFromBody = body?.IdAclUser?.Trim();
        if (!string.IsNullOrEmpty(idAclFromBody))
            HttpContext.Session.SetString("ID_ACL_USER", idAclFromBody);

        var sessionAclUser = HttpContext.Session.GetString("ID_ACL_USER")?.Trim();
        var sessionUserId = sessionAclUser ?? string.Empty;

        HttpContext.Session.SetString(AclSessionKey, "1");
        HttpContext.Session.SetString("gstrUserID", sessionUserId);
        if (!string.IsNullOrEmpty(sessionUserId))
            HttpContext.Session.SetString("gstrUserName", sessionUserId);

        var basePath = HttpContext.Request.PathBase.HasValue
            ? HttpContext.Request.PathBase.ToString().TrimEnd('/')
            : "";
        var homeUrl = string.IsNullOrEmpty(basePath) ? "/Home/Index" : basePath + "/Home/Index";

        return Ok(new
        {
            success = true,
            redirectUrl = homeUrl,
            userId = sessionUserId,
            empName = sessionUserId,
            userName = sessionUserId
        });
    }
    #endregion

    #region LogoutPage
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
    #endregion

    #region Logout
    /// <summary>
    /// Ends the app session, clears all cookies, and returns a redirect URL for the browser.
    /// </summary>
    [HttpPost("logout")]
    public IActionResult Logout()
    {
        HttpContext.Session.Clear();

        var tokenKey = _configuration["Auth:AccessTokenStorageKey"] ?? "authacl_access_token";
        var refreshKey = _configuration["Auth:RefreshTokenStorageKey"] ?? "authacl_refresh_token";
        var cookieOptions = CreateAuthCookieOptions(Request);
        Response.Cookies.Delete(tokenKey, cookieOptions);
        Response.Cookies.Delete(refreshKey, cookieOptions);
        Response.Cookies.Delete(".AspNetCore.Session");

        var redirectUrl = Url.Action("SessionExpired", "Home") ?? "/Home/SessionExpired";

        return Ok(new
        {
            success = true,
            redirectUrl
        });
    }
    #endregion

    #region ExchangeAuthCodeAsync
    private async Task<(bool Success, string? Message, string? AccessToken, string? RefreshToken)> ExchangeAuthCodeAsync(string authCode)
    {
        var baseUrl = _configuration["Auth:BaseUrl"]?.Trim();
        if (string.IsNullOrWhiteSpace(baseUrl))
            return (false, "Auth service URL is not configured.", null, null);

        var exchangePath = _configuration["Auth:ExchangeAuthCodeUrl"]?.Trim();
        if (string.IsNullOrWhiteSpace(exchangePath))
            return (false, "Auth code exchange endpoint is not configured.", null, null);

        var url = baseUrl.TrimEnd('/') + "/" + exchangePath.TrimStart('/');
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
                return (false, TryParseApiErrorMessage(payload) ?? "Auth code exchange failed.", null, null);
            }

            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;
            if (root.TryGetProperty("success", out var successNode) && successNode.ValueKind == JsonValueKind.False)
            {
                var msg = TryParseApiErrorMessage(payload);
                return (false, msg ?? "Auth code exchange was not successful.", null, null);
            }

            var access = root.GetProperty("data").GetProperty("accessToken").GetString();
            var refresh = root.GetProperty("data").TryGetProperty("refreshToken", out var rt) && rt.ValueKind == JsonValueKind.String
                ? rt.GetString()
                : null;

            if (string.IsNullOrWhiteSpace(access))
                return (false, "Exchange response did not contain an access token.", null, null);

            return (true, null, access.Trim(), string.IsNullOrWhiteSpace(refresh) ? null : refresh.Trim());
        }
        catch
        {
            return (false, "Auth code exchange error.", null, null);
        }
    }
    #endregion

    #region CreateAuthCookieOptions
    private static CookieOptions CreateAuthCookieOptions(HttpRequest request)
    {
        return new CookieOptions
        {
            HttpOnly = true,
            Secure = request.IsHttps,
            Path = "/",
            SameSite = SameSiteMode.Lax,
        };
    }
    #endregion

    #region SafeRedirectUrlOrNull
    private string? SafeRedirectUrlOrNull(string? redirectUrl)
    {
        if (string.IsNullOrWhiteSpace(redirectUrl))
            return null;

        if (Uri.TryCreate(redirectUrl, UriKind.Relative, out var relativeUri))
        {
            var value = relativeUri.ToString();
            if (value.StartsWith("/", StringComparison.Ordinal) && !value.StartsWith("//", StringComparison.Ordinal))
                return value;
        }

        if (!Uri.TryCreate(redirectUrl, UriKind.Absolute, out var target))
            return null;

        if (Uri.TryCreate($"{Request.Scheme}://{Request.Host}", UriKind.Absolute, out var current)
            && Uri.Compare(target, current, UriComponents.SchemeAndServer, UriFormat.SafeUnescaped, StringComparison.OrdinalIgnoreCase) == 0)
            return target.ToString();

        foreach (var configKey in new[] { "Vasp:BaseUrl", "Auth:BaseUrl" })
        {
            var configured = _configuration[configKey]?.Trim();
            if (Uri.TryCreate(configured, UriKind.Absolute, out var allowed)
                && Uri.Compare(target, allowed, UriComponents.SchemeAndServer, UriFormat.SafeUnescaped, StringComparison.OrdinalIgnoreCase) == 0)
                return target.ToString();
        }

        return null;
    }
    #endregion

    #region TryParseApiErrorMessage
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
    #endregion

    #region VerifyTokenExternalAsync
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

            return (false, message ?? $"Verify API failed with status {(int)response.StatusCode}", redirectUrl);
        }
        catch
        {
            return (false, "Verify API error.", null);
        }
    }
    #endregion

}
