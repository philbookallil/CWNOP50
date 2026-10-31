# PackName Checkout — Per-Item Subsidy Breakdown

> **4.9 history.** Copied from the 4.90 source (`NOP49/CWPlans/`) on 2026-10-31, when CW.Core moved to nopCommerce 5.00. It is kept as the record of the 4.9 work and is **not updated for 5.00**: file paths, line numbers and versions are 4.9's. For how CW.Core works now, read the references in `CWPlans/` and `CWPlans/CWCore_Port_Reference.md`.

**Status:** Draft — awaiting human approval before implementation  
**Date:** 2026-07-23  
**Complexity:** Tier 2

---

## 1. Goal

Show the subsidy being consumed per cart item in the PackName checkout product list. For each item that has any subsidy applied, display compact chips beneath the subtotal that reveal:

- Quantity covered by a product-group allocation (PG qty)
- Credit from a product-group credit pool (PG credit)
- Credit from the global subsidy pool

For the packname shipping row, show how much of the shipping cost is covered by subsidy.

No new column is added. Everything sits inside the existing product data cell.

---

## 2. Background — How Subsidy Calculation Works

### 2.1 Two calculation paths

`SubsidyCreditService` has two parallel paths:

| Path | Methods | Effect |
|---|---|---|
| **Process** | `ProcessPackNameSubsidyQuantity`, `ProcessPackNameSubsidyCredit` | Mutates cloned credit objects; returns remaining amounts. No record kept. |
| **Apply** | `ApplyPackNameSubsidyQuantity`, `ApplyPackNameSubsidyCredit`, `GetPackNameShippingRemaining` | Mutates credits AND appends `SubsidyOrderItem`/`SubsidyOrderShipping` records to caller-supplied lists. |

The existing checkout total calculation (`CalculateSubsidyTotalAsync`) uses **Process** — fast, no records. Order placement uses **Apply** — persists to database.

For this feature we need a **non-persisted Apply run** — a dry run that uses Apply logic so we know what was consumed per item, but throws the records away rather than saving them.

### 2.2 Credit consumption order (per §14.5 CLAUDE.md)

1. PG quantity allocation (`Quantity`) → covers whole units
2. PG quantity topup (`QuantityTopup`) → additional whole units
3. PG credit pool (`ProductGroupCredit`) → dollar value for items in this PG
4. PG credit topup (`ProductGroupCreditTopup`)
5. Global subsidy credit (`Credit`)
6. Global credit topup (`CreditTopup`)

### 2.3 Shipping split

Packnames sharing the same address and shipping method split the shipping cost equally. Each packname's share is then paid from its remaining global credit via `GetPackNameShippingRemaining`. If the shipping cost exceeds available credit, the remainder is charged normally.

### 2.4 SubsidyOrderItem structure

`SubsidyOrderItem.Id` is **repurposed** as the `ShoppingCartItemId` during the Apply run (not a database primary key at this stage).

```csharp
// Fields relevant to the per-item display:
int     Id;                    // = ShoppingCartItemId
int?    ProductGroupId;        // null → global credit row
int     Quantity;              // units covered by PG qty
int     QuantityTopup;         // units covered by PG qty topup
decimal ProductGroupCredit;    // incl-tax value from PG credit pool
decimal ProductGroupCreditTopup;
decimal Credit;                // incl-tax value from global pool
decimal CreditTopup;
```

---

## 3. New Files

### 3.1 `Domain/Subsidies/DTOs/CartSubsidyPreview.cs`

```csharp
namespace Nop.Plugin.CW.Core.Domain.Subsidies.DTOs;

/// <summary>
/// Result of a dry-run Apply pass over the cart subsidy logic.
/// Items are keyed by <c>SubsidyOrderItem.Id == ShoppingCartItemId</c>.
/// ShippingCredits are keyed by <c>PackNameId</c>.
/// </summary>
public record CartSubsidyPreview
{
    public IList<SubsidyOrderItem> Items { get; init; } = [];
    public IList<SubsidyOrderShipping> ShippingCredits { get; init; } = [];
}
```

### 3.2 `Models/Subsidies/SubsidyItemBreakdownModel.cs`

