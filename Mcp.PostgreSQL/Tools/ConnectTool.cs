using System.ComponentModel;
using Mcp.PostgreSQL.Configuration;
using Mcp.PostgreSQL.Services;
using ModelContextProtocol.Server;

namespace Mcp.PostgreSQL.Tools;

[McpServerToolType]
public class ConnectTool(DatabaseService databaseService)
{
    [McpServerTool, Description("Connects to a PostgreSQL server and returns a connectionId. Pass that connectionId to every other tool. Port is optional, defaults to 5432.")]
    public async Task<string> Connect(
        [Description("PostgreSQL server host or IP address")] string host,
        [Description("PostgreSQL server port, defaults to 5432")] int? port,
        [Description("Username")] string username,
        [Description("Password")] string password,
        [Description("Default database, optional")] string? database)
    {
        var configuration = new ServerConfiguration
        {
            Host = host,
            Port = port,
            Username = username,
            Password = password,
            Database = database
        };

        try
        {
            await using var connection = await databaseService.OpenConnectionAsync(configuration);
            var serverVersion = connection.ServerVersion;

            await using var command = connection.CreateCommand();
            command.CommandText = "SHOW standard_conforming_strings";
            var standardConformingStrings = (string?)await command.ExecuteScalarAsync();

            if (!string.Equals(standardConformingStrings, "on", StringComparison.OrdinalIgnoreCase))
                return $"Connection rejected: standard_conforming_strings is '{standardConformingStrings}' on {host}:{configuration.ResolvedPort}. " +
                       "This server requires it to be 'on' so backslashes in string literals cannot hide additional statements from the query guard.";

            var connectionId = databaseService.Register(configuration);

            return $"Connected to PostgreSQL {serverVersion} at {host}:{configuration.ResolvedPort}.\n" +
                   $"connectionId: {connectionId}\n" +
                   "Use this connectionId in every other tool call.";
        }
        catch (Exception ex)
        {
            return $"Connection failed: {ex.Message}";
        }
    }

    [McpServerTool, Description("Closes a connection created with connect and forgets its credentials.")]
    public string Disconnect(
        [Description("connectionId returned by connect")] string? connectionId = null)
    {
        return databaseService.Remove(connectionId)
            ? "Disconnected."
            : "Unknown or expired connectionId.";
    }
}
