namespace tms_template_net8.Integrations;

public static class IntegrationServiceCollectionExtensions
{
    /// <summary>
    /// Registers services that talk to systems outside this web app (HTTP APIs, SDK-backed data access, etc.).
    /// </summary>
    public static IServiceCollection AddExternalIntegrations(this IServiceCollection services)
    {
        services.AddScoped<IACLService, ACLService>();
        services.AddScoped<ICoreAPIService, CoreAPIService>();
        return services;
    }
}