```csharp
namespace Nop.Plugin.CW.Core.Models.Subsidies;

/// <summary>
/// A single subsidy source row shown under a cart item's subtotal.
/// One row per distinct product group (or one row for global credit).
/// </summary>
public record SubsidyItemBreakdownModel
{
    /// <summary>Null means this row represents global credit, not a product group.</summary>
    public int? ProductGroupId { get; init; }

    public string? ProductGroupName { get; init; }

    /// <summary>Symbol ID in pg-icons.svg (e.g. "pg-icon-Shirts"). Matches ProductGroup.Icon.</summary>
    public string? ProductGroupIcon { get; init; }

    /// <summary>Hex colour, e.g. "#e91e63".</summary>
    public string? ProductGroupColour { get; init; }

    /// <summary>Total units covered by PG quantity allocation (Quantity + QuantityTopup). Zero if none.</summary>
    public int QuantityUsed { get; init; }

    /// <summary>
    /// Total credit value covered, incl. tax.
    /// For PG rows: ProductGroupCredit + ProductGroupCreditTopup.
    /// For global rows: Credit + CreditTopup.
    /// </summary>
    public decimal CreditUsed { get; init; }

    /// <summary>Pre-formatted credit string for display, e.g. "−$12.50". Null if CreditUsed == 0.</summary>
    public string? CreditDisplay { get; init; }
}
```

---

## 4. Modified Files

### 4.1 `Services/Orders/IOrderTotalCalculationOverrideService.cs`

Add one method to the interface:

```csharp
/// <summary>
/// Performs a dry-run of the subsidy Apply pass over the cart.
/// Returns per-item breakdown records and per-packname shipping credits
/// without persisting anything.
/// </summary>
Task<CartSubsidyPreview> PreviewSubsidyBreakdownAsync(
    IList<PackNameShoppingCartItem> cart,
    Customer customer,
    int storeId);
```

Add the required using at the top of the file:
```csharp
using Nop.Plugin.CW.Core.Domain.Subsidies.DTOs;
```

---

### 4.2 `Services/Orders/OrderTotalCalculationOverrideService.cs`

Add the implementation of `PreviewSubsidyBreakdownAsync`. Place it in the `#region Methods` block, near `CalculateSubsidyTotalAsync`.

```csharp
/// <inheritdoc/>
public async Task<CartSubsidyPreview> PreviewSubsidyBreakdownAsync(
    IList<PackNameShoppingCartItem> cart,
    Customer customer,
    int storeId)
{
    ArgumentNullException.ThrowIfNull(cart);
    ArgumentNullException.ThrowIfNull(customer);

    if (!cart.Any())
        return new CartSubsidyPreview();

    var packNameIds = cart.Select(item => item.PackNameId).Distinct().ToList();

    // GetPackNamesSubsidyCreditAsync already returns deep clones — safe to mutate.
    var packNameCredits = await _subsidyCreditService.GetPackNamesSubsidyCreditAsync(packNameIds);

    var orderItems = new List<SubsidyOrderItem>();

    foreach (var shoppingCartItem in cart)
    {
        var packNameCredit = packNameCredits.FirstOrDefault(x => x.PackNameId == shoppingCartItem.PackNameId);
        if (packNameCredit == null || !packNameCredit.SubsidyCredits.Any())
            continue;

        var product = await _productService.GetProductByIdAsync(shoppingCartItem.ProductId);
        var sciSubTotal = (await _shoppingCartService.GetSubTotalAsync(shoppingCartItem, true)).subTotal;

        var (sciInclTax, _) = await _taxService.GetProductPriceAsync(product, sciSubTotal, true, customer);
        var (sciExclTax, _) = await _taxService.GetProductPriceAsync(product, sciSubTotal, false, customer);

        var unitPriceInclTax = sciInclTax / shoppingCartItem.Quantity;
        var unitPriceExclTax = sciExclTax / shoppingCartItem.Quantity;

        // Apply quantity allocation — mutates packNameCredit, populates orderItems
        var quantityLeft = _subsidyCreditService.ApplyPackNameSubsidyQuantity(
            packNameCredit, shoppingCartItem, shoppingCartItem.Quantity, orderItems);

        // Units covered by allocation reduce the remaining credit amount to apply
        if (quantityLeft < shoppingCartItem.Quantity)
        {
            sciInclTax = unitPriceInclTax * quantityLeft;
            sciExclTax = unitPriceExclTax * quantityLeft;
        }

        if (quantityLeft == 0 || sciInclTax <= 0)
            continue;

        // Apply credit allocation — mutates packNameCredit, populates orderItems
        _subsidyCreditService.ApplyPackNameSubsidyCredit(
            packNameCredit, shoppingCartItem, sciInclTax, sciExclTax, orderItems);
    }

    // Shipping preview
    var shippingCredits = new List<SubsidyOrderShipping>();
    var storeSettings = await _settingService.LoadSettingAsync<MultiStoreSettings>(storeId);

    if (storeSettings.IncludeShippingOnSubsidy)
        await ApplyShippingSubsidyPreviewAsync(cart, customer, storeId, storeSettings, packNameCredits, shippingCredits);

    return new CartSubsidyPreview
    {
        Items = orderItems,
        ShippingCredits = shippingCredits,
    };
}
```

