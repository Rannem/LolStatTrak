using FluentMigrator;

namespace LolStatTrak.Infrastructure.Migrations;

[Migration(202601010001)]
public class M202601010001_InitialSchema : Migration
{
    public override void Up()
    {
        Create.Table("users")
            .WithColumn("id").AsGuid().PrimaryKey()
            .WithColumn("discord_id").AsString(64).NotNullable().Unique()
            .WithColumn("discord_username").AsString(128).NotNullable()
            .WithColumn("avatar_url").AsString(512).Nullable()
            .WithColumn("riot_puuid").AsString(128).Nullable()
            .WithColumn("riot_game_name").AsString(64).Nullable()
            .WithColumn("riot_tag_line").AsString(16).Nullable()
            .WithColumn("created_at").AsDateTimeOffset().NotNullable();

        Create.Table("clubs")
            .WithColumn("id").AsGuid().PrimaryKey()
            .WithColumn("name").AsString(128).NotNullable()
            .WithColumn("slug").AsString(128).NotNullable().Unique()
            .WithColumn("owner_user_id").AsGuid().NotNullable()
            .WithColumn("invite_code").AsString(32).NotNullable().Unique()
            .WithColumn("created_at").AsDateTimeOffset().NotNullable();

        Create.ForeignKey("fk_clubs_owner")
            .FromTable("clubs").ForeignColumn("owner_user_id")
            .ToTable("users").PrimaryColumn("id");

        Create.Table("club_members")
            .WithColumn("club_id").AsGuid().NotNullable()
            .WithColumn("user_id").AsGuid().NotNullable()
            .WithColumn("role").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("status").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("joined_at").AsDateTimeOffset().NotNullable();

        Create.PrimaryKey("pk_club_members").OnTable("club_members").Columns("club_id", "user_id");
        Create.ForeignKey("fk_club_members_club").FromTable("club_members").ForeignColumn("club_id")
            .ToTable("clubs").PrimaryColumn("id").OnDelete(System.Data.Rule.Cascade);
        Create.ForeignKey("fk_club_members_user").FromTable("club_members").ForeignColumn("user_id")
            .ToTable("users").PrimaryColumn("id").OnDelete(System.Data.Rule.Cascade);

        Create.Table("club_banned_champions")
            .WithColumn("club_id").AsGuid().NotNullable()
            .WithColumn("champion_id").AsInt32().NotNullable();

        Create.PrimaryKey("pk_club_banned_champions").OnTable("club_banned_champions")
            .Columns("club_id", "champion_id");
        Create.ForeignKey("fk_club_banned_champions_club").FromTable("club_banned_champions")
            .ForeignColumn("club_id").ToTable("clubs").PrimaryColumn("id").OnDelete(System.Data.Rule.Cascade);

        Create.Table("lobbies")
            .WithColumn("id").AsGuid().PrimaryKey()
            .WithColumn("club_id").AsGuid().NotNullable()
            .WithColumn("created_by_user_id").AsGuid().NotNullable()
            .WithColumn("status").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("created_at").AsDateTimeOffset().NotNullable();

        Create.ForeignKey("fk_lobbies_club").FromTable("lobbies").ForeignColumn("club_id")
            .ToTable("clubs").PrimaryColumn("id").OnDelete(System.Data.Rule.Cascade);
        Create.ForeignKey("fk_lobbies_creator").FromTable("lobbies").ForeignColumn("created_by_user_id")
            .ToTable("users").PrimaryColumn("id");

        Create.Table("lobby_players")
            .WithColumn("lobby_id").AsGuid().NotNullable()
            .WithColumn("user_id").AsGuid().NotNullable()
            .WithColumn("assigned_team").AsInt32().Nullable()
            .WithColumn("assigned_champion_id").AsInt32().Nullable();

        Create.PrimaryKey("pk_lobby_players").OnTable("lobby_players").Columns("lobby_id", "user_id");
        Create.ForeignKey("fk_lobby_players_lobby").FromTable("lobby_players").ForeignColumn("lobby_id")
            .ToTable("lobbies").PrimaryColumn("id").OnDelete(System.Data.Rule.Cascade);
        Create.ForeignKey("fk_lobby_players_user").FromTable("lobby_players").ForeignColumn("user_id")
            .ToTable("users").PrimaryColumn("id").OnDelete(System.Data.Rule.Cascade);

        Create.Table("matches")
            .WithColumn("id").AsGuid().PrimaryKey()
            .WithColumn("club_id").AsGuid().NotNullable()
            .WithColumn("lobby_id").AsGuid().Nullable()
            .WithColumn("riot_match_id").AsString(64).NotNullable().Unique()
            .WithColumn("played_at").AsDateTimeOffset().NotNullable()
            .WithColumn("queue_id").AsInt32().NotNullable()
            .WithColumn("raw_payload").AsCustom("jsonb").NotNullable();

        Create.ForeignKey("fk_matches_club").FromTable("matches").ForeignColumn("club_id")
            .ToTable("clubs").PrimaryColumn("id").OnDelete(System.Data.Rule.Cascade);
        Create.ForeignKey("fk_matches_lobby").FromTable("matches").ForeignColumn("lobby_id")
            .ToTable("lobbies").PrimaryColumn("id").OnDelete(System.Data.Rule.SetNull);

        Create.Table("match_participants")
            .WithColumn("id").AsGuid().PrimaryKey()
            .WithColumn("match_id").AsGuid().NotNullable()
            .WithColumn("user_id").AsGuid().NotNullable()
            .WithColumn("puuid").AsString(128).NotNullable()
            .WithColumn("champion_id").AsInt32().NotNullable()
            .WithColumn("team").AsInt32().NotNullable()
            .WithColumn("kills").AsInt32().NotNullable()
            .WithColumn("deaths").AsInt32().NotNullable()
            .WithColumn("assists").AsInt32().NotNullable()
            .WithColumn("win").AsBoolean().NotNullable()
            .WithColumn("raw_stats").AsCustom("jsonb").NotNullable();

        Create.ForeignKey("fk_match_participants_match").FromTable("match_participants")
            .ForeignColumn("match_id").ToTable("matches").PrimaryColumn("id").OnDelete(System.Data.Rule.Cascade);
        Create.ForeignKey("fk_match_participants_user").FromTable("match_participants")
            .ForeignColumn("user_id").ToTable("users").PrimaryColumn("id").OnDelete(System.Data.Rule.Cascade);
    }

    public override void Down()
    {
        Delete.Table("match_participants");
        Delete.Table("matches");
        Delete.Table("lobby_players");
        Delete.Table("lobbies");
        Delete.Table("club_banned_champions");
        Delete.Table("club_members");
        Delete.Table("clubs");
        Delete.Table("users");
    }
}
