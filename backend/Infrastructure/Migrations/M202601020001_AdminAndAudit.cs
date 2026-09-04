using FluentMigrator;

namespace LolStatTrak.Infrastructure.Migrations;

[Migration(202601020001)]
public class M202601020001_AdminAndAudit : Migration
{
    public override void Up()
    {
        Alter.Table("users")
            .AddColumn("is_global_admin").AsBoolean().NotNullable().WithDefaultValue(false)
            // 0 = Pending (awaiting global-admin approval), 1 = Approved, 2 = Rejected.
            .AddColumn("access_status").AsInt32().NotNullable().WithDefaultValue(0);

        // Anyone who already signed up before this migration keeps working.
        Execute.Sql("update users set access_status = 1");

        Create.Table("audit_log")
            .WithColumn("id").AsGuid().PrimaryKey()
            .WithColumn("club_id").AsGuid().Nullable()
            .WithColumn("actor_user_id").AsGuid().Nullable()
            .WithColumn("action").AsString(64).NotNullable()
            .WithColumn("target_type").AsString(32).Nullable()
            .WithColumn("target_id").AsString(128).Nullable()
            .WithColumn("details").AsCustom("jsonb").Nullable()
            .WithColumn("created_at").AsDateTimeOffset().NotNullable();

        Create.Index("ix_audit_log_club_created").OnTable("audit_log")
            .OnColumn("club_id").Ascending().OnColumn("created_at").Descending();

        // Club deletion cascades audit rows; deleting a user leaves the row with a null actor.
        Create.ForeignKey("fk_audit_log_club").FromTable("audit_log").ForeignColumn("club_id")
            .ToTable("clubs").PrimaryColumn("id").OnDelete(System.Data.Rule.Cascade);
        Create.ForeignKey("fk_audit_log_actor").FromTable("audit_log").ForeignColumn("actor_user_id")
            .ToTable("users").PrimaryColumn("id").OnDelete(System.Data.Rule.SetNull);
    }

    public override void Down()
    {
        Delete.Table("audit_log");
        Delete.Column("access_status").FromTable("users");
        Delete.Column("is_global_admin").FromTable("users");
    }
}
