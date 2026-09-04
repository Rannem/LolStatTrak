using FluentMigrator;

namespace LolStatTrak.Infrastructure.Migrations;

[Migration(202601030001)]
public class M202601030001_LobbyGameMode : Migration
{
    public override void Up()
    {
        Alter.Table("lobbies")
            // 0 = Aram, 1 = AramMayhem, 2 = SummonersRift (see LobbyGameMode).
            .AddColumn("game_mode").AsInt32().NotNullable().WithDefaultValue(0)
            // Whether the app rolls champions too, or only teams (game picks champs itself, e.g. ARAM Mayhem).
            .AddColumn("assign_champions").AsBoolean().NotNullable().WithDefaultValue(true);

        Alter.Table("matches")
            // Riot's info.gameMode (e.g. ARAM, CLASSIC) so the UI can label what was actually played.
            .AddColumn("riot_game_mode").AsString(32).Nullable()
            .AddColumn("game_duration_seconds").AsInt32().Nullable();
    }

    public override void Down()
    {
        Delete.Column("game_mode").FromTable("lobbies");
        Delete.Column("assign_champions").FromTable("lobbies");
        Delete.Column("riot_game_mode").FromTable("matches");
        Delete.Column("game_duration_seconds").FromTable("matches");
    }
}
