namespace PgsqlMcpClient.Options;

public sealed class McpOptions
{
    public const string SectionName = "Mcp";

    public string Endpoint { get; set; } = "http://localhost:5000/mcp";

    public string ClientName { get; set; } = "pgsql-mcp-agent-client";

    public string ClientVersion { get; set; } = "1.0.0";
}