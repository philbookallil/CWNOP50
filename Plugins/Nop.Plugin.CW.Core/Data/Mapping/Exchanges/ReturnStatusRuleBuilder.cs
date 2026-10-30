using FluentMigrator.Builders.Create.Table;
using Nop.Data.Mapping.Builders;
using Nop.Plugin.CW.Core.Domain.Exchanges;

namespace Nop.Plugin.CW.Core.Data.Mapping.Exchanges;

public class ReturnStatusRuleBuilder : NopEntityBuilder<ReturnStatusRule>
{
    public override void MapEntity(CreateTableExpressionBuilder table)
    {
        table.WithColumn(nameof(ReturnStatusRule.Id)).AsInt32().PrimaryKey().Identity()
            .WithColumn(nameof(ReturnStatusRule.ReturnStatusId)).AsInt32().NotNullable()
            .WithColumn(nameof(ReturnStatusRule.StoreId)).AsInt32().NotNullable()
            .WithColumn(nameof(ReturnStatusRule.PerformStoreCredit)).AsBoolean().NotNullable()
            .WithColumn(nameof(ReturnStatusRule.PerformExchange)).AsBoolean().NotNullable()
            .WithColumn(nameof(ReturnStatusRule.BackInStock)).AsBoolean().NotNullable()
            .WithColumn(nameof(ReturnStatusRule.Authorised)).AsBoolean().NotNullable();
    }
}
