namespace tms_template_net8.Services;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers app-local services. Add new module/business services here instead of in Program.cs.
    /// </summary>
    public static IServiceCollection AddAppServices(this IServiceCollection services)
    {
        services.AddSingleton<IProductService, ProductService>();
        services.AddScoped<IReportService, ReportService>();
        return services;
    }
}
