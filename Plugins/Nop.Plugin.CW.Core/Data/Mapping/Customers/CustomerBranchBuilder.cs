using FluentMigrator.Builders.Create.Table;
using Nop.Core.Domain.Customers;
using Nop.Data.Extensions;
using Nop.Data.Mapping.Builders;
using Nop.Plugin.CW.Core.Domain.Branches;
using Nop.Plugin.CW.Core.Domain.Customers;
using System.Data;

namespace Nop.Plugin.CW.Core.Data.Mapping.Customers;

public class CustomerBranchBuilder : NopEntityBuilder<CustomerBranch>
{
    /// <summary>
    /// Apply entity configuration
    /// </summary>
    /// <param name="table">Create table expression builder</param>
    public override void MapEntity(CreateTableExpressionBuilder table)
    {
        table.WithColumn(nameof(CustomerBranch.Id)).AsInt32().ForeignKey<Customer>().PrimaryKey()
            .WithColumn(nameof(CustomerBranch.BranchId)).AsInt32().Nullable().ForeignKey<Branch>().OnDelete(Rule.None)
            .WithColumn(nameof(CustomerBranch.JimCardCode)).AsString().Nullable()
            .WithColumn(nameof(CustomerBranch.LastModifiedDateUtc)).AsDateTime2().NotNullable();
    }
}