using FluentMigrator.Builders.Create.Table;
using Nop.Core.Domain.Stores;
using Nop.Data.Extensions;
using Nop.Data.Mapping.Builders;
using Nop.Plugin.CW.Core.Domain.Branches;
using System.Data;

namespace Nop.Plugin.CW.Core.Data.Mapping.Branches;

public class BranchBuilder : NopEntityBuilder<Branch>
{
    #region Methods

    /// <summary>
    /// Apply entity configuration
    /// </summary>
    /// <param name="table">Create table expression builder</param>
    public override void MapEntity(CreateTableExpressionBuilder table)
    {
        table.WithColumn(nameof(Branch.Id)).AsInt32().PrimaryKey().Identity()
            .WithColumn(nameof(Branch.ParentBranchId)).AsInt32().ForeignKey<Branch>(onDelete: Rule.None).Nullable()
            .WithColumn(nameof(Branch.BranchCode)).AsString().NotNullable()
            .WithColumn(nameof(Branch.Name)).AsString().NotNullable()
            .WithColumn(nameof(Branch.CardCode)).AsString().Nullable()
            .WithColumn(nameof(Branch.BranchNumber)).AsString().Nullable()
            .WithColumn(nameof(Branch.PaymentTerms)).AsString().Nullable()
            .WithColumn(nameof(Branch.StoreId)).AsInt32().ForeignKey<Store>()
            .WithColumn(nameof(Branch.DomainRegistrationFilter)).AsString().Nullable()
            .WithColumn(nameof(Branch.ShippingMethod)).AsString().Nullable()
            .WithColumn(nameof(Branch.Active)).AsBoolean().WithDefaultValue(true).NotNullable()
            .WithColumn(nameof(Branch.CreatedOnUtc)).AsDateTime2().NotNullable()
            .WithColumn(nameof(Branch.UpdatedOnUtc)).AsDateTime2().NotNullable()
            .WithColumn(nameof(Branch.Deleted)).AsBoolean().WithDefaultValue(false).NotNullable();
    }

    #endregion
}
