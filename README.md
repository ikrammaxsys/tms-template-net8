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
  - Service registration
  - HTTP client setup for ACL and Core API calls
  - Authentication + authorization middleware setup
  - ACL bootstrap redirect (`/` -> `/ACLChecking`)
- `Auth/`
  - JWT key loading and validation services
  - Access-token validation middleware
  - Token refresh service hooks
- `Controllers/ACLCheckingController.cs`
  - Entry point for ACL callback/query parameters
  - Auth code exchange flow
  - Token verification flow
  - Session population (`gstrUserID`, `gstrUserName`)
  - Logout and session cleanup flow
- `Services/`
  - `ACLService`: VASP/ACL user lookup
  - `CoreAPIService`: sample call to TMS Core API (`/api/v1/status`)
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

Primary runtime settings are in `appsettings.json`:

- `Jwt`
  - `RsaKeyPath`
  - `Issuer`
  - `Audience`
- `Auth`
  - `BaseUrl`
  - `VerifyTokenUrl`
  - `ExchangeAuthCodeUrl`
  - `RefreshTokenApiUrl`
  - `LogoutApiUrl`
  - `AccessTokenStorageKey`
  - `RefreshTokenStorageKey`
- `Vasp`
  - `BaseUrl` (ACL/VASP UI base URL)
- `UiFoundation`
  - `BaseUrl` (TMS UI web component loader base URL)
- `CoreApi`
  - `BaseUrl` (TMS Core API base URL)

Optional/used by views:

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
- Register service in `Program.cs`

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
2. Register required services in `Program.cs`
3. Wrap usage behind project-local service interfaces in `Services/Interfaces`

### TMS Core API

`CoreAPIService` is the template entry point for Core API integration.  
Configure `CoreApi:BaseUrl` and add new methods following `GetStatusAsync`.

### ACL V2 (Authorization)

ACL V2 behavior is implemented by:

- `ACLCheckingController`
- `AccessTokenValidationMiddleware`
- JWT service registration in `AuthServiceExtensions`
- Token refresh service (`AuthTokenRefreshService`)

If your ACL endpoints or payload contracts differ, update:

- `Auth` section values
- `ACLCheckingController` parsing/mapping logic
- `IACLService`/`ACLService` request paths

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

