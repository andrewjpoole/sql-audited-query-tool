using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SqlAuditedQueryTool.Core.Interfaces;

namespace SqlAuditedQueryTool.Database;

public static class DatabaseServiceCollectionExtensions
{
    public static IServiceCollection AddDatabaseServices(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IConnectionFactory, ReadOnlyConnectionFactory>();
        services.AddScoped<IQueryExecutor, SqlQueryExecutor>();
        services.AddSingleton<IQueryHistoryStore, InMemoryQueryHistoryStore>();
        services.AddSingleton<IChatHistoryStore, InMemoryChatHistoryStore>();
        return services;
    }
}
