namespace Nop.Plugin.CW.Core.Domain.BulkOrder;

/// <summary>
/// Why a product cannot be ordered from the bulk screen even though it is perfectly orderable from its own
/// page. Every member names data nop demands at add-to-cart that a grid of quantity boxes has
/// nowhere to collect - NOT a stock or permission problem, which is why these do not go through the
/// disabled-cell rule and get their own message and a link to the product page.
/// </summary>
public enum BulkOrderUnsupportedReason
{
    /// <summary>The product can be ordered here.</summary>
    None = 0,

    /// <summary>
    /// IsGiftCard. Needs a recipient name and sender name, plus both email addresses when GiftCardType is
    /// Virtual, and nop rejects a malformed one (ShoppingCartService.cs:1049-1066). Four fields per LINE.
    /// </summary>
    GiftCard = 10,

    /// <summary>
    /// IsRental. Needs a start and an end date per line, start before end
    /// (ShoppingCartService.cs:1091-1107).
    /// </summary>
    Rental = 20,

    /// <summary>
    /// CustomerEntersPrice. Needs a price inside Minimum/MaximumCustomerEnteredPrice
    /// (ShoppingCartService.cs:388-396). A quantity box cannot ask for one, and there is nothing to price.
    /// </summary>
    CustomerEntersPrice = 30,

    /// <summary>
    /// IsRecurring. The only member that needs no extra DATA: nop refuses to hold a recurring product beside
    /// a standard one in the same cart (ShoppingCartService.cs:1224-1226), and a bulk screen exists to add
    /// several products at once. Reconsider if a store ever sells recurring products in bulk and nothing else.
    /// </summary>
    Recurring = 40,

    /// <summary>
    /// A REQUIRED attribute that takes free text, a date or a file, which applies to EVERY line of this card —
    /// so nop will demand a value the grid has no input for. These render as "Not available on
    /// this screen".
    /// </summary>
    /// <remarks>
    /// Unlike every other member this is not a property of the product: it depends on the pin state, because a
    /// conditionally-required attribute is only required when its condition is met.
    ///
    /// It is reached two ways. Either the attribute is
    /// unconditional, or conditional with a condition the pin state has met — both apply to the whole card. Or
    /// its condition depends on an AXIS, applying only to cells whose axis value satisfies it, reaching here
    /// only when EVERY composed cell is one of those (the depended-on axis has a single value, so every cell
    /// carries it). One that blocks only some cells does not come here at all: the product is
    /// orderable and the block is per cell.
    /// </remarks>
    RequiredTextAttribute = 50,

    /// <summary>
    /// ProductType is not SimpleProduct. A grouped product is a list of other products: nop's cart refuses it
    /// outright ("This is not simple product", ShoppingCartService.cs:368) and nop's own catalogue links to
    /// the page that lists the children. Falling through the disabled-cell rule rendered one greyed box
    /// reading "Not available", which is the wrong sentence - the children are available.
    /// </summary>
    Grouped = 60,

    /// <summary>
    /// DisableBuyButton. nop's cart refuses it (ShoppingCartService.cs:353) and nop's catalogue still shows
    /// the product, without a button. Same correction as Grouped: a reason and a link, not a stock glyph.
    /// </summary>
    BuyDisabled = 70
}