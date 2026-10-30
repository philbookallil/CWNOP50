using FluentMigrator.Builders.Create.Table;
using Nop.Data.Mapping.Builders;
using Nop.Plugin.CW.Freight.Domain;

namespace Nop.Plugin.CW.Freight.Data.Mapping;

public class CWShippingMethodCustomerRoleMappingBuilder : NopEntityBuilder<CWShippingMethodCustomerRoleMapping>
{
    public override void MapEntity(CreateTableExpressionBuilder table)
    {
        table
            .WithColumn(nameof(CWShippingMethodCustomerRoleMapping.ShippingMethodId)).AsInt32().NotNullable()
            .WithColumn(nameof(CWShippingMethodCustomerRoleMapping.CustomerRoleId)).AsInt32().NotNullable();
    }
}
