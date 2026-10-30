using FluentMigrator;
using Nop.Data.Extensions;
using Nop.Data.Migrations;
using Nop.Plugin.Payments.NABGateway.Domain;

namespace Nop.Plugin.Payments.NABGateway.Data.Migrations;

/// <summary>
/// Creates the table backing the NAB XML payment / failed-transaction reports.
/// </summary>
[NopMigration(MigrationVersions.InitialSchema, "Payments.NABGateway: table for NAB XML transactions", MigrationProcessType.Installation)]
public class NABXMLTransactionInitialVersion : AutoReversingMigration
{
    public override void Up()
    {
        this.CreateTableIfNotExists<NABXMLTransaction>();
    }
}
