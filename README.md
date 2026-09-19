# PostgreSQL MCP Agent Client

This ASP.NET Core 9 application hosts the chat frontend and runs the Microsoft Agent Framework in-process. It discovers MCP tools and the database schema, sends natural-language requests to Azure OpenAI, and routes read-only query calls to the local PostgreSQL MCP server.

## Requirements

- .NET 9 SDK or newer
- The `PgsqlMcpServer` running at `http://localhost:5000/mcp`
- An Azure OpenAI resource and chat model deployment
- A valid MCP JWT access token

## Configure Azure OpenAI

For API-key authentication, set these variables before starting the client:

```powershell
$env:AzureOpenAI__Endpoint = "https://<resource>.openai.azure.com/"
$env:AzureOpenAI__DeploymentName = "<chat-deployment-name>"
$env:AzureOpenAI__UseManagedIdentity = "false"
$env:AzureOpenAI__ApiKey = "<api-key>"
```

For managed identity, use:

```powershell
$env:AzureOpenAI__Endpoint = "https://<resource>.openai.azure.com/"
$env:AzureOpenAI__DeploymentName = "<chat-deployment-name>"
$env:AzureOpenAI__UseManagedIdentity = "true"
```

## Configure MCP access

The MCP endpoint defaults to `http://localhost:5000/mcp`. Start the server first, then generate a development token in the same PowerShell session:

```powershell
$env:Mcp__Endpoint = "http://localhost:5000/mcp"
$env:Mcp__AccessToken = (Invoke-RestMethod http://localhost:5000/auth/token -Method Post).access_token
```

The client reads the database schema during startup. This lets the agent map terms such as `DOJ`, `date of joining`, `job title`, and `name` to the actual table and column names. After a query runs, the chat response exposes the exact SQL in an expandable `Executed query` section.

## Build and run

Start `PgsqlMcpServer` first in a separate terminal. Then open a second PowerShell terminal in this directory:

```powershell
dotnet restore
dotnet build
dotnet run --launch-profile PgsqlMcpClient
```

The client listens at `http://localhost:5100`. Open that URL in a browser.

You can also run without the launch profile:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run --no-launch-profile --urls http://localhost:5100
```

## Verify the client API

The backend accepts chat requests at `POST http://localhost:5100/api/chat`:

```powershell
Invoke-RestMethod http://localhost:5100/api/chat -Method Post -ContentType "application/json" -Body (@{
  message = "Show the names of employees"
} | ConvertTo-Json)
```

The response contains the agent answer and, after a successful database query, the exact SQL sent to MCP:

```json
{
  "message": "...",
  "query": "SELECT first_name, second_name FROM employees"
}
```

## Startup checklist

1. Start PostgreSQL.
2. Start `PgsqlMcpServer` on port `5000`.
3. Verify `Invoke-RestMethod http://localhost:5000/health`.
4. Generate and set `Mcp__AccessToken`.
5. Set the Azure OpenAI variables.
6. Start `PgsqlMcpClient` on port `5100`.
7. Open `http://localhost:5100/`.

## Troubleshooting

- `This site can't be reached`: confirm the client is running and open `http://localhost:5100`, not the MCP server port.
- `Connection refused on localhost:5000`: start the MCP server first and verify its `/health` endpoint.
- `MCP error 401/403`: generate a new token from `/auth/token` and set `Mcp__AccessToken` in the same terminal used to start the client.
- `address already in use`: stop the existing process or use another client port and open that URL.
- The query section is empty: submit a new chat request after rebuilding and restarting the client; the SQL is captured when the MCP query tool runs.

For a React server that is itself ASP.NET Core, copy the service registrations and endpoint mapping from `Program.cs` into that host instead of running this project separately. The `McpClient` and `AgentService` are ordinary DI services and do not require a separate process.
