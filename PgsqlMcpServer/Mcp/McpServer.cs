using System.Text.Json;
using PgsqlMcpServer.Database;

namespace PgsqlMcpServer.Mcp;

public sealed class McpServer
{
    private readonly PostgresService database;
    private readonly ILogger<McpServer> logger;

    public McpServer(PostgresService database, ILogger<McpServer> logger)
    {
        this.database = database;
        this.logger = logger;
    }

    public async Task<JsonRpcResponse?> HandleAsync(Stream body, CancellationToken cancellationToken)
    {
        try
        {
            using var document = await JsonDocument.ParseAsync(body, cancellationToken: cancellationToken);
            var request = document.RootElement.Deserialize<JsonRpcRequest>();
            if (request is null || request.JsonRpc != "2.0" || string.IsNullOrWhiteSpace(request.Method))
            {
                return Error(null, -32600, "Invalid JSON-RPC request.");
            }

            if (request.Method == "notifications/initialized" || request.Method.StartsWith("notifications/", StringComparison.Ordinal))
            {
                return null;
            }

            return await DispatchAsync(request, cancellationToken);
        }
        catch (JsonException exception)
        {
            logger.LogWarning(exception, "Invalid JSON received on the MCP endpoint.");
            return Error(null, -32700, "Parse error.");
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unhandled MCP request error.");
            return Error(null, -32603, "Internal error.");
        }
    }

    private async Task<JsonRpcResponse> DispatchAsync(JsonRpcRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return request.Method switch
            {
                "initialize" => Success(request, InitializeResult()),
                "ping" => Success(request, new { }),
                "tools/list" => Success(request, ToolsResult()),
                "resources/list" => Success(request, ResourcesResult()),
                "prompts/list" => Success(request, new { prompts = Array.Empty<McpPrompt>() }),
                "tools/call" => await CallToolAsync(request, cancellationToken),
                "resources/read" => await ReadResourceAsync(request, cancellationToken),
                _ => Error(request.Id, -32601, $"Method '{request.Method}' not found.")
            };
        }
        catch (ArgumentException exception)
        {
            return Error(request.Id, -32602, exception.Message);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "MCP method {Method} failed.", request.Method);
            return Error(request.Id, -32603, "Request failed.");
        }
    }

    private async Task<JsonRpcResponse> CallToolAsync(JsonRpcRequest request, CancellationToken cancellationToken)
    {
        var arguments = GetObject(request.Params, "arguments");
        var toolName = GetString(request.Params, "name");
        if (toolName != "query")
        {
            return Error(request.Id, -32602, "Unknown tool. Use 'query'.");
        }

        var sql = GetString(arguments, "sql");
        var rows = await database.QueryAsync(sql, cancellationToken);
        var payload = JsonSerializer.Serialize(new { rowCount = rows.Count, rows });
        return Success(request, new { content = new[] { new McpContent("text", payload) }, isError = false });
    }

    private async Task<JsonRpcResponse> ReadResourceAsync(JsonRpcRequest request, CancellationToken cancellationToken)
    {
        var uri = GetString(request.Params, "uri");
        if (uri != "database://schema")
        {
            return Error(request.Id, -32602, "Unknown resource URI.");
        }

        const string sql = "SELECT table_schema, table_name, column_name, ordinal_position, data_type, is_nullable FROM information_schema.columns WHERE table_schema NOT IN ('pg_catalog', 'information_schema') ORDER BY table_schema, table_name, ordinal_position";
        var rows = await database.QueryAsync(sql, cancellationToken);
        var payload = JsonSerializer.Serialize(new
        {
            database = "configured PostgreSQL database",
            tables = rows
        });
        return Success(request, new
        {
            contents = new[] { new { uri, mimeType = "application/json", text = payload } }
        });
    }

    private static object InitializeResult() => new
    {
        protocolVersion = "2025-06-18",
        capabilities = new { resources = new { }, tools = new { }, prompts = new { } },
        serverInfo = new { name = "pgsql-mcp-server", version = "1.0.0" }
    };

    private static object ToolsResult() => new
    {
        tools = new[]
        {
            new McpTool(
                "query",
                "Execute one read-only SELECT, SHOW, or EXPLAIN statement against the configured database.",
                new
                {
                    type = "object",
                    properties = new { sql = new { type = "string", description = "A single read-only SQL statement." } },
                    required = new[] { "sql" },
                    additionalProperties = false
                })
        }
    };

    private static object ResourcesResult() => new
    {
        resources = new[]
        {
            new McpResource(
                "database://schema",
                "Database schema",
                "Discover user-visible tables, columns, data types, and nullability in the configured database.",
                "application/json")
        }
    };

    private static JsonRpcResponse Success(JsonRpcRequest request, object result) =>
        new("2.0", request.Id, result);

    private static JsonRpcResponse Error(JsonElement? id, int code, string message) =>
        new("2.0", id, Error: new JsonRpcError(code, message));

    private static JsonElement GetObject(JsonElement? parameters, string propertyName)
    {
        if (parameters is { ValueKind: JsonValueKind.Object } value && value.TryGetProperty(propertyName, out var property))
        {
            return property;
        }

        return default;
    }

    private static string GetString(JsonElement? parameters, string propertyName) =>
        GetString(parameters is { } value ? value : default, propertyName);

    private static string GetString(JsonElement parameters, string propertyName)
    {
        if (parameters.ValueKind == JsonValueKind.Object
            && parameters.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(property.GetString()))
        {
            return property.GetString()!;
        }

        throw new ArgumentException($"Missing required string property '{propertyName}'.");
    }
}
