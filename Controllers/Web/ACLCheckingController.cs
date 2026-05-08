using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using tms_template_net8.AccessValidation;
using tms_template_net8.Services;
using tms_template_net8.Tokens;
using WebModels = tms_template_net8.Models;

namespace tms_template_net8.Controllers.Web;

[Route("[controller]")]
public class ACLCheckingController : Controller
{
    public const string AclSessionKey = "AclCheckPassed";
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ITokenService _tokenService;
    private readonly IUserAccessControlService _userAccessControlService;
    private readonly AuthOptions _auth;

    public ACLCheckingController(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ITokenService tokenService,
        IUserAccessControlService userAccessControlService,
        IOptions<AuthOptions> authOptions)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _tokenService = tokenService;
        _userAccessControlService = userAccessControlService;
        _auth = authOptions.Value;
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
        var tokenKey = _auth.AccessTokenStorageKey;
        var refreshKey = _auth.RefreshTokenStorageKey;
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

            ReplaceCookie(tokenKey, exchanged.AccessToken!, cookieOptions);
            ReplaceCookie(refreshKey, exchanged.RefreshToken!, cookieOptions);
            return LocalRedirect(Request.PathBase + Request.Path + BuildRedirectQueryWithout("auth-code", "access_token"));
        }

        // One canonical cookie at Path=/ (replace). Avoids duplicate cookies when an older
        // cookie was issued with different options/path so ContainsKey missed it.
        var queryToken = Request.Query["access_token"].ToString();
        if (!string.IsNullOrWhiteSpace(queryToken))
        {
            ReplaceCookie(tokenKey, queryToken, cookieOptions);
            return LocalRedirect(Request.PathBase + Request.Path + BuildRedirectQueryWithout("access_token"));
        }

        ViewBag.IdAclUser = Request.Query["ID_ACL_USER"].ToString().Trim();
        return View();
    }

    [HttpPost("verify")]
    public async Task<IActionResult> Verify([FromBody] WebModels.AclTokenRequest? body)
    {
        await HttpContext.Session.LoadAsync(HttpContext.RequestAborted);

        var tokenKey = _auth.AccessTokenStorageKey;
        var token = body?.Token?.Trim();
        if (string.IsNullOrWhiteSpace(token))
            token = Request.Cookies[tokenKey]?.Trim();
        if (string.IsNullOrWhiteSpace(token))
            return BadRequest(new { success = false, message = "Access token is missing.", redirectUrl = (string?)null });

        var (_, tokenKind) = _tokenService.ValidateTokenWithKind(token);
        if (tokenKind != AuthTokenValidationKind.Valid)
            return BadRequest(new { success = false, message = tokenKind == AuthTokenValidationKind.Expired ? "token_expired" : "invalid_token", redirectUrl = (string?)null });

        // Prefer explicit ID from POST (survives missing/stale session cookie), then session.
        var idAclFromBody = body?.IdAclUser?.Trim();
        if (!string.IsNullOrEmpty(idAclFromBody))
            HttpContext.Session.SetString("ID_ACL_USER", idAclFromBody);

        var sessionAclUser = HttpContext.Session.GetString("ID_ACL_USER")?.Trim();
        var sessionUserId = sessionAclUser ?? string.Empty;

        // Load roles + access-control snapshot into session for [RequirePageAccess].
        // Failure is non-fatal: the user can still hit pages without the attribute.
        if (!string.IsNullOrEmpty(sessionUserId))
            await _userAccessControlService.LoadAndStoreAsync(HttpContext, sessionUserId, token, HttpContext.RequestAborted);

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

    /// <summary>
    /// Shows a full-page signing-out screen; the browser then POSTs to <see cref="Logout"/> to end the session.
    /// </summary>
    [HttpGet("logout")]
    public IActionResult LogoutPage()
    {
        var vaspBaseUrl = _configuration["Vasp:BaseUrl"]?.Trim();
        if (string.IsNullOrEmpty(vaspBaseUrl))
            vaspBaseUrl = "/";

        ViewBag.TokenKey = _auth.AccessTokenStorageKey;
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
        HttpContext.Session.Clear();

        var tokenKey = _auth.AccessTokenStorageKey;
        var refreshKey = _auth.RefreshTokenStorageKey;
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

    private async Task<(bool Success, string? Message, string? AccessToken, string? RefreshToken)> ExchangeAuthCodeAsync(string authCode)
    {
        var baseUrl = _auth.BaseUrl?.Trim();
        if (string.IsNullOrWhiteSpace(baseUrl))
            return (false, "Auth service URL is not configured.", null, null);

        var exchangePath = _auth.ExchangeAuthCodeUrl?.Trim();
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

    private void ReplaceCookie(string name, string value, CookieOptions options)
    {
        Response.Cookies.Delete(name, options);
        Response.Cookies.Append(name, value, options);
    }

    /// <summary>
    /// Rebuilds the current request's query string with the named keys removed (case-insensitive).
    /// Used to strip <c>auth-code</c> / <c>access_token</c> from the URL after they're consumed.
    /// </summary>
    private string BuildRedirectQueryWithout(params string[] excludedKeys)
    {
        var qb = new QueryBuilder();
        foreach (var kv in Request.Query)
        {
            if (excludedKeys.Any(k => string.Equals(kv.Key, k, StringComparison.OrdinalIgnoreCase)))
                continue;
            foreach (var v in kv.Value)
                qb.Add(kv.Key, v ?? string.Empty);
        }
        return qb.ToQueryString().ToString();
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
}
