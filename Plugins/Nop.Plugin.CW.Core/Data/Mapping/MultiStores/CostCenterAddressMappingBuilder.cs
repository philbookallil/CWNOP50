using FluentMigrator.Builders.Create.Table;
using Nop.Core.Domain.Common;
using Nop.Data.Extensions;
using Nop.Data.Mapping;
using Nop.Data.Mapping.Builders;
using Nop.Plugin.CW.Core.Domain.MultiStores;


namespace Nop.Plugin.CW.Core.Data.Mapping.MultiStores
{
    public partial class CostCenterAddressMappingBuilder : NopEntityBuilder<CostCenterAddressMapping>
    {
        #region Methods

        /// <summary>
        /// Apply entity configuration
        /// </summary>
        /// <param name="table">Create table expression builder</param>
        public override void MapEntity(CreateTableExpressionBuilder table)
        {
            table
                .WithColumn(NameCompatibilityManager.GetColumnName(typeof(CostCenterAddressMapping), nameof(CostCenterAddressMapping.AddressId)))
                    .AsInt32().ForeignKey<Address>().PrimaryKey()
                .WithColumn(NameCompatibilityManager.GetColumnName(typeof(CostCenterAddressMapping), nameof(CostCenterAddressMapping.CostCenterId)))
                    .AsInt32().ForeignKey<CostCenter>().PrimaryKey();
        }

        #endregion
    }
}
