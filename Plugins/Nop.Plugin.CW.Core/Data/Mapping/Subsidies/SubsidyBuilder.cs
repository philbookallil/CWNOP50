using FluentMigrator.Builders.Create.Table;
using Nop.Core.Domain.Stores;
using Nop.Data.Extensions;
using Nop.Data.Mapping.Builders;
using Nop.Plugin.CW.Core.Domain.Subsidies;

namespace Nop.Plugin.CW.Core.Data.Mapping.Subsidies;

public class SubsidyBuilder : NopEntityBuilder<Subsidy>
{
    #region Methods

    /// <summary>
    /// Apply entity configuration
    /// </summary>
    /// <param name="table">Create table expression builder</param>
    public override void MapEntity(CreateTableExpressionBuilder table)
    {
        table.WithColumn(nameof(Subsidy.Id)).AsInt32().PrimaryKey().Identity()
             .WithColumn(nameof(Subsidy.StoreId)).AsInt32().ForeignKey<Store>().NotNullable()
             .WithColumn(nameof(Subsidy.Name)).AsString(100).NotNullable()
             .WithColumn(nameof(Subsidy.Description)).AsString().Nullable()
             .WithColumn(nameof(Subsidy.Credit)).AsDecimal(18, 4).NotNullable()
             .WithColumn(nameof(Subsidy.DurationStart)).AsDate().NotNullable()
             .WithColumn(nameof(Subsidy.DurationEnd)).AsDate().NotNullable()
             .WithColumn(nameof(Subsidy.AnnualRecurrence)).AsBoolean().NotNullable()
             .WithColumn(nameof(Subsidy.OrderOutsidePeriod)).AsBoolean().NotNullable()
             .WithColumn(nameof(Subsidy.OnlyAllocatedProducts)).AsBoolean().NotNullable()
             .WithColumn(nameof(Subsidy.IncludeManagers)).AsBoolean().NotNullable()
             .WithColumn(nameof(Subsidy.RestrictToAllocatedQty)).AsBoolean().NotNullable()
             .WithColumn(nameof(Subsidy.CreatedOnUtc)).AsDateTime2().NotNullable()
             .WithColumn(nameof(Subsidy.Active)).AsBoolean().NotNullable()
             .WithColumn(nameof(Subsidy.Deleted)).AsBoolean().NotNullable();
    }

    #endregion
}
