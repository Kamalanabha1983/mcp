using PgsqlMcpServer.Database;
using PgsqlMcpServer.Mcp;
using PgsqlMcpServer.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<PostgresOptions>(
	builder.Configuration.GetSection(PostgresOptions.SectionName));
builder.Services.AddSingleton<PostgresService>();
builder.Services.AddSingleton<McpServer>();

var app = builder.Build();

app.UseExceptionHandler(exceptionApp =>
{
	exceptionApp.Run(async context =>
	{
		context.Response.StatusCode = StatusCodes.Status500InternalServerError;
		await Results.Json(new
		{
			error = new { code = -32603, message = "Internal server error." }
		}).ExecuteAsync(context);
	});
});

app.MapGet("/", () => Results.Ok(new
{
	name = "pgsql-mcp-server",
	protocol = "MCP over JSON-RPC 2.0",
	endpoint = "/mcp"
}));

app.MapGet("/health", (PostgresService database) =>
	database.CanConnectAsync());

app.MapPost("/mcp", async (HttpContext context, McpServer server, CancellationToken cancellationToken) =>
{
	var response = await server.HandleAsync(context.Request.Body, cancellationToken);

	if (response is null)
	{
		return Results.NoContent();
	}

	context.Response.ContentType = "application/json";
	return Results.Json(response);
});

app.Run();
