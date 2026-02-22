using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace SqlAuditedQueryTool.AppHost;

public static class DatabaseSeeder
{
    public static IResourceBuilder<SqlServerDatabaseResource> WithDatabaseSeeding(
        this IResourceBuilder<SqlServerDatabaseResource> database, string seedPath)
    {
        return database.OnResourceReady(async (resource, evt, ct) =>
        {
            var logger = evt.Services.GetRequiredService<ILogger<Program>>();
            logger.LogInformation("Database is ready — seeding...");

            var connectionString = await resource.ConnectionStringExpression.GetValueAsync(ct);
            var seedSql = await File.ReadAllTextAsync(seedPath, ct);

            // Split on GO batch separators (must appear on their own line)
            var batches = Regex.Split(seedSql, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase)
                .Where(b => !string.IsNullOrWhiteSpace(b));

            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(ct);

            foreach (var batch in batches)
            {
                await using var command = connection.CreateCommand();
                command.CommandText = batch;
                command.CommandTimeout = 60;
                await command.ExecuteNonQueryAsync(ct);
            }

            logger.LogInformation("Database seeding completed successfully.");
        });
    }
}
