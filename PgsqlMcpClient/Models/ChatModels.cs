namespace PgsqlMcpClient.Models;

public sealed record ChatRequest(string Message);

public sealed record ChatResponse(string Message);