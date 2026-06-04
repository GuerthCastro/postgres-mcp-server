using Mcp.PostgreSQL.Configuration;
using Npgsql;

namespace Mcp.PostgreSQL.Services;

public class DatabaseService
{
    private ServerConfiguration _configuration = new();

    public bool IsConnected => _configuration.IsConnected;

    public void Configure(string host, int? port, string username, string password, string? database)
    {
        _configuration = new ServerConfiguration
        {
            Host = host,
            Port = port,
            Username = username,
            Password = password,
            Database = database
        };
    }

    public string GetConnectionString(string? database = null)
    {
        var targetDatabase = database ?? _configuration.Database ?? "postgres";

        return new NpgsqlConnectionStringBuilder
        {
            Host = _configuration.Host,
            Port = _configuration.ResolvedPort,
            Username = _configuration.Username,
            Password = _configuration.Password,
            Database = targetDatabase
        }.ToString();
    }

    public async Task<NpgsqlConnection> OpenConnectionAsync(string? database = null)
    {
        var connection = new NpgsqlConnection(GetConnectionString(database));
        await connection.OpenAsync();
        return connection;
    }

    public ServerConfiguration GetConfiguration() => _configuration;
}
