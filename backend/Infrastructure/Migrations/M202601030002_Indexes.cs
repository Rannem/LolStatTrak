using FluentMigrator;

namespace LolStatTrak.Infrastructure.Migrations;

/// <summary>
/// Covering indexes for the hot read paths. The composite primary keys only help when filtering
/// by their leading column (club_id / lobby_id), so the per-user and per-match lookups need their own.
/// </summary>
[Migration(202601030002)]
public class M202601030002_Indexes : Migration
{
    public override void Up()
    {
        // "My clubs" — club_members PK is (club_id, user_id), so lookups by user need this.
        Create.Index("ix_club_members_user").OnTable("club_members").OnColumn("user_id");

        // Club page lobby list: where club_id = ? order by created_at desc.
        Create.Index("ix_lobbies_club_created").OnTable("lobbies")
            .OnColumn("club_id").Ascending()
            .OnColumn("created_at").Descending();

        // Club matches tab: where club_id = ? order by played_at desc.
        Create.Index("ix_matches_club_played").OnTable("matches")
            .OnColumn("club_id").Ascending()
            .OnColumn("played_at").Descending();

        // "Does this lobby already have a match?" during stat sync.
        Create.Index("ix_matches_lobby").OnTable("matches").OnColumn("lobby_id");

        // Participant fan-out per match, and per-user stat aggregation.
        Create.Index("ix_match_participants_match").OnTable("match_participants").OnColumn("match_id");
        Create.Index("ix_match_participants_user").OnTable("match_participants").OnColumn("user_id");

        // Lobby player -> user join when rolling / rendering.
        Create.Index("ix_lobby_players_user").OnTable("lobby_players").OnColumn("user_id");

        // Admin pending-approvals list; partial so it stays tiny.
        Execute.Sql("create index if not exists ix_users_pending on users (created_at) where access_status = 0");

        // Match correlation resolves users by PUUID.
        Execute.Sql("create index if not exists ix_users_riot_puuid on users (riot_puuid) where riot_puuid is not null");
    }

    public override void Down()
    {
        Delete.Index("ix_club_members_user").OnTable("club_members");
        Delete.Index("ix_lobbies_club_created").OnTable("lobbies");
        Delete.Index("ix_matches_club_played").OnTable("matches");
        Delete.Index("ix_matches_lobby").OnTable("matches");
        Delete.Index("ix_match_participants_match").OnTable("match_participants");
        Delete.Index("ix_match_participants_user").OnTable("match_participants");
        Delete.Index("ix_lobby_players_user").OnTable("lobby_players");
        Execute.Sql("drop index if exists ix_users_pending");
        Execute.Sql("drop index if exists ix_users_riot_puuid");
    }
}
