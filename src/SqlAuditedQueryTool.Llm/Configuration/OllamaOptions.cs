namespace SqlAuditedQueryTool.Llm.Configuration;

public sealed class OllamaOptions
{
    public const string SectionName = "Llm";

    public string BaseUrl { get; set; } = "http://localhost:11434";
    public string Model { get; set; } = "qwen2.5-coder:7b";
    public int SchemaCacheMinutes { get; set; } = 5;
    public int ChatTimeoutSeconds { get; set; } = 120;

    /// <summary>
    /// Enable thinking mode for reasoning models (e.g. qwen3.5). Default false for speed on small models.
    /// When null/not set on the request, thinking models may still emit inline &lt;think&gt; tags.
    /// </summary>
    public bool ThinkingEnabled { get; set; }

    public TimeSpan SchemaCacheDuration => TimeSpan.FromMinutes(SchemaCacheMinutes);
    public TimeSpan ChatTimeout => TimeSpan.FromSeconds(ChatTimeoutSeconds);
}
