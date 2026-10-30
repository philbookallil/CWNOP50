using FluentMigrator.Builders.Create.Table;
using Nop.Core.Domain.Common;
using Nop.Data.Extensions;
using Nop.Data.Mapping;
using Nop.Data.Mapping.Builders;
using Nop.Plugin.CW.Core.Domain.PackNames;


namespace Nop.Plugin.CW.Core.Data.Mapping.PackNames
{
    public partial class PackNameAddressMappingBuilder : NopEntityBuilder<PackNameAddressMapping>
    {
        #region Methods

        /// <summary>
        /// Apply entity configuration
        /// </summary>
        /// <param name="table">Create table expression builder</param>
        public override void MapEntity(CreateTableExpressionBuilder table)
        {
            table
                .WithColumn(NameCompatibilityManager.GetColumnName(typeof(PackNameAddressMapping), nameof(PackNameAddressMapping.AddressId)))
                    .AsInt32().ForeignKey<Address>().PrimaryKey()
                .WithColumn(NameCompatibilityManager.GetColumnName(typeof(PackNameAddressMapping), nameof(PackNameAddressMapping.PackNameId)))
                    .AsInt32().ForeignKey<PackName>().PrimaryKey();
        }

        #endregion
    }
}
