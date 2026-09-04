using Dapper;
using LolStatTrak.Domain.Entities;
using LolStatTrak.Infrastructure.Data;

namespace LolStatTrak.Infrastructure.Repositories;

/// <summary>Club member enriched with Discord display info for the UI.</summary>
public class ClubMemberView
{
    public Guid ClubId { get; set; }
    public Guid UserId { get; set; }
    public string DiscordUsername { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string? RiotGameName { get; set; }
    public string? RiotTagLine { get; set; }
    public ClubMemberRole Role { get; set; }
    public ClubMembershipStatus Status { get; set; }
    public DateTimeOffset JoinedAt { get; set; }
}

public class ClubOverview : Club
{
    public string OwnerUsername { get; set; } = string.Empty;
    public int MemberCount { get; set; }
    public int MatchCount { get; set; }
}

public class ClubRepository(NpgsqlConnectionFactory connectionFactory)
{
    public async Task<Club> CreateAsync(string name, string slug, Guid ownerUserId, string inviteCode)
    {
        await using var conn = await connectionFactory.CreateOpenConnectionAsync();
        var club = await conn.QuerySingleAsync<Club>(
            """
            insert into clubs (id, name, slug, owner_user_id, invite_code, created_at)
            values (gen_random_uuid(), @name, @slug, @ownerUserId, @inviteCode, now())
            returning id "Id", name "Name", slug "Slug", owner_user_id "OwnerUserId",
                      invite_code "InviteCode", created_at "CreatedAt"
            """,
            new { name, slug, ownerUserId, inviteCode });

        // Creator is automatically the approved Owner member.
        await conn.ExecuteAsync(
            """
            insert into club_members (club_id, user_id, role, status, joined_at)
            values (@clubId, @userId, @role, @status, now())
            """,
            new { clubId = club.Id, userId = ownerUserId, role = (int)ClubMemberRole.Owner, status = (int)ClubMembershipStatus.Approved });

        return club;
    }

    public async Task<Club?> GetByInviteCodeAsync(string inviteCode)
    {
        await using var conn = await connectionFactory.CreateOpenConnectionAsync();
        return await conn.QuerySingleOrDefaultAsync<Club>(
            """
            select id "Id", name "Name", slug "Slug", owner_user_id "OwnerUserId",
                   invite_code "InviteCode", created_at "CreatedAt"
            from clubs where invite_code = @inviteCode
            """,
            new { inviteCode });
    }

    public async Task<IEnumerable<Club>> GetForUserAsync(Guid userId)
    {
        await using var conn = await connectionFactory.CreateOpenConnectionAsync();
        return await conn.QueryAsync<Club>(
            """
            select c.id "Id", c.name "Name", c.slug "Slug", c.owner_user_id "OwnerUserId",
                   c.invite_code "InviteCode", c.created_at "CreatedAt"
            from clubs c
            join club_members m on m.club_id = c.id
            where m.user_id = @userId and m.status = @approved
            """,
            new { userId, approved = (int)ClubMembershipStatus.Approved });
    }

    /// <summary>Auto-join via invite code: inserts an already-Approved member row.</summary>
    public async Task JoinViaInviteAsync(Guid clubId, Guid userId)
    {
        await using var conn = await connectionFactory.CreateOpenConnectionAsync();
        await conn.ExecuteAsync(
            """
            insert into club_members (club_id, user_id, role, status, joined_at)
            values (@clubId, @userId, @role, @status, now())
            on conflict (club_id, user_id) do nothing
            """,
            new { clubId, userId, role = (int)ClubMemberRole.Member, status = (int)ClubMembershipStatus.Approved });
    }

    /// <summary>Request-to-join flow: inserts a Pending member row awaiting admin/mod approval.</summary>
    public async Task RequestJoinAsync(Guid clubId, Guid userId)
    {
        await using var conn = await connectionFactory.CreateOpenConnectionAsync();
        await conn.ExecuteAsync(
            """
            insert into club_members (club_id, user_id, role, status, joined_at)
            values (@clubId, @userId, @role, @status, now())
            on conflict (club_id, user_id) do nothing
            """,
            new { clubId, userId, role = (int)ClubMemberRole.Member, status = (int)ClubMembershipStatus.Pending });
    }

    public async Task<IEnumerable<ClubMemberView>> GetPendingRequestsAsync(Guid clubId)
    {
        await using var conn = await connectionFactory.CreateOpenConnectionAsync();
        return await conn.QueryAsync<ClubMemberView>(
            """
            select m.club_id "ClubId", m.user_id "UserId", u.discord_username "DiscordUsername",
                   u.avatar_url "AvatarUrl", u.riot_game_name "RiotGameName", u.riot_tag_line "RiotTagLine", m.role "Role", m.status "Status", m.joined_at "JoinedAt"
            from club_members m
            join users u on u.id = m.user_id
            where m.club_id = @clubId and m.status = @pending
            order by m.joined_at
            """,
            new { clubId, pending = (int)ClubMembershipStatus.Pending });
    }

