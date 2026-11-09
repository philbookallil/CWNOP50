using FluentMigrator;
using Nop.Data.Extensions;
using Nop.Data.Mapping;
using Nop.Data.Migrations;
using Nop.Plugin.CW.Core.Domain.Subsidies;

namespace Nop.Plugin.CW.Core.Data.Migrations;

/// <summary>
/// Adds the subsidy expiry reminder table to a database installed before the table was part of the schema
/// </summary>
/// <remarks>
/// An installation creates the table from <see cref="SchemaMigration"/> and nop marks this migration as applied without
/// running it; a plugin update runs it, and it creates the table only when it is missing.
/// </remarks>
[NopMigration(MigrationVersions.SubsidyReminder, "CW.Core CORE_SUB_SubsidyReminder", MigrationProcessType.Update)]
public class SubsidyReminderMigration : Migration
{
    public override void Up()
    {
        var table = NameCompatibilityManager.GetTableName(typeof(SubsidyReminder));

        if (Schema.Table(table).Exists())
            return;

        this.CreateTableIfNotExists<SubsidyReminder>();

        Create.Index(SchemaMigration.SubsidyReminderUniqueIndex)
            .OnTable(table)
            .OnColumn(nameof(SubsidyReminder.SubsidyPackNameId)).Ascending()
            .OnColumn(nameof(SubsidyReminder.PeriodEnd)).Ascending()
            .WithOptions().Unique();
    }

    public override void Down()
    {
        var table = NameCompatibilityManager.GetTableName(typeof(SubsidyReminder));

        if (Schema.Table(table).Exists())
            Delete.Table(table);
    }
}