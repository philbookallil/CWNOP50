using FluentMigrator.Builders.Create.Table;
using Nop.Core.Domain.Stores;
using Nop.Data.Extensions;
using Nop.Data.Mapping.Builders;
using Nop.Plugin.CW.Core.Domain.Branches;
using Nop.Plugin.CW.Core.Domain.PackNames;
using System.Data;

namespace Nop.Plugin.CW.Core.Data.Mapping.PackNames;

public class PackNameListBuilder : NopEntityBuilder<PackNameList>
{
    /// <summary>
    /// Apply entity configuration
    /// </summary>
    /// <param name="table">Create table expression builder</param>
    public override void MapEntity(CreateTableExpressionBuilder table)
    {
        table.WithColumn(nameof(PackNameList.Id)).AsInt32().PrimaryKey().Identity()
            .WithColumn(nameof(PackNameList.Name)).AsString()
            .WithColumn(nameof(PackNameList.StoreId)).AsInt32().ForeignKey<Store>(onDelete: Rule.None)
            .WithColumn(nameof(PackNameList.BranchId)).AsInt32().ForeignKey<Branch>(onDelete: Rule.None).Nullable()
            .WithColumn(nameof(PackNameList.Active)).AsBoolean()
            .WithColumn(nameof(PackNameList.AllManagers)).AsBoolean()
            .WithColumn(nameof(PackNameList.ParentManagers)).AsBoolean()
            .WithColumn(nameof(PackNameList.IsDefault)).AsBoolean()
            .WithColumn(nameof(PackNameList.CreatedOnUtc)).AsDateTime2().NotNullable()
            .WithColumn(nameof(PackNameList.Deleted)).AsBoolean().NotNullable();
    }
}