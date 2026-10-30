using FluentMigrator.Builders.Create.Table;
using Nop.Data.Extensions;
using Nop.Data.Mapping.Builders;
using Nop.Plugin.CW.Core.Domain.Subsidies;
using System.Data;

namespace Nop.Plugin.CW.Core.Data.Mapping.Subsidies;

public class ProductGroupTopupHistoryBuilder : NopEntityBuilder<ProductGroupTopupHistory>
{
    /// <summary>
    /// Apply entity configuration
    /// </summary>
    /// <param name="table">Create table expression builder</param>
    public override void MapEntity(CreateTableExpressionBuilder table)
    {
        table.WithColumn(nameof(ProductGroupTopupHistory.Id)).AsInt32().PrimaryKey().Identity()
             .WithColumn(nameof(ProductGroupTopupHistory.ProductGroupTopupId)).AsInt32().ForeignKey<ProductGroupTopup>(onDelete: Rule.None).NotNullable()
             .WithColumn(nameof(ProductGroupTopupHistory.Quantity)).AsInt32().NotNullable()
             .WithColumn(nameof(ProductGroupTopupHistory.Credit)).AsDecimal().NotNullable()
             .WithColumn(nameof(ProductGroupTopupHistory.Reason)).AsString().Nullable()
             .WithColumn(nameof(ProductGroupTopupHistory.CreatorId)).AsInt32().NotNullable()
             .WithColumn(nameof(ProductGroupTopupHistory.CreatedOnUtc)).AsDateTime2().NotNullable();
    }
}
