namespace SqlAuditedQueryTool.Llm.Configuration;

public sealed class OllamaOptions
{
    public const string SectionName = "Llm";

    public string BaseUrl { get; set; } = "http://localhost:11434";
    public string Model { get; set; } = "qwen2.5-coder:7b";
    public int SchemaCacheMinutes { get; set; } = 5;

    public TimeSpan SchemaCacheDuration => TimeSpan.FromMinutes(SchemaCacheMinutes);
}