Add the private helper `ApplyShippingSubsidyPreviewAsync`. This mirrors the existing private `GetShippingSubsidyAmount` but calls `GetPackNameShippingRemaining` (which records into the output list) instead of `ProcessSubsidyShipping` (which does not record):

```csharp
/// <summary>
/// Mirrors GetShippingSubsidyAmount but uses GetPackNameShippingRemaining so that
/// per-packname shipping records are captured into <paramref name="shippingCredits"/>.
/// </summary>
private async Task ApplyShippingSubsidyPreviewAsync(
    IList<PackNameShoppingCartItem> cart,
    Customer customer,
    int storeId,
    MultiStoreSettings storeSettings,
    IList<PackNameCredit> packNameCredits,
    IList<SubsidyOrderShipping> shippingCredits)
{
    var shippingPrices = new List<PackNameShippingPrice>();
    var packNameIds = cart.Select(item => item.PackNameId).Distinct().ToList();
    var assignments = await _shippingAssignmentService.LoadAsync(customer, storeId);

    foreach (var packNameId in packNameIds)
    {
        var branchId = cart.FirstOrDefault(x => x.PackNameId == packNameId)?.BranchId ?? 0;
        var shippingOption = assignments.GetSelectedOption(packNameId);
        var packNameAddressId = assignments.GetAddressId(packNameId);

        if (!packNameAddressId.HasValue
            || packNameAddressId.Value == 0
            || !await _shoppingCartService.IsAddressValidForCheckoutAsync(
                    branchId, packNameId, packNameAddressId.Value, customer, storeId, storeSettings))
            continue;

        var existing = shippingPrices.FirstOrDefault(x =>
            x.AddressId == packNameAddressId.Value
            && x.ShippingOption?.Name == shippingOption?.Name);

        if (existing != null)
            existing.PackNameIds.Add(packNameId);
        else
            shippingPrices.Add(new PackNameShippingPrice
            {
                PackNameIds = [packNameId],
                AddressId = packNameAddressId.Value,
                ShippingOption = shippingOption,
            });
    }

    foreach (var shippingPrice in shippingPrices)
    {
        var packNameCart = cart.Where(item => shippingPrice.PackNameIds.Contains(item.PackNameId)).ToList();

        if (await IsFreeShippingAsync(packNameCart.Cast<ShoppingCartItem>().ToList()))
            continue;

        decimal? shippingTotal = null;
        if (shippingPrice.ShippingOption != null)
            (shippingTotal, _) = await AdjustShippingRateAsync(
                shippingPrice.ShippingOption.Rate,
                cart.Cast<ShoppingCartItem>().ToList(),
                shippingPrice.ShippingOption.IsPickupInStore);

        if (!shippingTotal.HasValue || shippingTotal.Value <= decimal.Zero)
            continue;

        if (_shoppingCartSettings.RoundPricesDuringCalculation)
            shippingTotal = await _priceCalculationService.RoundPriceAsync(shippingTotal.Value);

        (var shippingInclTax, _) = await _taxService.GetShippingPriceAsync(shippingTotal.Value, true, customer);
        (var shippingExclTax, _) = await _taxService.GetShippingPriceAsync(shippingTotal.Value, false, customer);

        if (_shoppingCartSettings.RoundPricesDuringCalculation)
        {
            shippingInclTax = await _priceCalculationService.RoundPriceAsync(shippingInclTax);
            shippingExclTax = await _priceCalculationService.RoundPriceAsync(shippingExclTax);
        }

        var shippingPerPackName = shippingInclTax / shippingPrice.PackNameIds.Count;
        var shippingExclPerPackName = shippingExclTax / shippingPrice.PackNameIds.Count;

        foreach (var packNameId in shippingPrice.PackNameIds)
        {
            _subsidyCreditService.GetPackNameShippingRemaining(
                packNameId,
                shippingPerPackName,
                shippingExclPerPackName,
                packNameCredits,
                shippingCredits);
        }
    }
}
```

