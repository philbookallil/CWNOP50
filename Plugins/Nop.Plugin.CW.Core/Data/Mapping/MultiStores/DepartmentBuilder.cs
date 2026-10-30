using FluentMigrator.Builders.Create.Table;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Stores;
using Nop.Data.Extensions;
using Nop.Data.Mapping.Builders;
using Nop.Plugin.CW.Core.Domain.MultiStores;

namespace Nop.Plugin.CW.Core.Data.Mapping.MultiStores;

public class DepartmentBuilder : NopEntityBuilder<Department>
{
    /// <summary>
    /// Apply entity configuration
    /// </summary>
    /// <param name="table">Create table expression builder</param>
    public override void MapEntity(CreateTableExpressionBuilder table)
    {
        table.WithColumn(nameof(Department.Id)).AsInt32().PrimaryKey().Identity()
            .WithColumn(nameof(Department.StoreId)).AsInt32().ForeignKey<Store>()
            .WithColumn(nameof(Department.Name)).AsString()
            .WithColumn(nameof(Department.Active)).AsBoolean().WithDefaultValue(true).NotNullable()
            .WithColumn(nameof(Department.CustomerRoleId)).AsInt32().ForeignKey<CustomerRole>().Nullable()
            .WithColumn(nameof(Department.Deleted)).AsBoolean().NotNullable();
    }
}