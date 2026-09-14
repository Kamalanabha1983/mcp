using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using PgsqlMcpServer.Database;
using PgsqlMcpServer.Mcp;
using PgsqlMcpServer.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<PostgresOptions>(
	builder.Configuration.GetSection(PostgresOptions.SectionName));
builder.Services.Configure<JwtOptions>(
	builder.Configuration.GetSection(JwtOptions.SectionName));
var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
	?? throw new InvalidOperationException("JWT configuration is missing.");

if (Encoding.UTF8.GetByteCount(jwtOptions.SigningKey) < 32)
{
	throw new InvalidOperationException("Jwt:SigningKey must be at least 32 bytes.");
}

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
	.AddJwtBearer(options =>
	{
		options.TokenValidationParameters = new TokenValidationParameters
		{
			ValidateIssuer = true,
			ValidIssuer = jwtOptions.Issuer,
			ValidateAudience = true,
			ValidAudience = jwtOptions.Audience,
			ValidateIssuerSigningKey = true,
			IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
			ValidateLifetime = true,
			ClockSkew = TimeSpan.FromSeconds(30),
			RoleClaimType = ClaimTypes.Role,
			NameClaimType = ClaimTypes.NameIdentifier
		};
	});
builder.Services.AddAuthorizationBuilder()
	.AddPolicy("McpReader", policy => policy
		.RequireAuthenticatedUser()
		.RequireClaim("id", "id-1")
		.RequireRole("admin"));
builder.Services.AddSingleton<PostgresService>();
builder.Services.AddSingleton<McpServer>();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

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
}).RequireAuthorization("McpReader");

if (app.Environment.IsDevelopment())
{
	app.MapPost("/auth/token", () =>
	{
		var claims = new[]
		{
			new Claim("id", "id-1"),
			new Claim(ClaimTypes.Role, "admin")
		};
		var credentials = new SigningCredentials(
			new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
			SecurityAlgorithms.HmacSha256);
		var token = new JwtSecurityToken(
			issuer: jwtOptions.Issuer,
			audience: jwtOptions.Audience,
			claims: claims,
			expires: DateTime.UtcNow.AddHours(1),
			signingCredentials: credentials);

		return Results.Ok(new { access_token = new JwtSecurityTokenHandler().WriteToken(token) });
	});
}

app.Run();