---

### 4.3 `Models/PackNameCheckout/PackNameShoppingCartModel.cs`

Add `SubsidyBreakdown` to the nested `PackNameShoppingCartItemModel`:

```csharp
// Add using:
using Nop.Plugin.CW.Core.Models.Subsidies;

// Inside public partial record PackNameShoppingCartItemModel:
public IList<SubsidyItemBreakdownModel> SubsidyBreakdown { get; set; } = [];
```

---

### 4.4 `Models/PackNameCheckout/PackNameBoxModel.cs`

Add the shipping subsidy display property:

```csharp
/// <summary>
/// Formatted shipping subsidy amount for this packname, e.g. "−$8.50".
/// Null when no shipping subsidy applies or IncludeShippingOnSubsidy is off.
/// </summary>
public string? ShippingSubsidyDisplay { get; set; }
```

---

### 4.5 `Factories/PackNameCheckoutModelFactory.cs`

#### 4.5.1 Add injected dependencies

In `#region Fields`, add:

```csharp
protected readonly IOrderTotalCalculationOverrideService _orderTotalCalculationOverrideService;
protected readonly ISubsidyService _subsidyService;
```

In the constructor parameter list, add:

```csharp
IOrderTotalCalculationOverrideService orderTotalCalculationOverrideService,
ISubsidyService subsidyService,
```

In the constructor body, add the assignments:

```csharp
_orderTotalCalculationOverrideService = orderTotalCalculationOverrideService;
_subsidyService = subsidyService;
```

Add usings at the top of the file:

```csharp
using Nop.Plugin.CW.Core.Models.Subsidies;
using Nop.Plugin.CW.Core.Services.Orders;
using Nop.Plugin.CW.Core.Services.Subsidies;
```

#### 4.5.2 Add private helper method

Add this helper method anywhere in the class (e.g. in a `#region Utilities` block):

```csharp
/// <summary>
/// Runs the subsidy preview and populates SubsidyBreakdown on every item model,
/// and ShippingSubsidyDisplay on every box model.
/// </summary>
private async Task PopulateSubsidyBreakdownAsync(
    IList<PackNameShoppingCartItem> packNameCart,
    Customer customer,
    int storeId,
    PackNameCheckoutModel model)
{
    // Run the dry-run Apply pass
    var preview = await _orderTotalCalculationOverrideService.PreviewSubsidyBreakdownAsync(
        packNameCart, customer, storeId);

    if (!preview.Items.Any() && !preview.ShippingCredits.Any())
        return;

    // Batch-fetch product groups for icons/colours (only non-null PG IDs)
    var pgIds = preview.Items
        .Where(x => x.ProductGroupId.HasValue)
        .Select(x => x.ProductGroupId!.Value)
        .Distinct()
        .ToList();

    var productGroups = pgIds.Any()
        ? await _subsidyService.GetProductGroupsByIdsAsync(pgIds)
        : [];

    var pgLookup = productGroups.ToDictionary(pg => pg.Id);

    // Map ShoppingCartItemId → list of breakdown rows
    // Note: FormatPriceAsync requires await, so use nested foreach instead of LINQ lambdas
    var itemBreakdowns = new Dictionary<int, List<SubsidyItemBreakdownModel>>();

    foreach (var cartItemGroup in preview.Items.GroupBy(x => x.Id)) // Id == ShoppingCartItemId
    {
        var rows = new List<SubsidyItemBreakdownModel>();

        foreach (var pgGroup in cartItemGroup.GroupBy(x => x.ProductGroupId))
        {
            var pgId = pgGroup.Key;
            var qtyUsed = pgGroup.Sum(x => x.Quantity + x.QuantityTopup);
            var creditUsed = pgId.HasValue
                ? pgGroup.Sum(x => x.ProductGroupCredit + x.ProductGroupCreditTopup)
                : pgGroup.Sum(x => x.Credit + x.CreditTopup);

            if (qtyUsed == 0 && creditUsed == 0)
                continue;

            string? pgName = null, pgIcon = null, pgColour = null;
            if (pgId.HasValue && pgLookup.TryGetValue(pgId.Value, out var pg))
            {
                pgName = pg.Name;
                pgIcon = pg.Icon;
                pgColour = pg.Colour;
            }

            var creditDisplay = creditUsed > 0
                ? await _priceFormatter.FormatPriceAsync(-creditUsed, true, false)
                : null;

            rows.Add(new SubsidyItemBreakdownModel
            {
                ProductGroupId = pgId,
                ProductGroupName = pgName,
                ProductGroupIcon = pgIcon,
                ProductGroupColour = pgColour,
                QuantityUsed = qtyUsed,
                CreditUsed = creditUsed,
                CreditDisplay = creditDisplay,
            });
        }

        if (rows.Count > 0)
            itemBreakdowns[cartItemGroup.Key] = rows;
    }

    // Map PackNameId → shipping subsidy display
    // Note: FormatPriceAsync requires await, so use a foreach instead of ToDictionary
    var shippingByPackName = new Dictionary<int, string?>();
    foreach (var g in preview.ShippingCredits.GroupBy(x => x.PackNameId))
    {
        var total = g.Sum(x => x.Credit + x.CreditTopup);
        shippingByPackName[g.Key] = total > 0
            ? await _priceFormatter.FormatPriceAsync(-total, true, false)
            : null;
    }

    // Push breakdown into item view models
    foreach (var group in model.ShoppingCart.GroupedItems)
    {
        foreach (var row in group.Items)
        {
            if (itemBreakdowns.TryGetValue(row.Item.Id, out var breakdown))
                row.Item.SubsidyBreakdown = breakdown;
        }
    }

    // Push shipping subsidy into box models (via boxCache deduplicated by PackNameId)
    foreach (var group in model.ShoppingCart.GroupedItems)
    {
        foreach (var row in group.Items)
        {
            var packNameId = row.Item.PackNameModel.Id;
            if (row.Box != null
                && shippingByPackName.TryGetValue(packNameId, out var display)
                && display != null
                && row.Box.ShippingSubsidyDisplay == null) // avoid overwriting if already set
            {
                row.Box.ShippingSubsidyDisplay = display;
            }
        }
    }
}
```

