using FluentMigrator.Builders.Create.Table;
using Nop.Core.Domain.Customers;
using Nop.Data.Extensions;
using Nop.Data.Mapping.Builders;
using Nop.Plugin.CW.Core.Domain.MultiStores;
using Nop.Plugin.CW.Core.Domain.PackNames;
using System.Data;

namespace Nop.Plugin.CW.Core.Data.Mapping.PackNames;

public class PackNameBuilder : NopEntityBuilder<PackName>
{
    /// <summary>
    /// Apply entity configuration
    /// </summary>
    /// <param name="table">Create table expression builder</param>
    public override void MapEntity(CreateTableExpressionBuilder table)
    {
        table.WithColumn(nameof(PackName.Id)).AsInt32().PrimaryKey().Identity()
            .WithColumn(nameof(PackName.Name)).AsString(255)
            .WithColumn(nameof(PackName.Colour)).AsString(7)
            .WithColumn(nameof(PackName.Gender)).AsString(1).Nullable()
            .WithColumn(nameof(PackName.PrintName)).AsString(255).Nullable()
            .WithColumn(nameof(PackName.Group)).AsBoolean()
            .WithColumn(nameof(PackName.Active)).AsBoolean()
            .WithColumn(nameof(PackName.PackNameListId)).AsInt32().ForeignKey<PackNameList>(onDelete: Rule.None)
            .WithColumn(nameof(PackName.CustomerId)).AsInt32().ForeignKey<Customer>(onDelete: Rule.None).Nullable()
            .WithColumn(nameof(PackName.DepartmentId)).AsInt32().ForeignKey<Department>(onDelete: Rule.None).Nullable()
            .WithColumn(nameof(PackName.CostCenterId)).AsInt32().ForeignKey<CostCenter>(onDelete: Rule.None).Nullable()
            .WithColumn(nameof(PackName.Manager)).AsBoolean().NotNullable()
            .WithColumn(nameof(PackName.EmployeeNumber)).AsString(255).Nullable()
            .WithColumn(nameof(PackName.POEntry)).AsString(255).Nullable()
            .WithColumn(nameof(PackName.Comments)).AsString(int.MaxValue).Nullable()
            .WithColumn(nameof(PackName.Deleted)).AsBoolean().NotNullable();
    }
}