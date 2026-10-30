using FluentMigrator.Builders.Create.Table;
using Nop.Data.Mapping.Builders;
using Nop.Plugin.CW.Freight.Domain;

namespace Nop.Plugin.CW.Freight.Data.Mapping;

public class CWShippingMethodBuilder : NopEntityBuilder<CWShippingMethod>
{
    public override void MapEntity(CreateTableExpressionBuilder table)
    {
        table
            .WithColumn(nameof(CWShippingMethod.Name)).AsString(400).NotNullable()
            .WithColumn(nameof(CWShippingMethod.Description)).AsString(int.MaxValue).Nullable()
            .WithColumn(nameof(CWShippingMethod.DisplayOrder)).AsInt32().NotNullable()
            .WithColumn(nameof(CWShippingMethod.Rate)).AsDecimal(18, 4).NotNullable()
            .WithColumn(nameof(CWShippingMethod.LowestOrderTotal)).AsDecimal(18, 4).NotNullable()
            .WithColumn(nameof(CWShippingMethod.HighestOrderTotal)).AsDecimal(18, 4).NotNullable()
            .WithColumn(nameof(CWShippingMethod.ApplyPerBranch)).AsBoolean().NotNullable();
    }
}
