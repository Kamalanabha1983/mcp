using System.Data;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Npgsql;
using PgsqlMcpServer.Options;

namespace PgsqlMcpServer.Database;

public sealed class PostgresService : IDisposable
{
    private readonly PostgresOptions options;
    private readonly ILogger<PostgresService> logger;
    private readonly NpgsqlDataSource dataSource;

    public PostgresService(IOptions<PostgresOptions> options, ILogger<PostgresService> logger)
    {
        this.options = options.Value;
        this.logger = logger;

        if (string.IsNullOrWhiteSpace(this.options.ConnectionString))
        {
            throw new InvalidOperationException("Postgres:ConnectionString must be configured.");
        }

        dataSource = NpgsqlDataSource.Create(this.options.ConnectionString);
    }

    public async Task<bool> CanConnectAsync()
    {
        try
        {
            await using var connection = await dataSource.OpenConnectionAsync();
            return connection.State == ConnectionState.Open;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "PostgreSQL health check failed.");
            return false;
        }
    }

    public async Task<IReadOnlyList<Dictionary<string, object?>>> QueryAsync(
        string sql,
        CancellationToken cancellationToken)
    {
        ValidateReadOnlyQuery(sql);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = options.CommandTimeoutSeconds;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var rows = new List<Dictionary<string, object?>>();

        while (await reader.ReadAsync(cancellationToken) && rows.Count < options.MaxRows)
        {
            var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            for (var column = 0; column < reader.FieldCount; column++)
            {
                row[reader.GetName(column)] = NormalizeValue(reader.GetValue(column));
            }

            rows.Add(row);
        }

        return rows;
    }

    private static void ValidateReadOnlyQuery(string sql)
    {
        var normalized = sql.Trim();
        if (normalized.Length == 0 || normalized.Contains(';'))
        {
            throw new ArgumentException("Only one non-empty read-only SQL statement is allowed.");
        }

        var firstKeyword = normalized.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)[0];
        if (!firstKeyword.Equals("select", StringComparison.OrdinalIgnoreCase)
            && !firstKeyword.Equals("show", StringComparison.OrdinalIgnoreCase)
            && !firstKeyword.Equals("explain", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Only SELECT, SHOW, and EXPLAIN statements are allowed.");
        }
    }

    private static object? NormalizeValue(object value) => value switch
    {
        DBNull => null,
        DateTime dateTime => dateTime.ToUniversalTime().ToString("O"),
        DateTimeOffset dateTimeOffset => dateTimeOffset.ToUniversalTime().ToString("O"),
        byte[] bytes => Convert.ToBase64String(bytes),
        _ => value
    };

    public void Dispose() => dataSource.Dispose();
}
