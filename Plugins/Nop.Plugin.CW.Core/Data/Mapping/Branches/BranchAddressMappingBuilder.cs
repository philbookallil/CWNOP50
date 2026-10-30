using FluentMigrator.Builders.Create.Table;
using Nop.Core.Domain.Common;
using Nop.Data.Extensions;
using Nop.Data.Mapping.Builders;
using Nop.Plugin.CW.Core.Domain.Branches;

namespace Nop.Plugin.CW.Core.Data.Mapping.Branches;

public class BranchAddressMappingBuilder : NopEntityBuilder<BranchAddressMapping>
{
    #region Methods

    /// <summary>
    /// Apply entity configuration
    /// </summary>
    /// <param name="table">Create table expression builder</param>
    public override void MapEntity(CreateTableExpressionBuilder table)
    {
        table
            .WithColumn(nameof(BranchAddressMapping.AddressId)).AsInt32().ForeignKey<Address>().PrimaryKey()
            .WithColumn(nameof(BranchAddressMapping.BranchId)).AsInt32().ForeignKey<Branch>().PrimaryKey();
    }

    #endregion
}
