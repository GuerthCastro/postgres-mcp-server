using Mcp.PostgreSQL.Services;
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<DatabaseService>();
builder.Services.AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly();
var app = builder.Build();
app.MapMcp();
app.Run();