**Important:** `_priceFormatter` is already injected as `IPriceFormatter` in the factory. The field name is `_priceFormatter` based on the existing fields list. Verify this before implementing — if the factory uses a differently-named field for price formatting, use that one instead. If `IPriceFormatter` is not already injected in the factory, add it.

#### 4.5.3 Call the helper from `PreparePackNameCheckoutModelItemsAsync`

The current method ends at line ~689 with `return model;`. Add the call immediately before the return:

```csharp
// Populate per-item subsidy breakdown (non-persisted dry run)
var storeId = packNameCart.First().StoreId;
await PopulateSubsidyBreakdownAsync(packNameCart, customer, storeId, model);

return model;
```

**Note:** `storeId` is already assigned earlier in the method. Verify the variable name before implementing — do not redeclare it.

---

### 4.6 `Views/PackNameCheckout/_CartByPackName.cshtml`

After the closing `</div>` of `.cart-product-subtotal` (currently at approximately line 157), add the subsidy breakdown:

```cshtml
@if (item.SubsidyBreakdown.Count > 0)
{
    <div class="cart-item-subsidy-breakdown">
        @foreach (var b in item.SubsidyBreakdown)
        {
            <div class="subsidy-chip @(b.ProductGroupId.HasValue ? "pg-chip" : "global-chip")"
                 title="@(b.ProductGroupId.HasValue ? b.ProductGroupName : T("Plugins.CW.Core.Subsidy.GlobalCredit").Text)">
                @if (b.ProductGroupId.HasValue)
                {
                    @Html.PgIcon(b.ProductGroupIcon, "chip-icon", b.ProductGroupColour)
                }
                else
                {
                    @Html.TigIcon("tig-icon-Globe", "chip-icon")
                }
                @if (b.QuantityUsed > 0)
                {
                    <span class="chip-qty">@b.QuantityUsed×</span>
                }
                @if (!string.IsNullOrEmpty(b.CreditDisplay))
                {
                    <span class="chip-credit">@b.CreditDisplay</span>
                }
            </div>
        }
    </div>
}
```

The placement is inside the `<div class="cart-group-product">` → `<div class="cart-product-info">` → after `.cart-product-subtotal`.

---

### 4.7 `Views/PackNameCheckout/_CartByProduct.cshtml`

