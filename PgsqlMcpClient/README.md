# PostgreSQL MCP Agent Client

This C# .NET 9 ASP.NET Core application runs the Microsoft Agent Framework in-process with a React backend. It discovers tools from the local MCP server, registers every discovered tool with the agent, and routes tool calls back to MCP over JSON-RPC.

## Configure

Set these environment variables before starting the client:

```powershell
$env:AzureOpenAI__Endpoint = "https://<resource>.openai.azure.com/"
$env:AzureOpenAI__DeploymentName = "<chat-deployment-name>"
$env:AzureOpenAI__UseManagedIdentity = "true"
```

With API-key authentication instead:

```powershell
$env:AzureOpenAI__UseManagedIdentity = "false"
$env:AzureOpenAI__ApiKey = "<api-key>"
```

The MCP endpoint defaults to `http://localhost:5000/mcp` and can be changed with `Mcp__Endpoint`.

## Run

Start `PgsqlMcpServer` first, then run this project:

```powershell
dotnet restore
dotnet run --urls http://localhost:5100
```

The React backend can call the agent in-process by forwarding a chat request to `POST http://localhost:5100/api/chat`:

```json
{ "message": "Which database am I connected to?" }
```

The response is:

```json
{ "message": "..." }
```

For a React server that is itself ASP.NET Core, copy the service registrations and endpoint mapping from `Program.cs` into that host instead of running this project separately. The `McpClient` and `AgentService` are ordinary DI services and do not require a separate process.