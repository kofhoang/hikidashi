using Hikidashi.Data;
using Hikidashi.Web;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddMcpAuthentication(builder.Configuration);
builder.Services.AddMcpServer().WithHttpTransport().WithToolsFromAssembly();

var app = builder.Build();

// Apply the facts schema on startup (cold-start cost is acceptable for personal use).
using (var scope = app.Services.CreateScope())
{
    var dataSource = scope.ServiceProvider.GetRequiredService<NpgsqlDataSource>();
    await Migrator.ApplyAsync(dataSource);
}

app.UseAuthentication();
app.UseAuthorization();

app.MapMcp("/mcp").RequireAuthorization(AuthSchemes.Mcp);

app.Run();

// Exposed for integration tests.
public partial class Program;
