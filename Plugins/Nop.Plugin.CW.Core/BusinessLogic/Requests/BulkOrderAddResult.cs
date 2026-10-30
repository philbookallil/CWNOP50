namespace Nop.Plugin.CW.Core.BusinessLogic.Requests;

/// <summary>
/// What happened, line by line. Lines that pass are committed even when others
/// fail, so this is never "it worked" or "it did not" - it is always a set of outcomes.
/// </summary>
/// <param name="AddedQuantity">
/// UNITS that reached the cart, not lines. Units because the two other numbers this same response drives -
/// the card's "n in cart" and the header counter - are both units, and three numbers about one action with
/// one of them counting something else is how a customer stops trusting all three.
/// </param>
/// <param name="Results">One entry per line submitted, in the order submitted</param>
/// <param name="CartQuantities">
/// Units of each product now in the cart, keyed by product id, packname-scoped exactly as the list's own
/// "n in cart" figure is. The WHOLE map, not a delta: nop merges a matching line into an existing one, and
/// addRequiredProducts can add a product the customer never typed a quantity for, so the client cannot
/// derive the new numbers from what it submitted.
/// </param>
/// <param name="CartTotalQuantity">
/// Units in the customer's full store cart - not packname-scoped, because this feeds the header counter,
/// which counts the whole cart everywhere else on the site.
/// </param>
public record BulkOrderAddResult(
    int AddedQuantity,
    IList<BulkOrderLineResult> Results,
    IDictionary<int, int> CartQuantities,
    int CartTotalQuantity);

/// <param name="LineIndex">Position in the submitted list</param>
/// <param name="ProductId">The product</param>
/// <param name="CellKey">Echoed back from the request, so the client can find the box</param>
/// <param name="Warnings">
/// nop's own warnings, unedited. Empty means the line is in the cart. We do not summarise or rewrite them:
/// they are the store's wording, already localized, and a customer who sees a different sentence here from
/// the one the product page would show has been told two different things about one rule.
/// </param>
public record BulkOrderLineResult(int LineIndex, int ProductId, string CellKey, IList<string> Warnings);