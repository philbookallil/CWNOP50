using FluentMigrator;
using Nop.Data.Extensions;
using Nop.Data.Mapping;
using Nop.Data.Migrations;
using Nop.Plugin.Integration.Odoo.Domain;

namespace Nop.Plugin.Integration.Odoo.Data.Migrations;

/// <summary>
/// Adds <c>Odoo_OrderMapping</c>, the record of the Odoo sale orders made from nop orders. Runs on install and on
/// update (<see cref="MigrationProcessType.NoMatter"/>) and creates nothing that already exists.
/// </summary>
[NopMigration(MigrationVersions.AddOrderMapping, "Integration.Odoo order mapping", MigrationProcessType.NoMatter)]
public class OrderMappingMigration : Migration
{
    /// <summary>
    /// Apply schema changes.
    /// </summary>
    public override void Up()
    {
        var table = NameCompatibilityManager.GetTableName(typeof(OdooOrderMapping));
        if (Schema.Table(table).Exists())
            return;

        this.CreateTableIfNotExists<OdooOrderMapping>();

        // One row per (order, destination): a send updates it, never adds a second.
        Create.Index($"IX_{table}_{nameof(OdooOrderMapping.NopOrderId)}_{nameof(OdooOrderMapping.DestinationKey)}")
            .OnTable(table)
            .OnColumn(nameof(OdooOrderMapping.NopOrderId)).Ascending()
            .OnColumn(nameof(OdooOrderMapping.DestinationKey)).Ascending()
            .WithOptions().Unique();
    }

    /// <summary>
    /// Revert schema changes.
    /// </summary>
    public override void Down()
    {
        var table = NameCompatibilityManager.GetTableName(typeof(OdooOrderMapping));
        if (Schema.Table(table).Exists())
            Delete.Table(table);
    }
}
