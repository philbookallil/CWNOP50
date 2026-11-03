namespace Nop.Plugin.Integration.Odoo.Models.Responses;

/// <summary>
/// One page of a <c>search_read</c>, read record by record: a record Odoo returned in a shape the
/// response class cannot read lands in <see cref="Errors"/> instead of failing the whole page.
/// </summary>
/// <typeparam name="T">The response class each record is read into.</typeparam>
/// <param name="Records">The records that were read.</param>
/// <param name="Errors">The records that could not be read.</param>
public record OdooRecordPage<T>(IReadOnlyList<T> Records, IReadOnlyList<OdooRecordReadError> Errors)
{
    /// <summary>
    /// How many records Odoo returned, read or not. Paging compares this, not <see cref="Records"/>,
    /// with the page size — otherwise an unreadable record would look like the end of the data.
    /// </summary>
    public int Count => Records.Count + Errors.Count;
}

/// <summary>
/// A record Odoo returned that could not be read.
/// </summary>
/// <param name="OdooId">The record's Odoo id, or <c>null</c> when even the id was unreadable.</param>
/// <param name="Message">Why it could not be read, including the JSON path of the offending field.</param>
public record OdooRecordReadError(int? OdooId, string Message);
