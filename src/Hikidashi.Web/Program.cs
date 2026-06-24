using Hikidashi.Data;
using Hikidashi.Web;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddMcpAuthentication(builder.Configuration);
builder.Services.AddMcpServer().WithHttpTransport().WithToolsFromAssembly();

var app = builder.Build();

// Apply EF migrations on startup (cold-start cost is acceptable for personal use).
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<FactsDbContext>();
    await db.Database.MigrateAsync();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapMcp("/mcp").RequireAuthorization(AuthSchemes.Mcp);

app.Run();

// Exposed for integration tests.
public partial class Program;
