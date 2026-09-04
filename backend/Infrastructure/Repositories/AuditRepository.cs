using System.Text.Json;
using Dapper;
using LolStatTrak.Infrastructure.Data;

namespace LolStatTrak.Infrastructure.Repositories;

public class AuditEntry
{
    public Guid Id { get; set; }
    public Guid? ClubId { get; set; }
    public string? ClubName { get; set; }
    public Guid? ActorUserId { get; set; }
    public string? ActorUsername { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? TargetType { get; set; }
    public string? TargetId { get; set; }
    public string? Details { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>Append-only record of moderation / admin actions, viewable per club or globally.</summary>
public class AuditRepository(NpgsqlConnectionFactory connectionFactory)
{
    private const string Select = """
        select a.id "Id", a.club_id "ClubId", c.name "ClubName", a.actor_user_id "ActorUserId",
               u.discord_username "ActorUsername", a.action "Action", a.target_type "TargetType",
               a.target_id "TargetId", a.details::text "Details", a.created_at "CreatedAt"
        from audit_log a
        left join clubs c on c.id = a.club_id
        left join users u on u.id = a.actor_user_id
        """;

    public async Task LogAsync(Guid? clubId, Guid? actorUserId, string action, string? targetType = null,
        string? targetId = null, object? details = null)
    {
        await using var conn = await connectionFactory.CreateOpenConnectionAsync();
        await conn.ExecuteAsync(
            """
            insert into audit_log (id, club_id, actor_user_id, action, target_type, target_id, details, created_at)
            values (gen_random_uuid(), @clubId, @actorUserId, @action, @targetType, @targetId, @details::jsonb, now())
            """,
            new
            {
                clubId, actorUserId, action, targetType, targetId,
                details = details is null ? null : JsonSerializer.Serialize(details),
            });
    }

    public async Task<IEnumerable<AuditEntry>> ListForClubAsync(Guid clubId, int limit = 100)
    {
        await using var conn = await connectionFactory.CreateOpenConnectionAsync();
        return await conn.QueryAsync<AuditEntry>(
            $"{Select} where a.club_id = @clubId order by a.created_at desc limit @limit",
            new { clubId, limit });
    }

    public async Task<IEnumerable<AuditEntry>> ListAllAsync(int limit = 200)
    {
        await using var conn = await connectionFactory.CreateOpenConnectionAsync();
        return await conn.QueryAsync<AuditEntry>(
            $"{Select} order by a.created_at desc limit @limit", new { limit });
    }
}
