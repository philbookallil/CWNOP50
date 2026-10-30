using FluentMigrator.Builders.Create.Table;
using Nop.Data.Mapping.Builders;
using Nop.Plugin.CW.Core.Domain.Exchanges;

namespace Nop.Plugin.CW.Core.Data.Mapping.Exchanges;

public class ReturnRequestItemBuilder : NopEntityBuilder<ReturnRequestItem>
{
    public override void MapEntity(CreateTableExpressionBuilder table)
    {
        table.WithColumn(nameof(ReturnRequestItem.Id)).AsInt32().PrimaryKey().Identity()
            .WithColumn(nameof(ReturnRequestItem.ReturnRequestId)).AsInt32().NotNullable()
            .WithColumn(nameof(ReturnRequestItem.ExchangeModeId)).AsInt32().NotNullable()
            .WithColumn(nameof(ReturnRequestItem.ExchangeTypeId)).AsInt32().NotNullable()
            .WithColumn(nameof(ReturnRequestItem.ReturnReasonId)).AsInt32().NotNullable()
            .WithColumn(nameof(ReturnRequestItem.ReturnActionId)).AsInt32().NotNullable()
            .WithColumn(nameof(ReturnRequestItem.StaffShipQty)).AsInt32().Nullable()
            .WithColumn(nameof(ReturnRequestItem.StaffRepackQty)).AsInt32().Nullable()
            .WithColumn(nameof(ReturnRequestItem.ReturnRequestGroup)).AsGuid().Nullable()
            .WithColumn(nameof(ReturnRequestItem.NeedsCompletion)).AsBoolean().NotNullable()
            .WithColumn(nameof(ReturnRequestItem.IsCompleted)).AsBoolean().NotNullable()
            .WithColumn(nameof(ReturnRequestItem.ShoppingCartItemId)).AsInt32().Nullable()
            .WithColumn(nameof(ReturnRequestItem.OrderItemId)).AsInt32().Nullable()
            .WithColumn(nameof(ReturnRequestItem.ReturnedOrderItemId)).AsInt32().NotNullable()
            .WithColumn(nameof(ReturnRequestItem.AdditionalFeeName)).AsString().Nullable()
            .WithColumn(nameof(ReturnRequestItem.AdditionalFeeAmount)).AsDecimal(18, 4).Nullable();
    }
}
