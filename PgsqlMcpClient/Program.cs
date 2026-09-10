using PgsqlMcpClient.Agent;
using PgsqlMcpClient.Mcp;
using PgsqlMcpClient.Models;
using PgsqlMcpClient.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<McpOptions>(builder.Configuration.GetSection(McpOptions.SectionName));
builder.Services.Configure<AzureOpenAiOptions>(builder.Configuration.GetSection(AzureOpenAiOptions.SectionName));
builder.Services.AddHttpClient<McpClient>();
builder.Services.AddSingleton<AgentService>();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapPost("/api/chat", async (ChatRequest request, AgentService agent, CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Message))
    {
        return Results.BadRequest(new { error = "message is required" });
    }

    try
    {
        var response = await agent.RunAsync(request.Message, cancellationToken);
        return Results.Ok(new ChatResponse(response));
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
        return Results.StatusCode(StatusCodes.Status499ClientClosedRequest);
    }
    catch (Exception exception)
    {
        app.Logger.LogError(exception, "Agent chat request failed.");
        return Results.Json(
            new { error = exception.Message },
            statusCode: StatusCodes.Status502BadGateway);
    }
});

app.MapFallbackToFile("index.html");

app.Run();