namespace tms_template_net8.Services;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the template's example/business services. Add new module services here
    /// instead of in Program.cs.
    /// </summary>
    public static IServiceCollection AddAppServices(this IServiceCollection services)
    {
        services.AddScoped<IACLService, ACLService>();
        services.AddScoped<ICoreAPIService, CoreAPIService>();
        services.AddScoped<IReportService, ReportService>();
        services.AddSingleton<IProductService, ProductService>();
        return services;
    }
}
