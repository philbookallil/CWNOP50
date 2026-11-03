using FluentMigrator.Builders.Create.Table;
using Nop.Data.Mapping.Builders;
using Nop.Plugin.Integration.Odoo.Domain;

namespace Nop.Plugin.Integration.Odoo.Data.Mapping;

public class OdooStoreMappingBuilder : NopEntityBuilder<OdooStoreMapping>
{
    public override void MapEntity(CreateTableExpressionBuilder table)
    {
        table
            .WithColumn(nameof(OdooStoreMapping.OdooStoreId)).AsInt32().NotNullable()
            .WithColumn(nameof(OdooStoreMapping.NopStoreId)).AsInt32().NotNullable()
            .WithColumn(nameof(OdooStoreMapping.LastSyncedRevision)).AsInt32().NotNullable()
            .WithColumn(nameof(OdooStoreMapping.LastSyncedWriteDate)).AsString(32).Nullable();
    }
}
