using FluentMigrator.Builders.Create.Table;
using Nop.Data.Extensions;
using Nop.Data.Mapping.Builders;
using Nop.Plugin.CW.Core.Domain.PackNames;
using Nop.Plugin.CW.Core.Domain.Subsidies;
using System.Data;

namespace Nop.Plugin.CW.Core.Data.Mapping.Subsidies;

public class SubsidyOrderShippingBuilder : NopEntityBuilder<SubsidyOrderShipping>
{
    /// <summary>
    /// Apply entity configuration
    /// </summary>
    /// <param name="table">Create table expression builder</param>
    public override void MapEntity(CreateTableExpressionBuilder table)
    {
        table.WithColumn(nameof(SubsidyOrderShipping.Id)).AsInt32().PrimaryKey().Identity()
             .WithColumn(nameof(SubsidyOrderShipping.OrderId)).AsInt32().NotNullable()
             .WithColumn(nameof(SubsidyOrderShipping.PackNameId)).AsInt32().ForeignKey<PackName>(onDelete: Rule.None).NotNullable()
             .WithColumn(nameof(SubsidyOrderShipping.SubsidyId)).AsInt32().ForeignKey<Subsidy>(onDelete: Rule.None).NotNullable()
             .WithColumn(nameof(SubsidyOrderShipping.Credit)).AsDecimal(18, 4).NotNullable()
             .WithColumn(nameof(SubsidyOrderShipping.CreditTopup)).AsDecimal(18, 4).NotNullable();
    }
}