    public async Task<IEnumerable<ClubMemberView>> GetMembersAsync(Guid clubId)
    {
        await using var conn = await connectionFactory.CreateOpenConnectionAsync();
        return await conn.QueryAsync<ClubMemberView>(
            """
            select m.club_id "ClubId", m.user_id "UserId", u.discord_username "DiscordUsername",
                   u.avatar_url "AvatarUrl", u.riot_game_name "RiotGameName", u.riot_tag_line "RiotTagLine", m.role "Role", m.status "Status", m.joined_at "JoinedAt"
            from club_members m
            join users u on u.id = m.user_id
            where m.club_id = @clubId and m.status = @approved
            order by m.role desc, u.discord_username
            """,
            new { clubId, approved = (int)ClubMembershipStatus.Approved });
    }

    public async Task<Club?> GetAsync(Guid clubId)
    {
        await using var conn = await connectionFactory.CreateOpenConnectionAsync();
        return await conn.QuerySingleOrDefaultAsync<Club>(
            """
            select id "Id", name "Name", slug "Slug", owner_user_id "OwnerUserId",
                   invite_code "InviteCode", created_at "CreatedAt"
            from clubs where id = @clubId
            """,
            new { clubId });
    }

    public async Task<IEnumerable<ClubOverview>> GetAllAsync()
    {
        await using var conn = await connectionFactory.CreateOpenConnectionAsync();
        return await conn.QueryAsync<ClubOverview>(
            """
            select c.id "Id", c.name "Name", c.slug "Slug", c.owner_user_id "OwnerUserId",
                   u.discord_username "OwnerUsername", c.invite_code "InviteCode", c.created_at "CreatedAt",
                   (select count(*) from club_members m where m.club_id = c.id and m.status = @approved) "MemberCount",
                   (select count(*) from matches x where x.club_id = c.id) "MatchCount"
            from clubs c
            join users u on u.id = c.owner_user_id
            order by c.created_at desc
            """,
            new { approved = (int)ClubMembershipStatus.Approved });
    }

    public async Task<bool> DeleteAsync(Guid clubId)
    {
        await using var conn = await connectionFactory.CreateOpenConnectionAsync();
        return await conn.ExecuteAsync("delete from clubs where id = @clubId", new { clubId }) > 0;
    }

    public async Task<bool> SetMemberRoleAsync(Guid clubId, Guid userId, ClubMemberRole role)
    {
        await using var conn = await connectionFactory.CreateOpenConnectionAsync();
        return await conn.ExecuteAsync(
            "update club_members set role = @role where club_id = @clubId and user_id = @userId and role <> @owner",
            new { clubId, userId, role = (int)role, owner = (int)ClubMemberRole.Owner }) > 0;
    }

    public async Task<bool> RemoveMemberAsync(Guid clubId, Guid userId)
    {
        await using var conn = await connectionFactory.CreateOpenConnectionAsync();
        return await conn.ExecuteAsync(
            "delete from club_members where club_id = @clubId and user_id = @userId and role <> @owner",
            new { clubId, userId, owner = (int)ClubMemberRole.Owner }) > 0;
    }

    public async Task ApproveMemberAsync(Guid clubId, Guid userId)
    {
        await using var conn = await connectionFactory.CreateOpenConnectionAsync();
        await conn.ExecuteAsync(
            "update club_members set status = @approved where club_id = @clubId and user_id = @userId",
            new { clubId, userId, approved = (int)ClubMembershipStatus.Approved });
    }

    public async Task<ClubMember?> GetMembershipAsync(Guid clubId, Guid userId)
    {
        await using var conn = await connectionFactory.CreateOpenConnectionAsync();
        return await conn.QuerySingleOrDefaultAsync<ClubMember>(
            """
            select club_id "ClubId", user_id "UserId", role "Role", status "Status", joined_at "JoinedAt"
            from club_members where club_id = @clubId and user_id = @userId
            """,
            new { clubId, userId });
    }

    public async Task SetBannedChampionsAsync(Guid clubId, IEnumerable<int> championIds)
    {
        await using var conn = await connectionFactory.CreateOpenConnectionAsync();
        await using var tx = await conn.BeginTransactionAsync();
        await conn.ExecuteAsync("delete from club_banned_champions where club_id = @clubId", new { clubId }, tx);
        foreach (var championId in championIds)
        {
            await conn.ExecuteAsync(
                "insert into club_banned_champions (club_id, champion_id) values (@clubId, @championId)",
                new { clubId, championId }, tx);
        }
        await tx.CommitAsync();
    }

    public async Task<IReadOnlyCollection<int>> GetBannedChampionsAsync(Guid clubId)
    {
        await using var conn = await connectionFactory.CreateOpenConnectionAsync();
        var ids = await conn.QueryAsync<int>(
            "select champion_id from club_banned_champions where club_id = @clubId", new { clubId });
        return ids.ToList();
    }
}
