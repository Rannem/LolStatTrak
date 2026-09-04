using Npgsql;

namespace LolStatTrak.Infrastructure.Data;

/// <summary>
/// Hands out open connections from a single <see cref="NpgsqlDataSource"/>: one parsed
/// connection string, one pool, and automatic server-side preparation of hot statements.
/// </summary>
public sealed class NpgsqlConnectionFactory : IAsyncDisposable
{
    private readonly NpgsqlDataSource _dataSource;

    public NpgsqlConnectionFactory(string connectionString)
    {
        var csb = new NpgsqlConnectionStringBuilder(connectionString)
        {
            // Dapper re-sends the same SQL text constantly; let Npgsql prepare the top N automatically.
            MaxAutoPrepare = 32,
            AutoPrepareMinUsages = 2,
        };
        _dataSource = new NpgsqlDataSourceBuilder(csb.ConnectionString).Build();
    }

    public async Task<NpgsqlConnection> CreateOpenConnectionAsync(CancellationToken ct = default)
        => await _dataSource.OpenConnectionAsync(ct);

    public ValueTask DisposeAsync() => _dataSource.DisposeAsync();
}
