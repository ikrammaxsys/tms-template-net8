# TMS Template (.NET 8)

ASP.NET Core MVC template for TMS subsystem projects with:

- TMS UI Core components for layout and page widgets
- TMS Core SDK/Common module integration points
- TMS Core API connectivity scaffold
- ACL V2 flow for authentication and authorization checks

This template gives a ready-to-use shell (sidebar, topbar, session handling, ACL entry flow) plus an example `ProductManagement` module that demonstrates list/create/detail/edit patterns.

## Tech Stack

- .NET 8 (`Microsoft.NET.Sdk.Web`)
- ASP.NET Core MVC + Web API
- Session + JWT bearer authentication
- TMS UI Core Web Components (loaded from UI Foundation loader)
- Package reference: `AuthACL.CentralAuth`

## What Is Inside

- `Program.cs`
  - Thin bootstrap. All DI is delegated to two extension methods:
    `services.AddAuthAndAcl(...)` (auth + ACL gate + Vasp HttpClient)
    and `services.AddAppServices()` (template/business services).
  - Registers TMS Core SDK via `services.AddTmsWebAppSdk(...)` and enables:
    - default SQL connection-name fallback to `ConnectionStrings:Default`
    - optional remote connection-string resolution (`UseRemoteAclConnectionProvider()`)
    - optional SQL stored-procedure error logging (`UseSqlErrorLogger()`)
  - Pipeline composes the ACL bootstrap redirect (`/` → `/ACLChecking`)
    and the access-token validation middleware.
- `Jwt/`, `Tokens/`, `AccessControl/`, `AccessValidation/`
  - JWT key loading (`RsaKeyLoader`) and validation (`TokenService`)
  - Access-token validation middleware (`AccessTokenValidationMiddleware`)
  - Token refresh service (`AuthTokenRefreshService`)
  - Page-level access control (`RequirePageAccessAttribute` + `PageAccessAuthorizationFilter`)
  - Strongly-typed config: `JwtOptions` and `AuthOptions` bound from `appsettings.json`.
  - All registered in one place via `services.AddAuthAndAcl(...)`.
- `Controllers/Web/ACLCheckingController.cs`
  - Entry point for ACL callback/query parameters
  - Auth code exchange flow
  - Token verification flow (loads roles + access controls into session)
  - Session population (`gstrUserID`, `gstrUserName`, `UserAclData`)
  - Logout and session cleanup flow
- `Services/`
  - `ServiceCollectionExtensions.AddAppServices()`: registers the services below
  - `ACLService`: VASP/ACL user lookup (template scaffolding example)
  - `CoreAPIService`: sample call to TMS Core API (`/api/v1/status`)
  - `ReportService`: sample SQL access via SDK `ISqlExecutor`
  - `ProductService`: in-memory sample CRUD service
- `Controllers/Web` and `Controllers/Api`
  - MVC pages + API endpoints
  - Example product CRUD API in `api/products`
- `Views/Shared`
  - Main layout with `<ui-sidebar>` and `<ui-topbar>`
  - Reusable confirmation modal partial
- `wwwroot/`
  - Shared JS (`apiClient`, route mapping, nav helper, notifications)
  - Module JS under `wwwroot/ProductManagement/js`
  - Sidebar data and static dropdown options in `wwwroot/data`

## Architecture Overview

1. External app redirects to `ACLChecking` with query params (for example `ID_ACL_USER`, `auth-code`).
2. `ACLCheckingController` exchanges auth code (when present), sets token cookies, and verifies token with auth API.
3. On success, user session values are set and user is redirected to `Home/Index`.
4. `AccessTokenValidationMiddleware` protects non-API pages:
   - Reads access token from cookie/query
   - Validates JWT
   - Attempts refresh if token expired and refresh token exists
   - Redirects to `Home/SessionExpired` if invalid
5. `SessionExpired` view redirects back to configured VASP base URL.

## Configuration

Primary runtime settings are in `appsettings.json`. The `Jwt` and `Auth` sections
are bound to strongly-typed `JwtOptions` and `AuthOptions` (defaults shown in
parentheses; defaults are baked into the option classes so missing keys are fine):

- `Jwt` → `JwtOptions`
  - `RsaKeyPath` — required
  - `Issuer` (`"authapi"`)
  - `Audience` (`"authapi-client"`)
