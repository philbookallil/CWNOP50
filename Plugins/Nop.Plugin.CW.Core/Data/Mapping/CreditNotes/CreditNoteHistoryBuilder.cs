using FluentMigrator.Builders.Create.Table;
using Nop.Data.Mapping.Builders;
using Nop.Plugin.CW.Core.Domain.CreditNotes;

namespace Nop.Plugin.CW.Core.Data.Mapping.CreditNotes;

public class CreditNoteHistoryBuilder : NopEntityBuilder<CreditNoteHistory>
{
    public override void MapEntity(CreateTableExpressionBuilder table)
    {
        table.WithColumn(nameof(CreditNoteHistory.Id)).AsInt32().PrimaryKey().Identity()
            .WithColumn(nameof(CreditNoteHistory.CreditNoteId)).AsInt32().NotNullable()
            .WithColumn(nameof(CreditNoteHistory.CustomerId)).AsInt32().NotNullable()
            .WithColumn(nameof(CreditNoteHistory.OrderId)).AsInt32().NotNullable()
            .WithColumn(nameof(CreditNoteHistory.Amount)).AsDecimal(18, 2).NotNullable()
            .WithColumn(nameof(CreditNoteHistory.CreatedOnUtc)).AsDateTime2().NotNullable();
    }
}
