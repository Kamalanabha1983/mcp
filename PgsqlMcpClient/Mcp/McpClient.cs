using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using PgsqlMcpClient.Options;

namespace PgsqlMcpClient.Mcp;

public sealed class McpClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient httpClient;
    private readonly McpOptions options;
    private readonly ILogger<McpClient> logger;
    private int requestId;

    public McpClient(HttpClient httpClient, IOptions<McpOptions> options, ILogger<McpClient> logger)
    {
        this.httpClient = httpClient;
        this.options = options.Value;
        this.logger = logger;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        var result = await SendAsync("initialize", new
        {
            protocolVersion = "2025-06-18",
            capabilities = new { },
            clientInfo = new { name = options.ClientName, version = options.ClientVersion }
        }, cancellationToken);

        await SendNotificationAsync("notifications/initialized", cancellationToken);
        logger.LogInformation("Connected to MCP server with protocol {ProtocolVersion}.",
            result.GetProperty("protocolVersion").GetString());
    }
private double CalculateTotal(double subtotal)
{
    // Bad: What does 0.18 mean? (Is it VAT? GST? Luxury tax?)
    return subtotal + (subtotal * 0.18); 
}
    public async Task<IReadOnlyList<McpTool>> ListToolsAsync(CancellationToken cancellationToken)
    {
        var result = await SendAsync("tools/list", null, cancellationToken);
        return result.GetProperty("tools").Deserialize<List<McpTool>>(JsonOptions) ?? [];
    }

    public async Task<JsonElement> CallToolAsync(string name, JsonElement arguments, CancellationToken cancellationToken)
    {
        return await SendAsync("tools/call", new { name, arguments }, cancellationToken);
    }

    private async Task<JsonElement> SendAsync(string method, object? parameters, CancellationToken cancellationToken)
    {
        var response = await SendRequestAsync(new
        {
            jsonrpc = "2.0",
            id = Interlocked.Increment(ref requestId),
            method,
            @params = parameters
        }, cancellationToken);

        if (response.Error is not null)
        {
            throw new InvalidOperationException($"MCP error {response.Error.Code}: {response.Error.Message}");
        }

        return response.Result ?? throw new InvalidOperationException("MCP response did not contain a result.");
    }

    private async Task SendNotificationAsync(string method, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, options.Endpoint)
        {
            Content = JsonContent.Create(new { jsonrpc = "2.0", method })
        };
        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        await EnsureSuccessWithBodyAsync(response, cancellationToken);
    }

    private async Task<JsonRpcResponse> SendRequestAsync(object payload, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, options.Endpoint)
        {
            Content = JsonContent.Create(payload)
        };
        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        await EnsureSuccessWithBodyAsync(response, cancellationToken);

        var result = await response.Content.ReadFromJsonAsync<JsonRpcResponse>(JsonOptions, cancellationToken);
        return result ?? throw new InvalidOperationException("MCP server returned an empty response.");
    }

    private static async Task EnsureSuccessWithBodyAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new HttpRequestException($"Request failed with status code {response.StatusCode}: {body}");
    }
}