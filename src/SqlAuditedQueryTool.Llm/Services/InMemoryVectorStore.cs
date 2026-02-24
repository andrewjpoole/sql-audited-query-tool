using SqlAuditedQueryTool.Core.Interfaces.Llm;
using SqlAuditedQueryTool.Core.Models.Llm;
using System.Collections.Concurrent;

namespace SqlAuditedQueryTool.Llm.Services;

public class InMemoryVectorStore : IVectorStore
{
    private readonly ConcurrentDictionary<string, (float[] Embedding, VectorMetadata Metadata)> _store = new();

    public Task UpsertAsync(string key, float[] embedding, VectorMetadata metadata, CancellationToken ct = default)
    {
        _store[key] = (embedding, metadata);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<VectorSearchResult>> SearchAsync(
        float[] query, 
        int topK = 10, 
        string? category = null, 
        CancellationToken ct = default)
    {
        // Filter by category if specified
        var items = category == null
            ? _store
            : _store.Where(kvp => kvp.Value.Metadata.Category == category);

        // Compute cosine similarity for each item
        var results = items
            .Select(kvp => new
            {
                Key = kvp.Key,
                Embedding = kvp.Value.Embedding,
                Metadata = kvp.Value.Metadata,
                Score = CosineSimilarity(query, kvp.Value.Embedding)
            })
            .OrderByDescending(x => x.Score)
            .Take(topK)
            .Select(x => new VectorSearchResult
            {
                Key = x.Key,
                Embedding = x.Embedding,
                Metadata = x.Metadata,
                Score = x.Score
            })
            .ToList();

        return Task.FromResult<IReadOnlyList<VectorSearchResult>>(results);
    }

    public Task RemoveAsync(string key, CancellationToken ct = default)
    {
        _store.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    private static float CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length)
            return 0f;

        var dotProduct = 0f;
        var magnitudeA = 0f;
        var magnitudeB = 0f;

        for (var i = 0; i < a.Length; i++)
        {
            dotProduct += a[i] * b[i];
            magnitudeA += a[i] * a[i];
            magnitudeB += b[i] * b[i];
        }

        magnitudeA = MathF.Sqrt(magnitudeA);
        magnitudeB = MathF.Sqrt(magnitudeB);

        if (magnitudeA == 0f || magnitudeB == 0f)
            return 0f;

        return dotProduct / (magnitudeA * magnitudeB);
    }
}
