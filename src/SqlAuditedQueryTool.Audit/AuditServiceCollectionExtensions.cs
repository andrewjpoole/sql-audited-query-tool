using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SqlAuditedQueryTool.Core.Interfaces;

namespace SqlAuditedQueryTool.Audit;

public static class AuditServiceCollectionExtensions
{
    public static IServiceCollection AddAuditServices(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IAuditLogger, GitHubAuditLogger>();
        return services;
    }
}
