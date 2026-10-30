using FluentMigrator.Builders.Create.Table;
using Nop.Data.Mapping.Builders;
using Nop.Plugin.CW.Core.Domain.Returns;

namespace Nop.Plugin.CW.Core.Data.Mapping.Returns;

public class ReturnCaseLineBuilder : NopEntityBuilder<ReturnCaseLine>
{
    public override void MapEntity(CreateTableExpressionBuilder table)
    {
        table.WithColumn(nameof(ReturnCaseLine.Id)).AsInt32().PrimaryKey().Identity()
            .WithColumn(nameof(ReturnCaseLine.ReturnCaseId)).AsInt32().NotNullable()
            .WithColumn(nameof(ReturnCaseLine.OrderItemId)).AsInt32().NotNullable()
            .WithColumn(nameof(ReturnCaseLine.Quantity)).AsInt32().NotNullable()
            .WithColumn(nameof(ReturnCaseLine.ModeId)).AsInt32().NotNullable()
            .WithColumn(nameof(ReturnCaseLine.ExchangeTypeId)).AsInt32().NotNullable()
            .WithColumn(nameof(ReturnCaseLine.ReplacementProductId)).AsInt32().Nullable()
            .WithColumn(nameof(ReturnCaseLine.ReplacementAttributesXml)).AsString(int.MaxValue).Nullable()
            .WithColumn(nameof(ReturnCaseLine.ReplacementAttributesInfo)).AsString(int.MaxValue).Nullable()
            .WithColumn(nameof(ReturnCaseLine.ReplacementQuantity)).AsInt32().Nullable()
            .WithColumn(nameof(ReturnCaseLine.DispositionId)).AsInt32().NotNullable()
            .WithColumn(nameof(ReturnCaseLine.DispositionReason)).AsString(int.MaxValue).Nullable()
            .WithColumn(nameof(ReturnCaseLine.DispositionOnUtc)).AsDateTime2().Nullable()
            .WithColumn(nameof(ReturnCaseLine.DispositionByCustomerId)).AsInt32().Nullable()
            .WithColumn(nameof(ReturnCaseLine.StaffShipQty)).AsInt32().Nullable()
            .WithColumn(nameof(ReturnCaseLine.StaffRepackQty)).AsInt32().Nullable()
            .WithColumn(nameof(ReturnCaseLine.ReturnRequestId)).AsInt32().Nullable();
    }
}