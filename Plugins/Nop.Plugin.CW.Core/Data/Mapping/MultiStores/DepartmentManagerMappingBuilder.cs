using FluentMigrator.Builders.Create.Table;
using Nop.Data.Extensions;
using Nop.Data.Mapping;
using Nop.Data.Mapping.Builders;
using Nop.Plugin.CW.Core.Domain.MultiStores;
using Nop.Plugin.CW.Core.Domain.PackNames;


namespace Nop.Plugin.CW.Core.Data.Mapping.MultiStores
{
    public partial class DepartmentManagerMappingBuilder : NopEntityBuilder<DepartmentManagerMapping>
    {
        #region Methods

        /// <summary>
        /// Apply entity configuration
        /// </summary>
        /// <param name="table">Create table expression builder</param>
        public override void MapEntity(CreateTableExpressionBuilder table)
        {
            table
                .WithColumn(NameCompatibilityManager.GetColumnName(typeof(DepartmentManagerMapping), nameof(DepartmentManagerMapping.PackNameId)))
                    .AsInt32().ForeignKey<PackName>().PrimaryKey()
                .WithColumn(NameCompatibilityManager.GetColumnName(typeof(DepartmentManagerMapping), nameof(DepartmentManagerMapping.DepartmentId)))
                    .AsInt32().ForeignKey<Department>().PrimaryKey();
        }

        #endregion
    }
}
