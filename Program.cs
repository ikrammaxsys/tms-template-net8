using AuthACL.CentralAuth.Jwt;
using AuthACL.CentralAuth.AccessValidation;
using tms_template_net8.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

builder.Services.AddAuthServices(builder.Configuration, builder.Environment);
builder.Services.AddHttpClient();
builder.Services.AddScoped<IAuthTokenRefreshService, AuthTokenRefreshService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IACLService, ACLService>();
builder.Services.AddSession();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseSession();
app.UseAuthentication();
app.UseAuthorization();
app.UseAccessTokenValidation();

app.UseHttpsRedirection();
app.UseRouting();

app.MapControllers();
app.Run();
