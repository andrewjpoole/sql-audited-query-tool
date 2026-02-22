using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SqlAuditedQueryTool.Core.Interfaces.Llm;
using SqlAuditedQueryTool.Llm.Configuration;
using SqlAuditedQueryTool.Llm.Services;

namespace SqlAuditedQueryTool.Llm;

public static class LlmServiceCollectionExtensions
{
    public static IServiceCollection AddLlmServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<OllamaOptions>(configuration.GetSection(OllamaOptions.SectionName));

        // IChatClient is registered by Aspire's AddOllamaApiClient in Program.cs
        services.AddScoped<ILlmService, OllamaLlmService>();

        services.AddScoped<IQueryAssistant, LlmQueryAssistant>();
        services.AddScoped<ISchemaProvider, SchemaMetadataProvider>();
        services.AddMemoryCache();

        return services;
    }
}
