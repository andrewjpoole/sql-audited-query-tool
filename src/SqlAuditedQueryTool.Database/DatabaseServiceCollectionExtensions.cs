using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SqlAuditedQueryTool.Core.Interfaces;
using SqlAuditedQueryTool.Core.Models;

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
        
        // Write query simulation services
        services.AddScoped<ISimulationService, SimulationService>();
        services.AddSingleton<IScriptGeneratorService, ScriptGeneratorService>();
        
        // Bind SqlScriptRunner options
        var sqlScriptRunnerSection = configuration.GetSection(SqlScriptRunnerOptions.SectionName);
        services.Configure<SqlScriptRunnerOptions>(sqlScriptRunnerSection);
        
        return services;
    }
}
