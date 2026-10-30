using FluentMigrator.Builders.Create.Table;
using Nop.Data.Mapping.Builders;
using Nop.Plugin.CW.Core.Domain.Exchanges;

namespace Nop.Plugin.CW.Core.Data.Mapping.Exchanges;

public class ReturnReasonRuleBuilder : NopEntityBuilder<ReturnReasonRule>
{
    public override void MapEntity(CreateTableExpressionBuilder table)
    {
        table.WithColumn(nameof(ReturnReasonRule.Id)).AsInt32().PrimaryKey().Identity()
            .WithColumn(nameof(ReturnReasonRule.ReturnReasonId)).AsInt32().NotNullable()
            .WithColumn(nameof(ReturnReasonRule.StoreId)).AsInt32().NotNullable()
            .WithColumn(nameof(ReturnReasonRule.AllowStoreCredit)).AsBoolean().NotNullable()
            .WithColumn(nameof(ReturnReasonRule.AllowExchange)).AsBoolean().NotNullable()
            .WithColumn(nameof(ReturnReasonRule.AllowRepackagingFee)).AsBoolean().NotNullable()
            .WithColumn(nameof(ReturnReasonRule.AllowShippingFee)).AsBoolean().NotNullable()
            .WithColumn(nameof(ReturnReasonRule.BackInStock)).AsBoolean().NotNullable()
            .WithColumn(nameof(ReturnReasonRule.IsDisabled)).AsBoolean().NotNullable();
    }
}
