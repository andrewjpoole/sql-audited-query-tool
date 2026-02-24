using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OllamaSharp;
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

        // Embedding services - uses the ollamaEmbed IOllamaApiClient to get base URL
        services.AddSingleton<IEmbeddingService>(sp =>
        {
            // Get all Ollama clients (first is ollamaModel, second is ollamaEmbed)
            var allOllamaClients = sp.GetServices<IOllamaApiClient>().ToList();
            if (allOllamaClients.Count < 2)
            {
                throw new InvalidOperationException("Expected at least 2 Ollama clients (ollamaModel and ollamaEmbed)");
            }
            
            // Get the second client which is ollamaEmbed
            var embedClient = allOllamaClients[1];
            
            // Create HttpClient with the base address from the Ollama client
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient();
            httpClient.BaseAddress = embedClient.Uri;
            httpClient.Timeout = TimeSpan.FromMinutes(5);
            
            return new OllamaEmbeddingService(httpClient);
        });
        services.AddSingleton<IVectorStore, InMemoryVectorStore>();
        services.AddScoped<ICompletionService, EmbeddingCompletionService>();
        
        // Background service for schema embedding
        services.AddHostedService<SchemaEmbeddingService>();

        return services;
    }
}
