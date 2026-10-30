using FluentMigrator;
using Nop.Data.Extensions;
using Nop.Data.Migrations;
using Nop.Plugin.CW.Freight.Domain;

namespace Nop.Plugin.CW.Freight.Data.Migrations;

[NopMigration(MigrationVersions.InitialSchema, "CW.Freight base schema", MigrationProcessType.Installation)]
public class SchemaMigration : AutoReversingMigration
{
    public override void Up()
    {
        this.CreateTableIfNotExists<CWShippingMethod>();
        this.CreateTableIfNotExists<CWShippingMethodStoreMapping>();
        this.CreateTableIfNotExists<CWShippingMethodCustomerRoleMapping>();
        this.CreateTableIfNotExists<CWShippingMethodCountryMapping>();
    }
}
