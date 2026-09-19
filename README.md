# PostgreSQL MCP Server

This ASP.NET Core 9 application exposes a PostgreSQL database through an MCP JSON-RPC endpoint.

## Requirements

- .NET 9 SDK or newer
- PostgreSQL 12 or newer
- A PostgreSQL database and user that can read the required tables

## Configure PostgreSQL

From `PgsqlMcpServer`, set the connection details for the current PowerShell session. This avoids storing a password in source control:

```powershell
$env:Postgres__ConnectionString = "Host=localhost;Port=5432;Database=Employee;Username=postgres;Password=your-password"
$env:Postgres__CommandTimeoutSeconds = "30"
$env:Postgres__MaxRows = "1000"
```

The server permits only one read-only `SELECT`, `SHOW`, or `EXPLAIN` statement per query call. Multiple statements and write operations are rejected.

## JWT authentication

The MCP endpoint requires a JWT with the configured issuer, audience, `id=id-1`, and `admin` role. Development configuration is provided in `appsettings.Development.json`.

Use a private signing key of at least 32 bytes outside local development:

```powershell
$env:Jwt__Issuer = "pgsql-mcp-server"
$env:Jwt__Audience = "pgsql-mcp-client"
$env:Jwt__SigningKey = "replace-with-a-private-key-at-least-32-bytes-long"
```

## Build and run

Open a PowerShell terminal in this directory. The `http` launch profile sets `ASPNETCORE_ENVIRONMENT=Development` and listens on port `5000`:

```powershell
dotnet restore
dotnet build
dotnet run --launch-profile http
```

Keep this terminal running. Verify the server from another terminal:

```powershell
Invoke-RestMethod http://localhost:5000/
Invoke-RestMethod http://localhost:5000/health
```

The server endpoints are:

- `GET http://localhost:5000/` - server information
- `GET http://localhost:5000/health` - PostgreSQL connectivity check
- `POST http://localhost:5000/mcp` - authenticated MCP JSON-RPC endpoint
- `POST http://localhost:5000/auth/token` - development-only token endpoint

## Get a development access token

When the server is running with the Development environment, request a token:

```powershell
$token = (Invoke-RestMethod http://localhost:5000/auth/token -Method Post).access_token
$env:Mcp__AccessToken = $token
```

The client can also read a token from `Mcp:AccessToken` in configuration, but environment variables are preferred for local secrets.

## MCP endpoint

MCP JSON-RPC requests are sent to `POST /mcp` with `Content-Type: application/json` and a bearer token.

Initialize a session:

```powershell
$body = @'
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "initialize",
  "params": {
    "protocolVersion": "2025-06-18",
    "capabilities": {},
    "clientInfo": { "name": "local-test-client", "version": "1.0.0" }
  }
}
'@
Invoke-RestMethod -Uri http://localhost:5000/mcp -Method Post -ContentType 'application/json' -Headers @{ Authorization = "Bearer $token" } -Body $body
```

Run a read-only query:

```powershell
Invoke-RestMethod -Uri http://localhost:5000/mcp -Method Post -ContentType 'application/json' -Headers @{ Authorization = "Bearer $token" } -Body (@{
  jsonrpc = '2.0'
  id = 2
  method = 'tools/call'
  params = @{
    name = 'query'
    arguments = @{ sql = 'SELECT current_database() AS database_name' }
  }
} | ConvertTo-Json -Depth 5)
```

The query response includes the exact SQL submitted in `query`, together with `rowCount` and `rows`.

## Database schema resource

The server exposes `database://schema`, which discovers user-visible tables and columns from the configured database:

```powershell
Invoke-RestMethod -Uri http://localhost:5000/mcp -Method Post -ContentType 'application/json' -Headers @{ Authorization = "Bearer $token" } -Body (@{
  jsonrpc = '2.0'
  id = 3
  method = 'resources/read'
  params = @{ uri = 'database://schema' }
} | ConvertTo-Json -Depth 5)
```

## Troubleshooting

- `Jwt:SigningKey must be at least 32 bytes`: run with `dotnet run --launch-profile http`, or set `ASPNETCORE_ENVIRONMENT=Development` before starting.
- `address already in use`: another process is using port `5000`; stop it or change the server URL and the client's `Mcp:Endpoint` together.
- PostgreSQL health is `false`: check the connection string, PostgreSQL service, database name, and firewall settings.

The implementation uses Npgsql because this server targets PostgreSQL. Supporting another database engine requires adding that engine's ADO.NET provider; the MCP contract and schema resource can remain unchanged.
