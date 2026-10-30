namespace Nop.Plugin.CW.Core.BusinessLogic.Requests;

/// <summary>
/// What the bulk screen asks the cart to do. One request, many lines - a single card's button
/// and the page-wide button differ only in how many lines the client puts in here.
/// </summary>
public record BulkOrderAddRequest(IList<BulkOrderLine> Lines);

/// <summary>
/// One cell's worth of order. Arity-agnostic on purpose: a fourth or fifth axis needs no new code, which was
/// the single biggest limitation of the previous implementation.
/// </summary>
/// <param name="ProductId">The product</param>
/// <param name="Selections">Axis values plus the card's pinned values, merged client-side</param>
/// <param name="Quantity">Units. Zero or negative lines are dropped before anything is validated.</param>
/// <param name="CellKey">
/// The cell's AXIS key, echoed back untouched so the client can attribute the result to the box the customer
/// typed in. Never trusted, never parsed, never used to look anything up - it is a correlation token.
/// </param>
public record BulkOrderLine(
    int ProductId,
    IList<BulkOrderSelection> Selections,
    int Quantity,
    string CellKey);

/// <param name="ProductAttributeMappingId">The attribute</param>
/// <param name="ValueIds">Chosen value ids. A list, because a checkbox attribute can hold several.</param>
/// <param name="EnteredText">
/// For a non-combinable attribute (text, date, file). Unused - the grid has no input for it - but part
/// of the contract now so that adding one later is a view change rather than a contract change.
/// </param>
public record BulkOrderSelection(
    int ProductAttributeMappingId,
    IList<int> ValueIds,
    string EnteredText);