- `Auth` → `AuthOptions`
  - `BaseUrl` — base URL of the auth API
  - `ExchangeAuthCodeUrl` — path or absolute URL for the auth-code exchange
  - `RefreshTokenApiUrl` (`"/api/auth/refresh-token"`)
  - `LogoutApiUrl` — informational
  - `UserRolesAndAccessUrl` — endpoint that returns `{ data: { user, roles, accessControls } }` for the page-access filter. Supports `{idAclUser}` placeholder, e.g. `/api/users/{idAclUser}/roles-and-access`. May be a path (combined with `BaseUrl`) or an absolute URL.
  - `AccessTokenStorageKey` (`"authacl_access_token"`)
  - `RefreshTokenStorageKey` (`"authacl_refresh_token"`)
  - `RefreshTokenRequestUsesGrantType` (`false`) — when true, refresh body is `{ grantType, refreshToken }`; otherwise `{ refreshToken }`
- `Vasp`
  - `BaseUrl` (ACL/VASP UI base URL — used by the named `Vasp` HttpClient)
- `UiFoundation`
  - `BaseUrl` (TMS UI web component loader base URL)
- `CoreApi`
  - `BaseUrl` (TMS Core API base URL)
- `ConnectionStrings`
  - `Default` (fallback SQL Server connection string used when no logical connection name is provided)
- `TmsSdk`
  - `ConnectionString:DefaultName` (logical DB connection name for `ISqlExecutor`)
  - `ConnectionString:RemoteResolverUrl` (optional; enables remote connection-string provider)
  - `ErrorLog:StoredProcedureName` (optional; enables SQL stored-procedure error logger)
  - `ErrorLog:ConnectionName` (optional; connection-name override for error logging)

Optional/used by views and middleware:

- `App:SystemName` for title/footer branding
- `App:BaseUrl` for base-path aware redirects in middleware (if app is mounted under sub-path)

> `appsettings.Development.json` currently keeps these sections commented. Add/override values there for local development.

## How To Run

1. Install .NET 8 SDK.
2. Configure `appsettings.json` (or `appsettings.Development.json`) for your environment:
   - Auth API URL
   - VASP/ACL base URL
   - Core API base URL
   - JWT public key path and issuer/audience
3. Restore and run:

```bash
dotnet restore
dotnet run
```

Default local URL is shown in console output.

## How To Use This Template For A New Module

Use `ProductManagement` as the reference implementation.

### 1) Add model + service

- Create model(s) in `Models/<Module>/`
- Add service interface in `Services/Interfaces/`
- Implement service in `Services/`
- Register service in `Services/ServiceCollectionExtensions.cs` inside `AddAppServices(...)` (avoid touching `Program.cs` for normal modules)

### 2) Add API endpoints

- Create `Controllers/Api/<Module>Controller.cs`
- Follow consistent response shape:
  - `success`
  - `message`
  - `data`

### 3) Add MVC pages

- Create `Controllers/Web/<Module>Controller.cs`
- Add views:
  - `Views/<Module>/Index.cshtml`
  - `Create.cshtml`
  - `Detail.cshtml`
  - `Edit.cshtml`

### 4) Add frontend script

- Create module JS in `wwwroot/<Module>/js/`
- Use shared utilities:
  - `wwwroot/js/apiClient.js`
  - `wwwroot/js/apiRoutes.js`
  - `wwwroot/js/notifications.js`

### 5) Add navigation

- Update `wwwroot/data/sidebar-items.json` with ACL-style menu entry.

## TMS Components Integration Notes

### Documentation Links

