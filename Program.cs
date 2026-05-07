using tms_template_net8.Jwt;
using tms_template_net8.Services;
using TMS.WebApp.Sdk.DependencyInjection;
using tms_template_net8.AccessValidation;

var builder = WebApplication.CreateBuilder(args);
await PublicPemSync.SyncAsync(builder.Configuration, builder.Environment);

// Add services to the container.
builder.Services.AddControllersWithViews();

builder.Services.AddAuthServices(builder.Configuration, builder.Environment);
builder.Services.AddHttpClient();
builder.Services.AddHttpClient("Vasp", (sp, client) =>
{
    var baseUrl = sp.GetRequiredService<IConfiguration>()["Vasp:BaseUrl"];
    if (string.IsNullOrWhiteSpace(baseUrl))
        throw new InvalidOperationException("Configuration 'Vasp:BaseUrl' is required.");
    client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
});
builder.Services.AddScoped<IAuthTokenRefreshService, AuthTokenRefreshService>();
builder.Services.AddScoped<IACLService, ACLService>();
builder.Services.AddScoped<IUserAccessControlService, UserAccessControlService>();
builder.Services.AddTmsWebAppSdk(builder.Configuration, opts =>
{
    // Optional: override options after binding from configuration
});
builder.Services.AddScoped<ICoreAPIService, CoreAPIService>();

// register module for app specific services
builder.Services.AddSingleton<IProductService, ProductService>();


builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.IdleTimeout = TimeSpan.FromMinutes(30);
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSession();

// Canonical ACL entry is /ACLChecking/?... (not /). Redirect bare / so wrong redirect URIs still work.
app.Use((ctx, next) =>
{
    if (ctx.Request.Path != "/")
        return next();
    var basePath = ctx.Request.PathBase.HasValue ? ctx.Request.PathBase.ToString().TrimEnd('/') : "";
    ctx.Response.Redirect($"{basePath}/ACLChecking{ctx.Request.QueryString}");
    return Task.CompletedTask;
});

app.UseAccessTokenValidation();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapControllers();
app.Run();
