using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using tms_template_net8.Models;
using tms_template_net8.Services;

namespace tms_template_net8.Controllers.Web;

[Route("[controller]")]
public class ACLCheckingController : Controller
{
    public const string AclSessionKey = "AclCheckPassed";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly IACLService _aclService;
    private readonly IUserAccessControlService _accessControlService;

    public ACLCheckingController(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        IACLService aclService,
        IUserAccessControlService accessControlService)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _aclService = aclService;
        _accessControlService = accessControlService;
    }

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
        var cookieOptions = new CookieOptions
        {
            HttpOnly = false,
            Secure = Request.IsHttps,
            Path = "/",
            SameSite = SameSiteMode.Lax,
        };

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
            Response.Cookies.Delete(tokenKey, cookieOptions);
            Response.Cookies.Append(tokenKey, token, cookieOptions);
        }

        ViewBag.Token = token;
        ViewBag.TokenKey = tokenKey;
        ViewBag.IdAclUser = Request.Query["ID_ACL_USER"].ToString().Trim();
        return View();
    }

    [HttpPost("verify")]
    public async Task<IActionResult> Verify([FromBody] AclTokenRequest? body)
    {
        await HttpContext.Session.LoadAsync(HttpContext.RequestAborted);

        var token = body?.Token?.Trim() ?? "";

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
                redirectUrl = verifyResult.RedirectUrl
            });
        }

        var idAclFromBody = body?.IdAclUser?.Trim();
        if (!string.IsNullOrEmpty(idAclFromBody))
            HttpContext.Session.SetString("ID_ACL_USER", idAclFromBody);

        var sessionAclUser = HttpContext.Session.GetString("ID_ACL_USER")?.Trim();

        UserAclData? aclSnapshot = null;
        if (!string.IsNullOrWhiteSpace(sessionAclUser))
        {
            aclSnapshot = await _accessControlService.LoadAndStoreAsync(HttpContext, sessionAclUser, token, HttpContext.RequestAborted);
        }

        var userInfo = await RetrieveUserDetail(sessionAclUser, token);
        var sessionUserId = aclSnapshot?.User?.UserId ?? userInfo.UserId ?? sessionAclUser ?? string.Empty;
        var sessionUserName = aclSnapshot?.User?.EmpName ?? userInfo.EmpName;

        HttpContext.Session.SetString(AclSessionKey, "1");
        HttpContext.Session.SetString("gstrUserID", sessionUserId);
        if (!string.IsNullOrEmpty(sessionUserName))
            HttpContext.Session.SetString("gstrUserName", sessionUserName);

        var basePath = HttpContext.Request.PathBase.HasValue
            ? HttpContext.Request.PathBase.ToString().TrimEnd('/')
            : "";
        var homeUrl = string.IsNullOrEmpty(basePath) ? "/Home/Index" : basePath + "/Home/Index";

        return Ok(new
        {
            success = true,
            redirectUrl = homeUrl,
            userId = sessionUserId,
            empName = sessionUserName,
            userName = sessionUserName,
            roles = aclSnapshot?.Roles,
            accessLoaded = aclSnapshot != null
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
    /// Ends the app session, clears all cookies, and returns a redirect URL for the browser.
    /// </summary>
    [HttpPost("logout")]
    public IActionResult Logout()
    {
        _accessControlService.Clear(HttpContext);
        HttpContext.Session.Clear();

        foreach (var cookieName in Request.Cookies.Keys)
            Response.Cookies.Delete(cookieName);

        var redirectUrl = Url.Action("SessionExpired", "Home") ?? "/Home/SessionExpired";

        return Ok(new
        {
            success = true,
            redirectUrl
        });
    }

    private async Task<(bool Success, string? Message, string? AccessToken, string? RefreshToken)> ExchangeAuthCodeAsync(string authCode)
    {
        var baseUrl = _configuration["Auth:BaseUrl"]?.Trim();
        if (string.IsNullOrWhiteSpace(baseUrl))
            return (false, "Auth service URL is not configured.", null, null);

        var url = baseUrl.TrimEnd('/') + "/api/auth/exchange-auth-code";
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
        catch (Exception ex)
        {
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
            if (root.TryGetProperty("error_description", out var desc) && desc.ValueKind == JsonValueKind.String)
                return desc.GetString();
            if (root.TryGetProperty("error", out var err) && err.ValueKind == JsonValueKind.String)
                return err.GetString();
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

            var message = TryParseApiErrorMessage(payload);
            string? redirectUrl = null;

            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;
            if (root.TryGetProperty("data", out var dataNode) && dataNode.ValueKind == JsonValueKind.Object)
            {
                if (dataNode.TryGetProperty("redirectUrl", out var redirectUrlNode))
                    redirectUrl = redirectUrlNode.GetString();
            }

            return (false, message ?? $"Verify API failed with status {(int)response.StatusCode}", redirectUrl);
        }
        catch (JsonException)
        {
            return (false, "Verify API returned an invalid JSON response.", null);
        }
        catch (Exception ex)
        {
            return (false, $"Verify API error: {ex.Message}", null);
        }
    }

    /// <summary>
    /// Loads display name (and id when present) from the Vasp ACL user API via <see cref="IACLService"/>.
    /// </summary>
    private async Task<UserDetail> RetrieveUserDetail(string? lookupUserId, string? bearerToken)
    {
        if (string.IsNullOrWhiteSpace(lookupUserId))
            return new UserDetail { UserId = lookupUserId, EmpName = lookupUserId };

        try
        {
            var payload = await _aclService.GetUserByIdAsync(lookupUserId, bearerToken, HttpContext.RequestAborted).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(payload))
                return new UserDetail { UserId = lookupUserId, EmpName = lookupUserId };

            using var doc = JsonDocument.Parse(payload);
            var userJson = doc.RootElement;
            if (userJson.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object)
                userJson = data;

            var user = JsonSerializer.Deserialize<UserDetail>(
                userJson.GetRawText(),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return new UserDetail
            {
                UserId = user?.UserId ?? lookupUserId,
                EmpName = user?.EmpName ?? lookupUserId
            };
        }
        catch
        {
            return new UserDetail { UserId = lookupUserId, EmpName = lookupUserId };
        }
    }
}
