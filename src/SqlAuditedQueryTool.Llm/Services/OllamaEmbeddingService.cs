using OllamaSharp;
using SqlAuditedQueryTool.Core.Interfaces.Llm;
using System.Text.Json;
using System.Net.Http.Json;

namespace SqlAuditedQueryTool.Llm.Services;

public class OllamaEmbeddingService : IEmbeddingService
{
    private readonly HttpClient _httpClient;
    private readonly string _modelName;

    public OllamaEmbeddingService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _modelName = "nomic-embed-text";
    }

    public async Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
    {
        var request = new
        {
            model = _modelName,
            prompt = text
        };

        var response = await _httpClient.PostAsJsonAsync("/api/embeddings", request, ct);
        response.EnsureSuccessStatusCode();
        
        var result = await response.Content.ReadFromJsonAsync<EmbeddingResponse>(ct);
        
        if (result?.Embedding == null)
            throw new InvalidOperationException("Embedding response was null");
        
        // Convert double[] to float[]
        return result.Embedding.Select(d => (float)d).ToArray();
    }

    public async Task<IReadOnlyList<float[]>> EmbedBatchAsync(IEnumerable<string> texts, CancellationToken ct = default)
    {
        var embeddings = new List<float[]>();
        
        // Process in batches to avoid overwhelming Ollama
        foreach (var text in texts)
        {
            var embedding = await EmbedAsync(text, ct);
            embeddings.Add(embedding);
        }
        
        return embeddings;
    }

    private class EmbeddingResponse
    {
        public double[] Embedding { get; set; } = Array.Empty<double>();
    }
}
