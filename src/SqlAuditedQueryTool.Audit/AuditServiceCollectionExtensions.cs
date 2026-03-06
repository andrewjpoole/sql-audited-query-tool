using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SqlAuditedQueryTool.Core.Interfaces;

namespace SqlAuditedQueryTool.Audit;

public static class AuditServiceCollectionExtensions
{
    public static IServiceCollection AddAuditServices(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpClient("AzDoAudit");
        services.AddSingleton<GitHubAuditLogger>();
        services.AddSingleton<AzDoAuditLogger>();
        services.AddScoped<IAuditLogger, CompositeAuditLogger>();
        return services;
    }
}
