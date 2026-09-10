# PostgreSQL MCP Server

A small ASP.NET Core 9 HTTP server that exposes a local PostgreSQL database through the Model Context Protocol (MCP) JSON-RPC endpoint.

## Requirements

- .NET SDK 9.0 or newer
- PostgreSQL

## Configure PostgreSQL

Edit `appsettings.json`, or use environment variables so credentials are not stored in source control:

```powershell
$env:Postgres__ConnectionString = "Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=your-password"
$env:Postgres__CommandTimeoutSeconds = "30"
$env:Postgres__MaxRows = "1000"
```

The server only allows one read-only `SELECT`, `SHOW`, or `EXPLAIN` statement per tool call. Multiple statements and writes are rejected.

## Build and run

From this directory:

```powershell
dotnet restore
dotnet build
dotnet run --launch-profile http
```

The server listens at `http://localhost:5000`. The health endpoint is `GET /health`.

## MCP endpoint

MCP JSON-RPC requests are sent to `POST /mcp` with `Content-Type: application/json`.

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
Invoke-RestMethod -Uri http://localhost:5000/mcp -Method Post -ContentType 'application/json' -Body $body
```

List the SQL tool:

```powershell
Invoke-RestMethod -Uri http://localhost:5000/mcp -Method Post -ContentType 'application/json' -Body (@{
  jsonrpc = '2.0'
  id = 2
  method = 'tools/list'
} | ConvertTo-Json -Depth 5)
```

Run a read-only query:

```powershell
Invoke-RestMethod -Uri http://localhost:5000/mcp -Method Post -ContentType 'application/json' -Body (@{
  jsonrpc = '2.0'
  id = 3
  method = 'tools/call'
  params = @{
    name = 'query'
    arguments = @{ sql = 'SELECT current_database() AS database_name' }
  }
} | ConvertTo-Json -Depth 5)
```

The server also exposes the database-neutral resource `database://schema`, which discovers user-visible tables and columns from the currently configured database. Changing `Postgres:ConnectionString` to another PostgreSQL database does not require code changes.

Read the generic schema resource with this MCP request:

```json
{
  "jsonrpc": "2.0",
  "id": 4,
  "method": "resources/read",
  "params": {
    "uri": "database://schema"
  }
}
```

The implementation uses Npgsql because this server targets PostgreSQL. Supporting another database engine requires adding that engine's ADO.NET provider; the MCP contract and schema resource can remain unchanged.
