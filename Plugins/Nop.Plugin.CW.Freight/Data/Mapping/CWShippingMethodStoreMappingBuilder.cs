using FluentMigrator.Builders.Create.Table;
using Nop.Data.Mapping.Builders;
using Nop.Plugin.CW.Freight.Domain;

namespace Nop.Plugin.CW.Freight.Data.Mapping;

public class CWShippingMethodStoreMappingBuilder : NopEntityBuilder<CWShippingMethodStoreMapping>
{
    public override void MapEntity(CreateTableExpressionBuilder table)
    {
        table
            .WithColumn(nameof(CWShippingMethodStoreMapping.ShippingMethodId)).AsInt32().NotNullable()
            .WithColumn(nameof(CWShippingMethodStoreMapping.StoreId)).AsInt32().NotNullable();
    }
}
