using System.Text.Json;
using System.Text.RegularExpressions;

namespace SqlAuditedQueryTool.Core.Security;

public sealed class DataLeakViolation
{
    public required string Rule { get; init; }
    public required string Description { get; init; }
    public string? Path { get; init; }
}

public sealed class DataLeakReport
{
    public bool IsClean => Violations.Count == 0;
    public IReadOnlyList<DataLeakViolation> Violations { get; init; } = [];
}

/// <summary>
/// Ensures payloads sent to the LLM contain only schema metadata
/// and never actual row data or PII patterns.
/// </summary>
public static class DataLeakPrevention
{
    // PII detection patterns
    private static readonly Regex EmailPattern = new(
        @"[a-zA-Z0-9._%+\-]+@[a-zA-Z0-9.\-]+\.[a-zA-Z]{2,}", RegexOptions.Compiled);

    private static readonly Regex SsnPattern = new(
        @"\b\d{3}-\d{2}-\d{4}\b", RegexOptions.Compiled);

    private static readonly Regex PhonePattern = new(
        @"\b(?:\+?1[-.\s]?)?\(?\d{3}\)?[-.\s]?\d{3}[-.\s]?\d{4}\b", RegexOptions.Compiled);

    private static readonly Regex CreditCardPattern = new(
        @"\b(?:\d{4}[-\s]?){3}\d{4}\b", RegexOptions.Compiled);

    // GUIDs that could be row identifiers
    private static readonly Regex GuidPattern = new(
        @"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\b",
        RegexOptions.Compiled);

    // Threshold for large string arrays that look like row data
    private const int RowDataArrayThreshold = 5;

    /// <summary>
    /// Validates that a payload intended for LLM consumption contains only
    /// schema metadata and no actual data values. Returns true if clean.
    /// </summary>
    public static bool ValidateLlmPayload(object? payload)
    {
        return InspectPayload(payload).IsClean;
    }

    /// <summary>
    /// Returns a detailed report of any data leak violations found.
    /// </summary>
    public static DataLeakReport InspectPayload(object? payload)
    {
        if (payload is null)
            return new DataLeakReport();

        var violations = new List<DataLeakViolation>();
        var json = JsonSerializer.Serialize(payload);

        ScanString(json, "root", violations);
        ScanStructure(payload, "root", violations);

        return new DataLeakReport { Violations = violations };
    }

    private static void ScanString(string text, string path, List<DataLeakViolation> violations)
    {
        if (EmailPattern.IsMatch(text))
        {
            violations.Add(new DataLeakViolation
            {
                Rule = "PII_EMAIL",
                Description = "Email address pattern detected in payload.",
                Path = path
            });
        }

        if (SsnPattern.IsMatch(text))
        {
            violations.Add(new DataLeakViolation
            {
                Rule = "PII_SSN",
                Description = "SSN pattern (###-##-####) detected in payload.",
                Path = path
            });
        }

        if (PhonePattern.IsMatch(text))
        {
            violations.Add(new DataLeakViolation
            {
                Rule = "PII_PHONE",
                Description = "Phone number pattern detected in payload.",
                Path = path
            });
        }

        if (CreditCardPattern.IsMatch(text))
        {
            violations.Add(new DataLeakViolation
            {
                Rule = "PII_CREDIT_CARD",
                Description = "Credit card number pattern detected in payload.",
                Path = path
            });
        }

        if (GuidPattern.IsMatch(text))
        {
            violations.Add(new DataLeakViolation
            {
                Rule = "POTENTIAL_ROW_ID",
                Description = "GUID pattern detected — may be a row identifier.",
                Path = path
            });
        }
    }

    private static void ScanStructure(object obj, string path, List<DataLeakViolation> violations)
    {
        // Serialize to JsonElement for structural inspection
        var json = JsonSerializer.Serialize(obj);
        using var doc = JsonDocument.Parse(json);
        ScanElement(doc.RootElement, path, violations);
    }

    private static void ScanElement(JsonElement element, string path, List<DataLeakViolation> violations)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Array:
                var items = element.EnumerateArray().ToList();
                // Flag large string arrays as potential row data
                if (items.Count >= RowDataArrayThreshold &&
                    items.All(i => i.ValueKind == JsonValueKind.String))
                {
                    violations.Add(new DataLeakViolation
                    {
                        Rule = "ROW_DATA_ARRAY",
                        Description = $"Large string array ({items.Count} items) — may contain row data.",
                        Path = path
                    });
                }

                for (int i = 0; i < items.Count; i++)
                    ScanElement(items[i], $"{path}[{i}]", violations);
                break;

            case JsonValueKind.Object:
                foreach (var prop in element.EnumerateObject())
                    ScanElement(prop.Value, $"{path}.{prop.Name}", violations);
                break;

            case JsonValueKind.String:
                var value = element.GetString();
                if (value is not null)
                    ScanString(value, path, violations);
                break;
        }
    }
}
