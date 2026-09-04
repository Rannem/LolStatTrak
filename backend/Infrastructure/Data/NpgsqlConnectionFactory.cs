using Npgsql;

namespace LolStatTrak.Infrastructure.Data;

/// <summary>Creates open Npgsql connections from the configured connection string.</summary>
public class NpgsqlConnectionFactory(string connectionString)
{
    public async Task<NpgsqlConnection> CreateOpenConnectionAsync(CancellationToken ct = default)
    {
        var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        return connection;
    }
}
