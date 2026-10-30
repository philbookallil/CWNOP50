using FluentMigrator.Builders.Create.Table;
using Nop.Data.Mapping.Builders;
using Nop.Plugin.CW.Core.Domain.Returns;

namespace Nop.Plugin.CW.Core.Data.Mapping.Returns;

public class ReturnChargeBuilder : NopEntityBuilder<ReturnCharge>
{
    public override void MapEntity(CreateTableExpressionBuilder table)
    {
        table.WithColumn(nameof(ReturnCharge.Id)).AsInt32().PrimaryKey().Identity()
            .WithColumn(nameof(ReturnCharge.ReturnResolutionId)).AsInt32().NotNullable()
            .WithColumn(nameof(ReturnCharge.ReturnCaseLineId)).AsInt32().Nullable()
            .WithColumn(nameof(ReturnCharge.FeeTypeId)).AsInt32().NotNullable()
            .WithColumn(nameof(ReturnCharge.Amount)).AsDecimal(18, 4).NotNullable()
            .WithColumn(nameof(ReturnCharge.Quantity)).AsInt32().NotNullable()
            .WithColumn(nameof(ReturnCharge.SourceFeeRuleId)).AsInt32().Nullable()
            .WithColumn(nameof(ReturnCharge.Description)).AsString(400).Nullable();
    }
}