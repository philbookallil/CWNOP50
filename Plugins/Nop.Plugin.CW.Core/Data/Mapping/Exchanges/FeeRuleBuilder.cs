using FluentMigrator.Builders.Create.Table;
using Nop.Data.Mapping.Builders;
using Nop.Plugin.CW.Core.Domain.Exchanges;

namespace Nop.Plugin.CW.Core.Data.Mapping.Exchanges;

public class FeeRuleBuilder : NopEntityBuilder<FeeRule>
{
    public override void MapEntity(CreateTableExpressionBuilder table)
    {
        table.WithColumn(nameof(FeeRule.Id)).AsInt32().PrimaryKey().Identity()
            .WithColumn(nameof(FeeRule.StoreId)).AsInt32().NotNullable()
            .WithColumn(nameof(FeeRule.FeeTypeId)).AsInt32().NotNullable()
            .WithColumn(nameof(FeeRule.Amount)).AsDecimal(18, 4).NotNullable()
            .WithColumn(nameof(FeeRule.OperatorId)).AsInt32().NotNullable()
            .WithColumn(nameof(FeeRule.UnitsQuantity)).AsInt32().NotNullable();
    }
}
