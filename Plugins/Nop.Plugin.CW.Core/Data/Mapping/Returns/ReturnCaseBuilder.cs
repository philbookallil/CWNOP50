using FluentMigrator.Builders.Create.Table;
using Nop.Data.Mapping.Builders;
using Nop.Plugin.CW.Core.Domain.Returns;

namespace Nop.Plugin.CW.Core.Data.Mapping.Returns;

public class ReturnCaseBuilder : NopEntityBuilder<ReturnCase>
{
    public override void MapEntity(CreateTableExpressionBuilder table)
    {
        table.WithColumn(nameof(ReturnCase.Id)).AsInt32().PrimaryKey().Identity()
            .WithColumn(nameof(ReturnCase.StoreId)).AsInt32().NotNullable()
            .WithColumn(nameof(ReturnCase.CustomerId)).AsInt32().NotNullable()
            .WithColumn(nameof(ReturnCase.OrderId)).AsInt32().NotNullable()
            .WithColumn(nameof(ReturnCase.CaseNumber)).AsString(400).Nullable()
            .WithColumn(nameof(ReturnCase.ReturnReasonId)).AsInt32().Nullable()
            .WithColumn(nameof(ReturnCase.ReturnActionId)).AsInt32().Nullable()
            .WithColumn(nameof(ReturnCase.StatusId)).AsInt32().NotNullable()
            .WithColumn(nameof(ReturnCase.CustomerComments)).AsString(int.MaxValue).Nullable()
            .WithColumn(nameof(ReturnCase.CreatedOnUtc)).AsDateTime2().NotNullable()
            .WithColumn(nameof(ReturnCase.UpdatedOnUtc)).AsDateTime2().NotNullable()
            .WithColumn(nameof(ReturnCase.SubmittedOnUtc)).AsDateTime2().Nullable();
    }
}