Apply the identical block from §4.6 in the same logical position in the product-grouped view. The structure of `_CartByProduct.cshtml` mirrors `_CartByPackName.cshtml` — locate `.cart-product-subtotal` and add the breakdown immediately after it.

---

### 4.8 `Views/PackNameCheckout/_PackNameBoxBody.cshtml`

Add the shipping subsidy line after the PO entry section (which is the first section in this view). The exact insertion point is after the `@if (!string.IsNullOrEmpty(Model.Item.PackNameModel.POEntry))` block.

```cshtml
@if (!string.IsNullOrEmpty(Model.ShippingSubsidyDisplay))
{
    <div class="packname-shipping-subsidy">
        @Html.TigIcon("tig-icon-Globe", "shipping-subsidy-icon")
        <span>@T("Plugins.CW.Core.Subsidy.ShippingCoveredBy")</span>
        <strong>@Model.ShippingSubsidyDisplay</strong>
    </div>
}
```

---

### 4.9 CSS — new rules

Create or extend the cart-specific CSS file. The best location is inside the existing `Content/PackName/` folder. If `shopping-cart.css` or `packname-checkout.css` exists, add these rules to it. If not, add them to whichever CSS file `_CartByPackName.cshtml` or `PackNameCheckout` loads at page render time.

**Rules to add:**

```css
/* ================================================================
   CART ITEM — SUBSIDY BREAKDOWN CHIPS
   Compact pills sitting below cart-product-subtotal
   ================================================================ */

.cart-item-subsidy-breakdown {
    display: flex;
    flex-wrap: wrap;
    gap: 3px;
    margin-top: 3px;
}

.subsidy-chip {
    display: inline-flex;
    align-items: center;
    gap: 2px;
    padding: 0 5px;
    height: 18px;
    border-radius: 9px;
    font-size: 0.68rem;
    font-weight: 600;
    white-space: nowrap;
    line-height: 18px;
}

.subsidy-chip.pg-chip {
    background-color: #f0f4ff;
    color: #2c4a9e;
    border: 1px solid #c5d0f5;
}

.subsidy-chip.global-chip {
    background-color: #e8f5e9;
    color: #1b5e20;
    border: 1px solid #a5d6a7;
}

/* SVG icons rendered via @Html.PgIcon / @Html.TigIcon inside a chip */
.subsidy-chip .pg-icon,
.subsidy-chip .tig-icon {
    width: 11px;
    height: 11px;
    flex-shrink: 0;
    vertical-align: middle;
}

.subsidy-chip .chip-qty {
    color: inherit;
    opacity: 0.8;
}

.subsidy-chip .chip-credit {
    color: inherit;
}

/* ================================================================
   PACKNAME BOX — SHIPPING SUBSIDY LINE
   ================================================================ */

.packname-shipping-subsidy {
    display: flex;
    align-items: center;
    gap: 5px;
    font-size: 0.78rem;
    color: #155724;
    background-color: #d4edda;
    border: 1px solid #c3e6cb;
    border-radius: 4px;
    padding: 3px 8px;
    margin-bottom: 6px;
}

    .packname-shipping-subsidy .shipping-subsidy-icon {
        width: 12px;
        height: 12px;
        flex-shrink: 0;
    }

    .packname-shipping-subsidy strong {
        margin-left: auto;
        font-weight: 700;
    }
```

---

### 4.10 CSS loading

The checkout cart is loaded via AJAX into `#cartItemsContainer`. This means any `NopHtml.AppendCssFileParts` call inside the partial view is ignored (the `<head>` is already rendered).

The CSS must be loaded at **page render time** from the layout or a component that renders on the checkout page itself — for example the `PackNameCheckoutController` layout view or whichever view renders the `_OrderSummary.cshtml` wrapper.

Verify which file handles the page-level CSS for the checkout and add the stylesheet load there:

```csharp
NopHtml.AppendCssFileParts("~/Plugins/CW.Core/Content/PackName/packname-checkout-subsidy.css");
```

Create that CSS file with the rules from §4.9 as a new dedicated file (rather than appending to an existing file) so it stays self-contained.

---

### 4.11 Locale resources

Add these strings in `CWCorePlugin.InstallAsync()` (the `AddOrUpdateLocaleResourceAsync` dictionary) and remove them in `UninstallAsync()`:

```csharp
["Plugins.CW.Core.Subsidy.GlobalCredit"] = "Global subsidy credit",
["Plugins.CW.Core.Subsidy.ShippingCoveredBy"] = "Shipping covered by subsidy:",
```

---

### 4.12 DI registration — `Infrastructure/NopStartup.cs`

`IOrderTotalCalculationOverrideService` is already registered (line 61). No new registration is needed — the factory already injects `IOrderTotalCalculationService`; just change the injected type to `IOrderTotalCalculationOverrideService` for the factory constructor parameter.

`ISubsidyService` — check if it's already registered. Search NopStartup.cs for `ISubsidyService`. If missing, add:

```csharp
services.AddScoped<ISubsidyService, SubsidyService>();
```

---

## 5. Changes Summary

| File | Change |
|---|---|
| `Domain/Subsidies/DTOs/CartSubsidyPreview.cs` | **New** — preview result record |
| `Models/Subsidies/SubsidyItemBreakdownModel.cs` | **New** — view model per breakdown row |
| `Services/Orders/IOrderTotalCalculationOverrideService.cs` | Add `PreviewSubsidyBreakdownAsync` |
| `Services/Orders/OrderTotalCalculationOverrideService.cs` | Implement `PreviewSubsidyBreakdownAsync` + `ApplyShippingSubsidyPreviewAsync` |
| `Models/PackNameCheckout/PackNameShoppingCartModel.cs` | Add `SubsidyBreakdown` to item nested model |
| `Models/PackNameCheckout/PackNameBoxModel.cs` | Add `ShippingSubsidyDisplay` |
| `Factories/PackNameCheckoutModelFactory.cs` | Inject override service + subsidy service; add `PopulateSubsidyBreakdownAsync`; call it before return |
| `Views/PackNameCheckout/_CartByPackName.cshtml` | Add chip block below `.cart-product-subtotal` |
| `Views/PackNameCheckout/_CartByProduct.cshtml` | Same chip block |
| `Views/PackNameCheckout/_PackNameBoxBody.cshtml` | Add shipping subsidy line after PO entry |
| `Content/PackName/packname-checkout-subsidy.css` | **New** — chip + shipping subsidy styles |
| Checkout page layout CSS loader | Append `packname-checkout-subsidy.css` at page render time |
| `CWCorePlugin.cs` | Add 2 locale resource strings |

---

## 6. Important Pre-Implementation Checks

Before writing code, verify these details in the actual files:

1. **`_priceFormatter` field name** in `PackNameCheckoutModelFactory.cs` — confirm the factory already injects `IPriceFormatter` and what field name it uses. If absent, add it.
2. **`storeId` variable availability** at the call site in `PreparePackNameCheckoutModelItemsAsync` — the variable is assigned as `packNameCart.First().StoreId` around line 642. Confirm it is still in scope when you add the `PopulateSubsidyBreakdownAsync` call.
3. **CSS load location** for the checkout page — trace how `PackNameCheckout/Index.cshtml` (or whichever entry view) loads its stylesheets, and add the new file there.
4. **`ISubsidyService` registration** — confirm or add as described in §4.12.
5. **`ProductGroup.Icon` field type** — the spec assumes it holds a FontAwesome class string (e.g. `"fas fa-tshirt"`). Confirm in `Domain/Subsidies/ProductGroup.cs`. If `Icon` stores something different (e.g. a file name), adjust the view markup accordingly.
6. **Constructor parameter order** in `PackNameCheckoutModelFactory` — add new parameters at the end of the constructor to avoid breaking existing DI registrations.

---

## 7. Behaviour Notes

- If a packname has no active subsidy, `PreviewSubsidyBreakdownAsync` returns empty lists and nothing is shown — no visual change.
- If a cart item's full cost is covered by quantity allocation only (zero remaining credit to apply), `SubsidyBreakdown` will have one row with `QuantityUsed > 0` and `CreditDisplay = null`.
- Shipping subsidy is only computed when `storeSettings.IncludeShippingOnSubsidy = true`.
- The breakdown runs every time the cart AJAX partial reloads. It uses cached credits (`GetPackNamesSubsidyCreditAsync` is cache-backed), so it should be fast.
- The preview clones credits before mutating them (`GetPackNamesSubsidyCreditAsync` already returns clones). It does not affect the actual cache or any persisted state.
- If a packname has items in multiple subsidies, both subsidies contribute to the breakdown rows. The grouping by `ProductGroupId` across subsidies collapses them into one visual row per product group.
