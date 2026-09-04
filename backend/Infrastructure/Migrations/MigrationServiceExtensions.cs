using FluentMigrator.Runner;
using Microsoft.Extensions.DependencyInjection;

namespace LolStatTrak.Infrastructure.Migrations;

/// <summary>Wires FluentMigrator against Postgres and runs pending migrations on startup.</summary>
public static class MigrationServiceExtensions
{
    public static IServiceCollection AddDatabaseMigrations(this IServiceCollection services, string connectionString)
    {
        services
            .AddFluentMigratorCore()
            .ConfigureRunner(rb => rb
                .AddPostgres()
                .WithGlobalConnectionString(connectionString)
                .ScanIn(typeof(M202601010001_InitialSchema).Assembly).For.Migrations())
            .AddLogging(lb => lb.AddFluentMigratorConsole());

        return services;
    }

    public static void RunDatabaseMigrations(this IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var runner = scope.ServiceProvider.GetRequiredService<IMigrationRunner>();
        runner.MigrateUp();
    }
}
