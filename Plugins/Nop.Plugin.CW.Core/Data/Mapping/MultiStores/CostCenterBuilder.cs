using FluentMigrator.Builders.Create.Table;
using Nop.Core.Domain.Stores;
using Nop.Data.Extensions;
using Nop.Data.Mapping.Builders;
using Nop.Plugin.CW.Core.Domain.Branches;
using Nop.Plugin.CW.Core.Domain.MultiStores;
using System.Data;

namespace Nop.Plugin.CW.Core.Data.Mapping.MultiStores;

public class CostCenterBuilder : NopEntityBuilder<CostCenter>
{
    /// <summary>
    /// Apply entity configuration
    /// </summary>
    /// <param name="table">Create table expression builder</param>
    public override void MapEntity(CreateTableExpressionBuilder table)
    {
        table.WithColumn(nameof(CostCenter.Id)).AsInt32().PrimaryKey().Identity()
            .WithColumn(nameof(CostCenter.BranchId)).AsInt32().ForeignKey<Branch>(onDelete: Rule.None).Nullable()
            .WithColumn(nameof(CostCenter.StoreId)).AsInt32().ForeignKey<Store>()
            .WithColumn(nameof(CostCenter.Name)).AsString()
            .WithColumn(nameof(CostCenter.Active)).AsBoolean().WithDefaultValue(true).NotNullable()
            .WithColumn(nameof(CostCenter.Deleted)).AsBoolean().NotNullable();
    }
}