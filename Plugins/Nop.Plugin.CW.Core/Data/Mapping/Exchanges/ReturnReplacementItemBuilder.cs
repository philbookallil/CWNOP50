using FluentMigrator.Builders.Create.Table;
using Nop.Data.Mapping.Builders;
using Nop.Plugin.CW.Core.Domain.Exchanges;

namespace Nop.Plugin.CW.Core.Data.Mapping.Exchanges;

public class ReturnReplacementItemBuilder : NopEntityBuilder<ReturnReplacementItem>
{
    public override void MapEntity(CreateTableExpressionBuilder table)
    {
        table.WithColumn(nameof(ReturnReplacementItem.Id)).AsInt32().PrimaryKey().Identity()
            .WithColumn(nameof(ReturnReplacementItem.AttributesInfo)).AsString(int.MaxValue).Nullable()
            .WithColumn(nameof(ReturnReplacementItem.AttributesXml)).AsString(int.MaxValue).Nullable()
            .WithColumn(nameof(ReturnReplacementItem.ProductId)).AsInt32().NotNullable()
            .WithColumn(nameof(ReturnReplacementItem.ProductName)).AsString().Nullable()
            .WithColumn(nameof(ReturnReplacementItem.ProductSeName)).AsString().Nullable()
            .WithColumn(nameof(ReturnReplacementItem.Quantity)).AsInt32().NotNullable()
            .WithColumn(nameof(ReturnReplacementItem.UnitPrice)).AsDecimal(18, 4).NotNullable()
            .WithColumn(nameof(ReturnReplacementItem.ReturnRequestItemId)).AsInt32().NotNullable();
    }
}
