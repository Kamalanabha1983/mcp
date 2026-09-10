namespace PgsqlMcpServer.Options;

public sealed class PostgresOptions
{
    public const string SectionName = "Postgres";

    public string ConnectionString { get; set; } = string.Empty;

    public int CommandTimeoutSeconds { get; set; } = 30;

    public int MaxRows { get; set; } = 1000;
}