- TMS Core SDK Docs: [http://10.230.8.170/core-sdk-docs/](http://10.230.8.170/core-sdk-docs/)
- TMS UI Core Docs: [http://10.230.8.170/UiFoundationdocs/](http://10.230.8.170/UiFoundationdocs/)

### TMS UI Core

The layout uses TMS UI web components:

- `<ui-sidebar>` in `_Sidebar.cshtml`
- `<ui-topbar>` in `_Topbar.cshtml`
- `<ui-page-section>`, `<ui-form-card>`, `<ui-form-content>`, `<ui-text-input>`, `<ui-dropdown>`, `<ui-button>`, `<ui-datatable>` in module pages

These are loaded via the UI Foundation loader script in `Views/Shared/_Layout.cshtml`. Configure `UiFoundation:BaseUrl` for your UI Core environment.

### TMS Core SDK / Common Module

`AuthACL.CentralAuth` package is installed and used in ACL/auth flows.  
For additional TMS common modules, follow the same pattern:

1. Add package reference in `.csproj`
2. Register required services in `Services/ServiceCollectionExtensions.cs` (`AddAppServices`)
3. Wrap usage behind project-local service interfaces in `Services/Interfaces`

#### SQL data access with `ISqlExecutor`

`ISqlExecutor`, `ISettingService`, and `IDropdownService` are registered by `AddTmsWebAppSdk(...)`.

- For stored procedures, pass `IEnumerable<SqlParameter>` and `CommandType.StoredProcedure`
- For Dapper-style queries, use `QueryAsync<T>` / `QuerySingleAsync<T>` with an anonymous object or POCO parameter object
- `connectionName` is optional:
  - when omitted, SDK uses `TmsSdk:ConnectionString:DefaultName`
  - when remote resolver is enabled, the name is resolved via `TmsSdk:ConnectionString:RemoteResolverUrl`
  - otherwise it resolves from `ConnectionStrings:<name>`

See `Services/ReportService.cs` and `Services/Interfaces/IReportService.cs` for concrete examples.

### TMS Core API

`CoreAPIService` is the template entry point for Core API integration.  
Configure `CoreApi:BaseUrl` and add new methods following `GetStatusAsync`.

### ACL V2 (Authorization)

ACL V2 behavior is implemented by:

- `ACLCheckingController` — gate page, auth-code exchange, verify, logout
- `AccessTokenValidationMiddleware` — per-request token check + refresh
- `AuthTokenRefreshService` — calls the refresh endpoint
- `UserAccessControlService` — loads roles + access controls into session
- All wired together in one place: `services.AddAuthAndAcl(...)` (see `Jwt/AuthServiceExtensions.cs`)

If your ACL endpoints or payload contracts differ, update:

- `Auth` section values in `appsettings.json` (bound to `AuthOptions`)
- `ACLCheckingController.ExchangeAuthCodeAsync` parsing logic
- `UserAccessControlService.ParsePayload` if the roles/access envelope differs

### Page-Level Access Control

Per-route policy-style authorization based on the user's `accessControls` map.

How it flows:

1. After token verify, `ACLCheckingController.Verify` calls `Auth:UserRolesAndAccessUrl` (with bearer token) and stores the parsed `UserAclData` (user + roles + per-resource rights) **server-side** in `HttpContext.Session` under key `UserAclData`.
2. Controllers/actions are decorated with `[RequirePageAccess("<access name>", AccessRight.View|Add|Edit|Delete)]`. The access name must match a key in the `accessControls` dictionary.
3. `PageAccessAuthorizationFilter` runs before the action, reads the snapshot from session via `IUserAccessControlService`, and either lets the request through or short-circuits to `/Home/AccessDenied` (HTTP 403). AJAX/`/api/*` requests get a JSON 403 instead of a redirect.

Example (see `Controllers/Web/ProductManagementController.cs`):

```csharp
[Route("[controller]")]
[RequirePageAccess("PAB Sites", AccessRight.View)]
public class ProductManagementController : Controller
{
    [HttpGet("Create")]
    [RequirePageAccess("PAB Sites", AccessRight.Add)]
    public IActionResult Create() => View();

    [HttpGet("Edit/{id:int}")]
    [RequirePageAccess("PAB Sites", AccessRight.Edit)]
    public IActionResult Edit(int id) { ... }
}
```

Programmatic checks (e.g. inside an action or view):

```csharp
public class MyController : Controller
{
    private readonly IUserAccessControlService _acl;
    public MyController(IUserAccessControlService acl) { _acl = acl; }

    public IActionResult Index()
    {
        var canEdit = _acl.HasAccess(HttpContext, "PAB Sites", AccessRight.Edit);
        var snapshot = _acl.GetCurrent(HttpContext); // user, roles, full map
        return View(new { canEdit, snapshot });
    }
}
```

## Included Sample Endpoints

- `GET /index` health endpoint
- `GET /api/products`
- `GET /api/products/{id}`
- `POST /api/products`
- `PUT /api/products/{id}`
- `DELETE /api/products/{id}`
- `GET /ProductManagement`

## Recommended Next Steps After Template Clone

1. Replace `ProductManagement` sample module with your real module.
2. Point UI loader URL to your TMS UI Core environment.
3. Set real ACL/Auth/Core API base URLs.
4. Add your branding assets (`logo`, title, etc.).
5. Move sample in-memory services to real data/API-backed services.

