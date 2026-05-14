using tms_template_net8.AccessValidation;
using tms_template_net8.Jwt;
using tms_template_net8.Services;
using TMS.WebApp.Sdk.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);
await PublicPemSync.SyncAsync(builder.Configuration, builder.Environment);

builder.Services.AddControllersWithViews();
builder.Services.AddHttpClient();

builder.Services.AddAuthAndAcl(builder.Configuration, builder.Environment);
builder.Services.AddAppServices();
builder.Services.AddTmsWebAppSdk(builder.Configuration, opts =>
{
    // Keep local development simple: fall back to ConnectionStrings:Default
    // when no explicit logical connection name is configured.
    if (string.IsNullOrWhiteSpace(opts.ConnectionString.DefaultName))
    {
        opts.ConnectionString.DefaultName = "Default";
    }
});

// Optional: resolve DB connection strings via the remote ACL endpoint when configured.
// if (!string.IsNullOrWhiteSpace(builder.Configuration["TmsSdk:ConnectionString:RemoteResolverUrl"]))
// {
//     builder.Services.UseRemoteAclConnectionProvider();
// }

// Optional: log SDK errors through the configured SQL stored procedure.
if (!string.IsNullOrWhiteSpace(builder.Configuration["TmsSdk:ErrorLog:StoredProcedureName"]))
{
    builder.Services.UseSqlErrorLogger();
}

builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.IdleTimeout = TimeSpan.FromMinutes(30);
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseRootAclRedirect();
app.UseAccessTokenValidation();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapControllers();
app.Run();
