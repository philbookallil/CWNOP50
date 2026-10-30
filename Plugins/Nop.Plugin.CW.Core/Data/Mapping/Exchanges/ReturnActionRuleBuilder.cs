using FluentMigrator.Builders.Create.Table;
using Nop.Data.Mapping.Builders;
using Nop.Plugin.CW.Core.Domain.Exchanges;

namespace Nop.Plugin.CW.Core.Data.Mapping.Exchanges;

public class ReturnActionRuleBuilder : NopEntityBuilder<ReturnActionRule>
{
    public override void MapEntity(CreateTableExpressionBuilder table)
    {
        table.WithColumn(nameof(ReturnActionRule.Id)).AsInt32().PrimaryKey().Identity()
            .WithColumn(nameof(ReturnActionRule.ReturnActionId)).AsInt32().NotNullable()
            .WithColumn(nameof(ReturnActionRule.StoreId)).AsInt32().NotNullable()
            .WithColumn(nameof(ReturnActionRule.AllowStoreCredit)).AsBoolean().NotNullable()
            .WithColumn(nameof(ReturnActionRule.AllowExchange)).AsBoolean().NotNullable()
            .WithColumn(nameof(ReturnActionRule.AllowRepackagingFee)).AsBoolean().NotNullable()
            .WithColumn(nameof(ReturnActionRule.AllowShippingFee)).AsBoolean().NotNullable()
            .WithColumn(nameof(ReturnActionRule.BackInStock)).AsBoolean().NotNullable()
            .WithColumn(nameof(ReturnActionRule.IsDisabled)).AsBoolean().NotNullable();
    }
}
