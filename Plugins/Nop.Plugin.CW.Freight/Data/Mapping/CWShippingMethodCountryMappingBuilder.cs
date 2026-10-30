using FluentMigrator.Builders.Create.Table;
using Nop.Data.Mapping.Builders;
using Nop.Plugin.CW.Freight.Domain;

namespace Nop.Plugin.CW.Freight.Data.Mapping;

public class CWShippingMethodCountryMappingBuilder : NopEntityBuilder<CWShippingMethodCountryMapping>
{
    public override void MapEntity(CreateTableExpressionBuilder table)
    {
        table
            .WithColumn(nameof(CWShippingMethodCountryMapping.ShippingMethodId)).AsInt32().NotNullable()
            .WithColumn(nameof(CWShippingMethodCountryMapping.CountryId)).AsInt32().NotNullable();
    }
}
