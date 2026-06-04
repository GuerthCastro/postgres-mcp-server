using System.ComponentModel;
using Mcp.PostgreSQL.Services;
using ModelContextProtocol.Server;

namespace Mcp.PostgreSQL.Tools;

[McpServerToolType]
public class ConnectTool(DatabaseService databaseService)
{
    [McpServerTool, Description("Connects to a PostgreSQL server. Port is optional, defaults to 5432.")]
    public async Task<string> Connect(
        [Description("PostgreSQL server host or IP address")] string host,
        [Description("PostgreSQL server port, defaults to 5432")] int? port,
        [Description("Username")] string username,
        [Description("Password")] string password,
        [Description("Default database, optional")] string? database)
    {
        databaseService.Configure(host, port, username, password, database);

        try
        {
            await using var connection = await databaseService.OpenConnectionAsync();
            var serverVersion = connection.ServerVersion;
            return $"Connected to PostgreSQL {serverVersion} at {host}:{databaseService.GetConfiguration().ResolvedPort}";
        }
        catch (Exception ex)
        {
            databaseService.Configure(string.Empty, null, string.Empty, string.Empty, null);
            return $"Connection failed: {ex.Message}";
        }
    }
}
