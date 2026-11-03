using FluentMigrator;
using Nop.Data.Extensions;
using Nop.Data.Mapping;
using Nop.Data.Migrations;
using Nop.Plugin.Integration.Odoo.Domain;

namespace Nop.Plugin.Integration.Odoo.Data.Migrations;

/// <summary>
/// Initial schema migration for the Integration.Odoo plugin.
/// </summary>
/// <remarks>
/// Creates the sync log (events and their traces) and the Odoo↔nop store mapping. The mapping is
/// one-to-one: one row per Odoo store and one Odoo store per nop store, enforced by two unique indexes.
/// Reversal is handled by <see cref="AutoReversingMigration"/>; on uninstall the indexes and tables
/// are dropped in reverse order.
/// </remarks>
[NopMigration(MigrationVersions.InitialSchema, "Integration.Odoo base schema", MigrationProcessType.Installation)]
public class SchemaMigration : AutoReversingMigration
{
    /// <summary>
    /// Apply schema changes.
    /// </summary>
    public override void Up()
    {
        var logTable = NameCompatibilityManager.GetTableName(typeof(OdooLog));
        var storeMappingTable = NameCompatibilityManager.GetTableName(typeof(OdooStoreMapping));

        this.CreateTableIfNotExists<OdooLogEvent>();

        this.CreateTableIfNotExists<OdooLog>();

        this.CreateTableIfNotExists<OdooStoreMapping>();

        // The trace grid pages one event's traces at a time.
        Create.Index($"IX_{logTable}_{nameof(OdooLog.LogEventId)}")
            .OnTable(logTable)
            .OnColumn(nameof(OdooLog.LogEventId)).Ascending();

        Create.Index($"IX_{storeMappingTable}_{nameof(OdooStoreMapping.OdooStoreId)}")
            .OnTable(storeMappingTable)
            .OnColumn(nameof(OdooStoreMapping.OdooStoreId)).Ascending()
            .WithOptions().Unique();

        Create.Index($"IX_{storeMappingTable}_{nameof(OdooStoreMapping.NopStoreId)}")
            .OnTable(storeMappingTable)
            .OnColumn(nameof(OdooStoreMapping.NopStoreId)).Ascending()
            .WithOptions().Unique();
    }
}
