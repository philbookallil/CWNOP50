# Subsidy Catalog — Public Store Implementation Spec

> **4.9 history.** Copied from the 4.90 source (`NOP49/CWPlans/`) on 2026-10-31, when CW.Core moved to nopCommerce 5.00. It is kept as the record of the 4.9 work and is **not updated for 5.00**: file paths, line numbers and versions are 4.9's. For how CW.Core works now, read the references in `CWPlans/` and `CWPlans/CWCore_Port_Reference.md`.

**Plugin:** `Nop.Plugin.CW.Core`  
**Status:** Design spec — ready for implementation  
**Last revised:** 2026-06-30

---

## 0. Background & Goals

This feature adds a public-store **Subsidy Catalog** page. When a packname with active subsidies is in context, the page shows:

- The product groups assigned to those subsidies, each with its products
- A **Subsidy Bar** (persistent, refreshable component) showing real-time credit/quantity remaining per product group, and the global subsidy credit

Design principles: no external plugins (no SevenSpikes, no Kendo UI), no Dapper/EF, no direct DB queries, Bootstrap 4.6 + jQuery only.

---

## 1. Terminology Map

| Old (Jim2 / Allowance) | New (TIG49 / Subsidy) |
|---|---|
| Allowance | Subsidy |
| AllowanceCredit / AllowanceCredits | SubsidyCredit / SubsidyCredits |
| ProductGroupCredit | ProductGroupCredit |
| PackNameCredit | PackNameCredit |
| AllowanceCatalogController | SubsidyCatalogController |
| IAllowancesShoppingCartService | ISubsidyCatalogCartService (new) |
| GetPackNameUsedAllowancesAsync | GetPackNameSubsidyCartUsageAsync (new) |

---

## 2. Credit Consumption Order

When a cart item is processed, credit is consumed in three **cross-subsidy passes**. All quantity is consumed before any credit is touched, across every subsidy the packname has:

**Pass 1 — Product-group quantity (all subsidies)**
1. Product-group quantity base (`ProductGroupCredit.AvailableQuantity`) — every subsidy
2. Product-group quantity topup (`ProductGroupCredit.AvailableQuantityTopup`) — every subsidy

**Pass 2 — Product-group credit (all subsidies)**
3. Product-group credit base (`ProductGroupCredit.AvailableCredit`) — every subsidy
4. Product-group credit topup (`ProductGroupCredit.AvailableCreditTopup`) — every subsidy

**Pass 3 — Global subsidy credit (all subsidies)**
5. Global subsidy credit base (`SubsidyCredit.AvailableCredit`) — every subsidy
6. Global subsidy credit topup (`SubsidyCredit.AvailableCreditTopup`) — every subsidy

**Why three passes instead of per-subsidy loops:** If a product belongs to a product group in Subsidy A and another product group in Subsidy B, the correct behaviour is to drain all available quantity from both subsidies before any credit is consumed. Processing one subsidy end-to-end before moving to the next would incorrectly consume credit from Subsidy A before checking whether Subsidy B still has quantity remaining.

**Key rule — `OnlyAllocatedProducts`:** If `SubsidyCredit.OnlyAllocatedProducts = true` and the cart item's product is not in any of that subsidy's product groups, pass 3 is skipped for that subsidy (the product cannot use global credit from it).

**Key rule — `RestrictToAllocatedQty`:** If `SubsidyCredit.RestrictToAllocatedQty = true`, the packname cannot order more qty than the product-group allows (enforced separately at add-to-cart time, not in this catalog view).

**DB views reflect placed orders only** — `SubsidyPackNameCredit` and `ProductGroupPackNameCredit` (which back `GetPackNameSubsidyCreditAsync`) do **not** include current cart items. The cart simulation layer (§4) bridges this gap.

---

## 3. Architecture Overview

```
SubsidyCatalogController
    └── ISubsidyCatalogModelFactory
            ├── ISubsidyCatalogCartService       ← new: cart simulation
            ├── ISubsidyCreditService             ← existing
            ├── ISubsidyOrderingService           ← existing
            ├── IPackNameContext                   ← existing
            ├── IProductModelFactory (NOP)        ← existing
            └── IPriceCalculationService (NOP)    ← existing (for item prices)

SubsidyBarViewComponent
    └── ISubsidyCatalogModelFactory.PrepareSubsidyBarModelAsync
```

---

## 4. SubsidyCartUsageItem DTO & Cart Service

### 4.1 `SubsidyCartUsageItem` DTO

```csharp
// Location: Domain/Subsidies/DTOs/SubsidyCartUsageItem.cs
namespace Nop.Plugin.CW.Core.Domain.Subsidies.DTOs;

/// <summary>
/// Represents how much of a subsidy budget a single cart item consumed,
/// broken down by product group and credit bucket.
/// ProductGroupId == null → global subsidy credit (steps 5–6).
/// ProductGroupId != null → product-group credit/quantity (steps 1–4).
/// </summary>
public class SubsidyCartUsageItem
{
    /// <summary>Null = global subsidy-level credit; non-null = product-group-level usage.</summary>
    public int? ProductGroupId { get; set; }

    // Quantity buckets (product-group level only, steps 1–2)
    public int Quantity { get; set; }       // consumed from pg qty base
    public int QuantityTopup { get; set; }  // consumed from pg qty topup

    // Product-group credit buckets (product-group level only, steps 3–4)
    public decimal ProductGroupCredit { get; set; }       // consumed from pg credit base
    public decimal ProductGroupCreditTopup { get; set; }  // consumed from pg credit topup

    // Global credit buckets (ProductGroupId == null, steps 5–6)
    public decimal Credit { get; set; }       // consumed from global subsidy credit base
    public decimal CreditTopup { get; set; }  // consumed from global subsidy credit topup
}
```

### 4.2 `ISubsidyCatalogCartService`

```csharp
// Location: Services/Subsidies/ISubsidyCatalogCartService.cs
namespace Nop.Plugin.CW.Core.Services.Subsidies;

public interface ISubsidyCatalogCartService
{
    /// <summary>
    /// Simulates credit consumption for all current cart items of the given packname
    /// and returns a flat list of per-product-group and global usage records.
    /// Call this once per page render; results are aggregated by the model factory.
    /// </summary>
    Task<IList<SubsidyCartUsageItem>> GetPackNameSubsidyCartUsageAsync(int packNameId, int storeId);
}
```

### 4.3 `SubsidyCatalogCartService` — Implementation

```csharp
// Location: Services/Subsidies/SubsidyCatalogCartService.cs
namespace Nop.Plugin.CW.Core.Services.Subsidies;

public class SubsidyCatalogCartService : ISubsidyCatalogCartService
{
    #region Fields

    protected readonly IRepository<PackNameShoppingCartItem> _packNameCartItemRepository;
    protected readonly ISubsidyCreditService _subsidyCreditService;
    protected readonly IPriceCalculationService _priceCalculationService;
    protected readonly IProductService _productService;
    protected readonly ITaxService _taxService;
    protected readonly IWorkContext _workContext;

    #endregion

    #region Ctor

    public SubsidyCatalogCartService(
        IRepository<PackNameShoppingCartItem> packNameCartItemRepository,
        ISubsidyCreditService subsidyCreditService,
        IPriceCalculationService priceCalculationService,
        IProductService productService,
        ITaxService taxService,
        IWorkContext workContext)
    {
        _packNameCartItemRepository = packNameCartItemRepository;
        _subsidyCreditService = subsidyCreditService;
        _priceCalculationService = priceCalculationService;
        _productService = productService;
        _taxService = taxService;
        _workContext = workContext;
    }

    #endregion

    #region Methods

    public async Task<IList<SubsidyCartUsageItem>> GetPackNameSubsidyCartUsageAsync(int packNameId, int storeId)
    {
        // 1. Get cart items for this packname
        var cartItems = await _packNameCartItemRepository.GetAllAsync(q =>
            q.Where(x => x.PackNameId == packNameId && x.StoreId == storeId));

        if (!cartItems.Any())
            return Array.Empty<SubsidyCartUsageItem>();

        // 2. Get subsidy credit snapshot (reflects placed orders only; NOT current cart items)
        var packNameCredit = await _subsidyCreditService.GetPackNameSubsidyCreditAsync(packNameId);
        if (packNameCredit == null || !packNameCredit.SubsidyCredits.Any())
            return Array.Empty<SubsidyCartUsageItem>();

        // 3. Clone — mutate the clone to simulate consumption without corrupting the cached snapshot
        var mutableCredit = (PackNameCredit)packNameCredit.Clone();

        var usageItems = new List<SubsidyCartUsageItem>();
        var customer = await _workContext.GetCurrentCustomerAsync();

        // 4. Process each cart item through the consumption chain in three cross-subsidy passes.
        //    All quantity (across every subsidy) is consumed before any credit is touched.
        //    This prevents credit from being consumed in Subsidy A while Subsidy B still has
        //    quantity remaining for the same product.
        foreach (var cartItem in cartItems)
        {
            var qty = cartItem.Quantity;
            var product = await _productService.GetProductByIdAsync(cartItem.ProductId);
            if (product == null || product.Deleted)
                continue;

            // Get line price (unit price × qty, incl and excl tax)
            var (linePriceInclTax, linePriceExclTax) = await GetCartItemLinePriceAsync(cartItem, product, customer);

            var remainingQty = qty;
            var remainingCostInclTax = linePriceInclTax;
            var remainingCostExclTax = linePriceExclTax;

            // ================================================================
            // PASS 1: Product-group quantity (steps 1 & 2) — across ALL subsidies
            // ================================================================
            foreach (var subsidyCredit in mutableCredit.SubsidyCredits)
            {
                if (remainingQty <= 0)
                    break;

                foreach (var pgCredit in subsidyCredit.ProductGroupCredits
                    .Where(pg => pg.ProductIds.Contains(cartItem.ProductId)))
                {
                    if (remainingQty <= 0)
                        break;

                    // Step 1: pg quantity base
                    var consumedQtyBase = Math.Min(remainingQty, pgCredit.AvailableQuantity);
                    pgCredit.AvailableQuantity -= consumedQtyBase;
                    remainingQty -= consumedQtyBase;

                    // Step 2: pg quantity topup
                    var consumedQtyTopup = Math.Min(remainingQty, pgCredit.AvailableQuantityTopup);
                    pgCredit.AvailableQuantityTopup -= consumedQtyTopup;
                    remainingQty -= consumedQtyTopup;

                    if (consumedQtyBase > 0 || consumedQtyTopup > 0)
                    {
                        usageItems.Add(new SubsidyCartUsageItem
                        {
                            ProductGroupId = pgCredit.ProductGroupId,
                            Quantity = consumedQtyBase,
                            QuantityTopup = consumedQtyTopup
                        });
                    }
                }
            }

            // ================================================================
            // PASS 2: Product-group credit (steps 3 & 4) — across ALL subsidies
            // ================================================================
            foreach (var subsidyCredit in mutableCredit.SubsidyCredits)
            {
                if (remainingCostInclTax <= 0)
                    break;

                foreach (var pgCredit in subsidyCredit.ProductGroupCredits
                    .Where(pg => pg.ProductIds.Contains(cartItem.ProductId)))
                {
                    if (remainingCostInclTax <= 0)
                        break;

                    var pgUsage = new SubsidyCartUsageItem { ProductGroupId = pgCredit.ProductGroupId };

                    // Step 3: pg credit base
                    if (pgCredit.AvailableCredit > 0)
                    {
                        var consumed = Math.Min(remainingCostInclTax, pgCredit.AvailableCredit);
                        var consumedExcl = linePriceInclTax > 0
                            ? consumed * linePriceExclTax / linePriceInclTax
                            : consumed;
                        pgCredit.AvailableCredit -= consumed;
                        pgUsage.ProductGroupCredit = consumed;
                        remainingCostInclTax -= consumed;
                        remainingCostExclTax -= consumedExcl;
                    }

                    // Step 4: pg credit topup
                    if (remainingCostInclTax > 0 && pgCredit.AvailableCreditTopup > 0)
                    {
                        var consumed = Math.Min(remainingCostInclTax, pgCredit.AvailableCreditTopup);
                        var consumedExcl = linePriceInclTax > 0
                            ? consumed * linePriceExclTax / linePriceInclTax
                            : consumed;
                        pgCredit.AvailableCreditTopup -= consumed;
                        pgUsage.ProductGroupCreditTopup = consumed;
                        remainingCostInclTax -= consumed;
                        remainingCostExclTax -= consumedExcl;
                    }

                    if (pgUsage.ProductGroupCredit > 0 || pgUsage.ProductGroupCreditTopup > 0)
                        usageItems.Add(pgUsage);
                }
            }

            // ================================================================
            // PASS 3: Global subsidy credit (steps 5 & 6) — across ALL subsidies
            // ================================================================
            foreach (var subsidyCredit in mutableCredit.SubsidyCredits)
            {
                if (remainingCostInclTax <= 0)
                    break;

                // Skip if OnlyAllocatedProducts and product is not in any pg of this subsidy
                var productInAnyPg = subsidyCredit.ProductGroupCredits
                    .Any(pg => pg.ProductIds.Contains(cartItem.ProductId));
                if (subsidyCredit.OnlyAllocatedProducts && !productInAnyPg)
                    continue;

                var globalUsage = new SubsidyCartUsageItem { ProductGroupId = null };

                // Step 5: global credit base
                if (subsidyCredit.AvailableCredit > 0)
                {
                    var consumed = Math.Min(remainingCostInclTax, subsidyCredit.AvailableCredit);
                    subsidyCredit.AvailableCredit -= consumed;
                    globalUsage.Credit = consumed;
                    remainingCostInclTax -= consumed;
                }

                // Step 6: global credit topup
                if (remainingCostInclTax > 0 && subsidyCredit.AvailableCreditTopup > 0)
                {
                    var consumed = Math.Min(remainingCostInclTax, subsidyCredit.AvailableCreditTopup);
                    subsidyCredit.AvailableCreditTopup -= consumed;
                    globalUsage.CreditTopup = consumed;
                    remainingCostInclTax -= consumed;
                }

                if (globalUsage.Credit > 0 || globalUsage.CreditTopup > 0)
                    usageItems.Add(globalUsage);
            }
        }

        return usageItems;
    }

    #endregion

    #region Utilities

    /// <summary>
    /// Returns the line price (unit price × qty) incl. and excl. tax for a cart item.
    /// Uses the same price resolution as the checkout flow.
    /// </summary>
    private async Task<(decimal inclTax, decimal exclTax)> GetCartItemLinePriceAsync(
        PackNameShoppingCartItem cartItem,
        Nop.Core.Domain.Catalog.Product product,
        Nop.Core.Domain.Customers.Customer customer)
    {
        // NOTE: Verify GetUnitPriceAsync signature in this NOP 4.9 build.
        // If it accepts ShoppingCartItem directly, prefer it — it handles attribute pricing.
        // Otherwise use GetFinalPriceAsync as shown here.
        var (unitPriceExclTax, _) = await _priceCalculationService.GetFinalPriceAsync(
            product: product,
            customer: customer,
            additionalCharge: 0,
            includeDiscounts: true,
            quantity: cartItem.Quantity);

        (var unitPriceInclTax, _) = await _taxService.GetProductPriceAsync(product, unitPriceExclTax, includingTax: true, customer: customer);
        (unitPriceExclTax, _)     = await _taxService.GetProductPriceAsync(product, unitPriceExclTax, includingTax: false, customer: customer);

        return (unitPriceInclTax * cartItem.Quantity, unitPriceExclTax * cartItem.Quantity);
    }

    #endregion
}
```

> **Note on `SubsidyCredits` vs `AllowanceCredits`:** In TIG49, `PackNameCredit` uses `SubsidyCredits` (a list of `SubsidyCredit`). Verify the exact property name in `Domain/Subsidies/DTOs/PackNameCredit.cs` and adjust accordingly.

> **Note on price calculation:** The implementation above uses `IPriceCalculationService.GetFinalPriceAsync`. If `IShoppingCartService.GetUnitPriceAsync` is available in this NOP build and handles shopping cart item pricing (including attribute combinations), prefer it. The goal is consistency with what `ApplyPackNameSubsidyCredit` receives at checkout.

---

## 5. Models

### 5.1 `SubsidyCatalogModel`

```csharp
// Location: Models/Subsidies/SubsidyCatalogModel.cs
public record SubsidyCatalogModel : BaseNopModel
{
    public SubsidyCatalogModel()
    {
        ProductGroups = new List<SubsidyCatalogProductGroupModel>();
        AvailableViewModes = new List<SelectListItem>();
    }

    public IList<SubsidyCatalogProductGroupModel> ProductGroups { get; set; }
    public string ViewMode { get; set; }  // "grid" or "list"
    public IList<SelectListItem> AvailableViewModes { get; set; }
}
```

### 5.2 `SubsidyCatalogProductGroupModel`

```csharp
// Location: Models/Subsidies/SubsidyCatalogProductGroupModel.cs
public record SubsidyCatalogProductGroupModel : BaseNopModel
{
    public SubsidyCatalogProductGroupModel()
    {
        Products = new List<ProductOverviewModel>();
    }

    public int Id { get; set; }                // ProductGroupId
    public string Name { get; set; }
    public string Icon { get; set; }           // CSS icon class
    public string Colour { get; set; }         // hex colour
    public int DisplayOrder { get; set; }

    // Quantities (from DB views = placed orders only; net of placed orders)
    public int TotalQuantity { get; set; }           // base + topup allocated
    public int AvailableQuantity { get; set; }       // base + topup remaining after placed orders

    // Credits (from DB views = placed orders only)
    public decimal AvailableCredit { get; set; }     // base + topup remaining after placed orders
    public decimal AllocatedGlobalCredit { get; set; } // total global credit across all subsidies (for "is exhausted" logic)

    // Cart simulation results
    public int ShoppingCartQuantity { get; set; }    // qty in cart consuming this pg
    public decimal ShoppingCartCredit { get; set; }  // pg credit consumed by cart

    // Derived display values
    public decimal RemainCredit { get; set; }        // AvailableCredit - ShoppingCartCredit
    public string RemainCreditFormatted { get; set; }
    public string ShoppingCartCreditFormatted { get; set; }

    // Products to display
    public string ViewMode { get; set; }
    public IList<ProductOverviewModel> Products { get; set; }
}
```

### 5.3 `SubsidyBarModel`

```csharp
// Location: Models/Subsidies/SubsidyBarModel.cs
public record SubsidyBarModel : BaseNopModel
{
    public SubsidyBarModel()
    {
        ProductGroups = new List<SubsidyCatalogProductGroupModel>();
    }

    // PackName identity fields (displayed in bar header)
    public string PackNameName { get; set; }
    public string PackNameColour { get; set; }
    public string PackNameBranch { get; set; }
    public string PackNameDepartment { get; set; }
    public string PackNameCostCenter { get; set; }
    public string PackNamePOEntry { get; set; }
    public bool PackNameIsGroup { get; set; }   // PackName.Group — renders packname-group icon
    public bool PackNameIsManager { get; set; } // PackName.Manager — used for UI logic (topup, etc.)
    public bool PackNameIsCustomer { get; set; } // PackName.IsCustomer — determines icon state when not a group

    // Global credit totals
    public decimal TotalCredit { get; set; }        // total allocated global credit (Credit + CreditTopup)
    public decimal AvailableCredit { get; set; }    // remaining after placed orders
    public decimal RemainCredit { get; set; }       // AvailableCredit - ShoppingCartCredit
    public decimal ShoppingCartCredit { get; set; } // global credit consumed by cart items
    public string RemainCreditFormatted { get; set; }
    public string ShoppingCartCreditFormatted { get; set; }

    // Per-product-group display (same model reused)
    public IList<SubsidyCatalogProductGroupModel> ProductGroups { get; set; }

    // Whether the manager can edit topup from this bar instance
    public bool EditSubsidyTopup { get; set; }
}
```

---

## 6. Model Factory

### 6.1 Interface

```csharp
// Location: Factories/ISubsidyCatalogModelFactory.cs
namespace Nop.Plugin.CW.Core.Factories;

public interface ISubsidyCatalogModelFactory
{
    Task<SubsidyCatalogModel> PrepareSubsidyCatalogModelAsync(PackName packName, string viewMode);
    Task<SubsidyBarModel> PrepareSubsidyBarModelAsync(PackName packName, bool flyout);

    /// <summary>Returns updated header data for a single product group (used by AJAX header refresh).</summary>
    Task<SubsidyCatalogProductGroupModel> PrepareProductGroupHeaderModelAsync(PackName packName, int productGroupId);
}
```

### 6.2 `PrepareSubsidyCatalogModelAsync` — Step-by-Step Algorithm

```
INPUTS: packName, viewMode ("grid" or "list")

STEP 1 — Get PackNameCredit snapshot
  var packNameCredit = await _subsidyCreditService.GetPackNameSubsidyCreditAsync(packName.Id)
  if packNameCredit is null: packNameCredit = new PackNameCredit()

STEP 2 — Inject OrderOutsidePeriod subsidies
  (Subsidies that are outside ordering period but still allow ordering via their flag)

  var allSubsidyPackNames = await _subsidyOrderingService.GetAllSubsidyPackNamesByPackNameIdAsync(packName.Id)
    // or equivalent method on ISubsidyService

  For each subsidyPackName where its subsidy is NOT already in packNameCredit.SubsidyCredits:
    var subsidy = await _subsidyService.GetSubsidyByIdAsync(subsidyPackName.SubsidyId)

    if subsidy.OrderOutsidePeriod == true && subsidy.Active:
      var productGroups = await _subsidyService.GetSubsidyProductGroupsBySubsidyIdAsync(subsidy.Id)
      
      if productGroups.Any():
        // Build a zero-credit SubsidyCredit entry so these product groups still appear in the catalog
        var subsidyCredit = new SubsidyCredit
        {
            SubsidyId = subsidy.Id,
            AvailableCredit = 0, AvailableCreditTopup = 0,
            Credit = 0, CreditTopup = 0,
            OnlyAllocatedProducts = subsidy.OnlyAllocatedProducts && (subsidy.IncludeManagers || !packName.Manager),
            RestrictToAllocatedQty = subsidy.RestrictToAllocatedQty && (subsidy.IncludeManagers || !packName.Manager),
        }
        
        For each subsidyProductGroup in productGroups:
          var productGroup = await _subsidyService.GetProductGroupByIdAsync(subsidyProductGroup.ProductGroupId)
          var productIds = (await _subsidyService.GetProductsByProductGroupIdAsync(productGroup.Id))
                              .Select(x => x.ProductId).ToList()
          
          subsidyCredit.ProductGroupCredits.Add(new ProductGroupCredit
          {
              ProductGroupId = productGroup.Id,
              Name = productGroup.Name, DisplayOrder = subsidyProductGroup.DisplayOrder,
              AvailableCredit = 0, AvailableCreditTopup = 0,
              AvailableQuantity = 0, AvailableQuantityTopup = 0,
              Credit = 0, CreditTopup = 0, Quantity = 0, QuantityTopup = 0,
              ProductIds = productIds
          })
        
        packNameCredit.SubsidyCredits.Add(subsidyCredit)

STEP 3 — Guard: if no subsidy credits at all, return empty model

STEP 4 — Get cart simulation results
  var cartUsage = await _catalogCartService.GetPackNameSubsidyCartUsageAsync(packName.Id, store.Id)

STEP 5 — Gender filter (optional)
  var settings = await _settingService.LoadSettingAsync<MultiStoreSettings>(store.Id)
  
  IList<int> validGenderSpecOptions = empty list  // empty = no filter applied
  
  if settings.GenderEnabled && (packName.Gender == "M" || packName.Gender == "F"):
    var genderAttribute = await _specificationAttributeService.GetSpecificationAttributeByNameAsync("Gender")
    if genderAttribute != null:
      var allOptions = await _specificationAttributeService
                            .GetSpecificationAttributeOptionsBySpecificationAttributeAsync(genderAttribute.Id)
      if packName.Gender == "M":
        validGenderSpecOptions = allOptions.Where(o => mens/boys/unisex names).Select(o => o.Id).ToList()
      else:
        validGenderSpecOptions = allOptions.Where(o => womens/girls/unisex names).Select(o => o.Id).ToList()

STEP 6 — Compute allocated global credit (for "is exhausted" logic in view)
  var allocatedGlobalCredit = packNameCredit.SubsidyCredits.Sum(x => x.Credit + x.CreditTopup)

STEP 7 — Build model.ProductGroups

  For each subsidyCredit in packNameCredit.SubsidyCredits:
    For each productGroupCredit in subsidyCredit.ProductGroupCredits:
    
      if productGroupCredit.ProductIds is empty → continue (skip; no products in this group)
      
      var existing = model.ProductGroups.FirstOrDefault(x => x.Id == productGroupCredit.ProductGroupId)
      
      if existing is null (first time we see this product group):
        // Load and filter products
        var products = await SearchSubsidyProductsAsync(
            productIds: productGroupCredit.ProductIds,
            storeId: store.Id,
            genderSpecOptionIds: validGenderSpecOptions)
        
        if products is empty → continue (no visible products = don't show the group)
        
        // Aggregate cart usage for this product group
        var usedPg = cartUsage.Where(x => x.ProductGroupId == productGroupCredit.ProductGroupId)
        
        // Build the model
        var pgModel = new SubsidyCatalogProductGroupModel
        {
            Id                   = productGroupCredit.ProductGroupId,
            Name                 = productGroup.Name,      // load via IProductGroupService/ISubsidyService
            Icon                 = productGroup.Icon,
            Colour               = productGroup.Colour,
            DisplayOrder         = productGroupCredit.DisplayOrder,
            AllocatedGlobalCredit = allocatedGlobalCredit,
            TotalQuantity        = productGroupCredit.Quantity + productGroupCredit.QuantityTopup,
            AvailableQuantity    = productGroupCredit.AvailableQuantity + productGroupCredit.AvailableQuantityTopup,
            AvailableCredit      = productGroupCredit.AvailableCredit + productGroupCredit.AvailableCreditTopup,
            ShoppingCartQuantity  = usedPg.Sum(x => x.Quantity + x.QuantityTopup),
            ShoppingCartCredit    = usedPg.Sum(x => x.ProductGroupCredit + x.ProductGroupCreditTopup),
            ViewMode             = viewMode,
            Products             = (await _productModelFactory.PrepareProductOverviewModelsAsync(products)).ToList()
        }
        
        pgModel.RemainCredit = pgModel.AvailableCredit - pgModel.ShoppingCartCredit
        if pgModel.RemainCredit > 0: format RemainCreditFormatted
        if pgModel.ShoppingCartCredit > 0: format ShoppingCartCreditFormatted
        
        model.ProductGroups.Add(pgModel)
      
      else (product group already added — a second subsidy also contains it; accumulate):
        existing.TotalQuantity       += productGroupCredit.Quantity + productGroupCredit.QuantityTopup
        existing.AvailableQuantity   += productGroupCredit.AvailableQuantity + productGroupCredit.AvailableQuantityTopup
        existing.AvailableCredit     += productGroupCredit.AvailableCredit + productGroupCredit.AvailableCreditTopup
        existing.RemainCredit         = existing.AvailableCredit - existing.ShoppingCartCredit
        Reformat RemainCreditFormatted

STEP 8 — Sort model.ProductGroups by DisplayOrder

STEP 9 — Attach view mode + build AvailableViewModes
```

### 6.3 `PrepareSubsidyBarModelAsync` — Step-by-Step Algorithm

```
INPUTS: packName, flyout (bool — suppresses topup button in flyout/header mode)

STEP 1 — Resolve dependencies
  var customer = await _workContext.GetCurrentCustomerAsync()
  var manager = await _packNameService.GetPackNameByCustomerIdAsync(customer.Id)
  var settings = await _settingService.LoadSettingAsync<MultiStoreSettings>(store.Id)

STEP 2 — Build identity fields
  model.PackNameName       = packName.Name
  model.PackNameColour     = packName.Colour
  model.PackNameBranch     = (load branch name from IBranchService if needed)
  model.PackNameDepartment = (from packName.DepartmentId if applicable)
  model.PackNameCostCenter = (from packName.CostCenterId if applicable)
  model.PackNamePOEntry    = packName.POEntry
  model.PackNameIsGroup    = packName.Group
  model.PackNameIsManager  = packName.Manager
  model.PackNameIsCustomer = packName.IsCustomer  // CustomerId.HasValue && CustomerId > 0
  model.EditSubsidyTopup   = !flyout
                              && settings.AllowEditSubsidyTopup
                              && settings.AllowManagersToAddPackNames
                              && (manager?.Manager ?? false)

STEP 3 — Get PackNameCredit snapshot
  var packNameCredit = await _subsidyCreditService.GetPackNameSubsidyCreditAsync(packName.Id)
  if null or empty: format model.RemainCreditFormatted = "$0.00"; return model (bar renders with zeros)

STEP 4 — Get cart simulation results
  var cartUsage = await _catalogCartService.GetPackNameSubsidyCartUsageAsync(packName.Id, store.Id)

STEP 5 — Compute global credit totals
  model.TotalCredit        = packNameCredit.SubsidyCredits.Sum(x => x.Credit + x.CreditTopup)
  model.ShoppingCartCredit = cartUsage.Where(x => x.ProductGroupId == null).Sum(x => x.Credit + x.CreditTopup)
  if model.ShoppingCartCredit > 0: format ShoppingCartCreditFormatted

STEP 6 — Iterate subsidy credits, accumulate AvailableCredit and build per-pg rows

  For each subsidyCredit in packNameCredit.SubsidyCredits:
    model.AvailableCredit += subsidyCredit.AvailableCredit + subsidyCredit.AvailableCreditTopup
    model.RemainCredit     = model.AvailableCredit - model.ShoppingCartCredit
    Reformat model.RemainCreditFormatted
    
    For each productGroupCredit in subsidyCredit.ProductGroupCredits where ProductIds.Any():
      var existing = model.ProductGroups.FirstOrDefault(x => x.Id == productGroupCredit.ProductGroupId)
      
      if existing is null:
        var usedPg = cartUsage.Where(x => x.ProductGroupId == productGroupCredit.ProductGroupId)
        
        var pgModel = new SubsidyCatalogProductGroupModel
        {
            Id                  = productGroupCredit.ProductGroupId,
            Name/Icon/Colour    = (load productGroup entity),
            DisplayOrder        = productGroupCredit.DisplayOrder,
            TotalQuantity       = productGroupCredit.Quantity + productGroupCredit.QuantityTopup,
            AvailableQuantity   = productGroupCredit.AvailableQuantity + productGroupCredit.AvailableQuantityTopup,
            AvailableCredit     = productGroupCredit.AvailableCredit + productGroupCredit.AvailableCreditTopup,
            ShoppingCartQuantity = usedPg.Sum(x => x.Quantity + x.QuantityTopup),
            ShoppingCartCredit   = usedPg.Sum(x => x.ProductGroupCredit + x.ProductGroupCreditTopup),
            // NOTE: No Products list — the bar does not load product catalogs
        }
        pgModel.RemainCredit = pgModel.AvailableCredit - pgModel.ShoppingCartCredit
        Format RemainCreditFormatted and ShoppingCartCreditFormatted as needed
        model.ProductGroups.Add(pgModel)
      
      else (accumulate):
        Same accumulation logic as §6.2

STEP 7 — Sort model.ProductGroups by DisplayOrder
```

### 6.4 `PrepareProductGroupHeaderModelAsync`

Used by the `ProductGroupHeaders` AJAX endpoint to refresh individual group headers after cart changes:

```
1. var packNameCredit = await _subsidyCreditService.GetPackNameSubsidyCreditAsync(packName.Id)
2. var cartUsage = await _catalogCartService.GetPackNameSubsidyCartUsageAsync(packName.Id, store.Id)
3. Find all ProductGroupCredit entries across SubsidyCredits where ProductGroupId == productGroupId
4. Accumulate into a single SubsidyCatalogProductGroupModel (same accumulation logic)
5. Return the model (controller renders _ProductGroupHeader partial)
```

---

## 7. Product Search Helper

The model factory needs a private method to query products filtered by a list of IDs and optionally by gender specification attribute options.

```csharp
private async Task<IList<Product>> SearchSubsidyProductsAsync(
    IList<int> productIds,
    int storeId,
    IList<int> genderSpecOptionIds)
{
    // Use IProductService.SearchProductsAsync if it supports:
    //   - filteredSpecOptions (list of SpecificationAttributeOption IDs)
    //   - productIds filtering
    // NOP 4.9 SearchProductsAsync accepts these parameters.
    // If gender filter is empty, pass null / empty for spec options.
    
    return await _productService.SearchProductsAsync(
        storeId: storeId,
        productIds: productIds.ToArray(),
        filteredSpecOptions: genderSpecOptionIds.Any() ? genderSpecOptionIds : null,
        visibleIndividuallyOnly: true,
        orderBy: ProductSortingEnum.Position);
}
```

---

## 8. Controller

```csharp
// Location: Controllers/SubsidyCatalogController.cs
namespace Nop.Plugin.CW.Core.Controllers;

[AutoValidateAntiforgeryToken]
public class SubsidyCatalogController : BasePluginController
{
    private readonly IPackNameContext _packNameContext;
    private readonly ISubsidyCatalogModelFactory _catalogModelFactory;
    private readonly IWorkContext _workContext;
    private readonly IStoreContext _storeContext;

    // GET /SubsidyCatalog  (route: "Plugin.CW.Core.SubsidyCatalog")
    public async Task<IActionResult> Index(string viewMode = "grid")
    {
        var customer = await _workContext.GetCurrentCustomerAsync();
        var packName = await _packNameContext.GetCurrentPackNameAsync(customer);

        if (packName == null)
            return RedirectToRoute("Homepage");

        var model = await _catalogModelFactory.PrepareSubsidyCatalogModelAsync(packName, viewMode);
        return View("~/Plugins/CW.Core/Views/SubsidyCatalog/Index.cshtml", model);
    }

    // POST /SubsidyCatalog/Items  (AJAX — reloads product groups partial)
    [HttpPost]
    public async Task<IActionResult> Items(string viewMode = "grid")
    {
        var customer = await _workContext.GetCurrentCustomerAsync();
        var packName = await _packNameContext.GetCurrentPackNameAsync(customer);

        if (packName == null)
            return Content(string.Empty);

        var model = await _catalogModelFactory.PrepareSubsidyCatalogModelAsync(packName, viewMode);
        return PartialView("~/Plugins/CW.Core/Views/SubsidyCatalog/_ProductGroups.cshtml", model);
    }

    // GET /SubsidyCatalog/Bar  (AJAX — reloads subsidy bar)
    public async Task<IActionResult> Bar()
    {
        var customer = await _workContext.GetCurrentCustomerAsync();
        var packName = await _packNameContext.GetCurrentPackNameAsync(customer);

        if (packName == null)
            return Content(string.Empty);

        var model = await _catalogModelFactory.PrepareSubsidyBarModelAsync(packName, flyout: false);
        return PartialView("~/Plugins/CW.Core/Views/SubsidyCatalog/SubsidyBar/Default.cshtml", model);
    }

    // GET /SubsidyCatalog/BarFlyout  (optional: compact version for header)
    public async Task<IActionResult> BarFlyout()
    {
        var customer = await _workContext.GetCurrentCustomerAsync();
        var packName = await _packNameContext.GetCurrentPackNameAsync(customer);

        if (packName == null)
            return Content(string.Empty);

        var model = await _catalogModelFactory.PrepareSubsidyBarModelAsync(packName, flyout: true);
        return PartialView("~/Plugins/CW.Core/Views/SubsidyCatalog/SubsidyBar/Flyout.cshtml", model);
    }

    // GET /SubsidyCatalog/ProductGroupHeaders  (AJAX — returns JSON dict: productGroupId → header HTML)
    public async Task<IActionResult> ProductGroupHeaders()
    {
        var customer = await _workContext.GetCurrentCustomerAsync();
        var packName = await _packNameContext.GetCurrentPackNameAsync(customer);

        if (packName == null)
            return Json(new Dictionary<int, string>());

        var fullModel = await _catalogModelFactory.PrepareSubsidyCatalogModelAsync(packName, "grid");
        var result = new Dictionary<int, string>();

        foreach (var pg in fullModel.ProductGroups)
        {
            var html = await this.RenderViewToStringAsync(
                "~/Plugins/CW.Core/Views/SubsidyCatalog/_ProductGroupHeader.cshtml", pg);
            result[pg.Id] = html;
        }

        return Json(result);
    }
}
```

> **`RenderViewToStringAsync`:** This requires a utility method that uses `ICompositeViewEngine` + a `StringWriter` to render a partial view to a string. Check if CW.Core already has such a helper. If not, see NopCommerce source for `HtmlHelperViewExtensions.RenderPartialViewToString`. An alternative is to avoid per-group header JSON and instead reload the entire `_ProductGroups` partial on cart events.

---

## 9. Routes

Register in `RouteProvider.cs` (set priority above default NOP routes):

```csharp
endpointRouteBuilder.MapControllerRoute(
    name: "Plugin.CW.Core.SubsidyCatalog",
    pattern: "SubsidyCatalog",
    defaults: new { controller = "SubsidyCatalog", action = "Index" });

endpointRouteBuilder.MapControllerRoute(
    name: "Plugin.CW.Core.SubsidyCatalog.Items",
    pattern: "SubsidyCatalog/Items",
    defaults: new { controller = "SubsidyCatalog", action = "Items" });

endpointRouteBuilder.MapControllerRoute(
    name: "Plugin.CW.Core.SubsidyCatalog.Bar",
    pattern: "SubsidyCatalog/Bar",
    defaults: new { controller = "SubsidyCatalog", action = "Bar" });

endpointRouteBuilder.MapControllerRoute(
    name: "Plugin.CW.Core.SubsidyCatalog.ProductGroupHeaders",
    pattern: "SubsidyCatalog/ProductGroupHeaders",
    defaults: new { controller = "SubsidyCatalog", action = "ProductGroupHeaders" });
```

---

## 10. SubsidyBarViewComponent

```csharp
// Location: Components/SubsidyBarViewComponent.cs
[ViewComponent(Name = "SubsidyBar")]
public class SubsidyBarViewComponent : NopViewComponent
{
    private readonly ISubsidyCatalogModelFactory _catalogModelFactory;
    private readonly IPackNameContext _packNameContext;
    private readonly IWorkContext _workContext;

    public SubsidyBarViewComponent(
        ISubsidyCatalogModelFactory catalogModelFactory,
        IPackNameContext packNameContext,
        IWorkContext workContext)
    {
        _catalogModelFactory = catalogModelFactory;
        _packNameContext = packNameContext;
        _workContext = workContext;
    }

    public async Task<IViewComponentResult> InvokeAsync(bool flyout = false)
    {
        var customer = await _workContext.GetCurrentCustomerAsync();
        var packName = await _packNameContext.GetCurrentPackNameAsync(customer);

        if (packName == null)
            return Content(string.Empty);

        var model = await _catalogModelFactory.PrepareSubsidyBarModelAsync(packName, flyout);
        return View("~/Plugins/CW.Core/Views/SubsidyCatalog/SubsidyBar/Default.cshtml", model);
    }
}
```

---

## 10.5 PgIcon HTML Helper

Product group icons live in a separate SVG sprite (`pg-icons.svg`) from the CW utility icons (`cw-icons.svg`). To render them consistently, add a `PgIcon` extension method to the existing `CWIconHtmlHelperExtensions` class.

### Method signature

```csharp
// In Extensions/CWIconHtmlHelperExtensions.cs
private const string PgSvgSpritePath = "/Plugins/CW.Core/Content/icons/pg-icons.svg";

/// <summary>
/// Renders a product group SVG icon from the pg-icons.svg sprite.
/// Returns empty when iconId is null or empty (no icon assigned).
/// </summary>
/// <param name="helper">The IHtmlHelper.</param>
/// <param name="iconId">Symbol ID in pg-icons.svg (e.g. "pg-icon-Shirts").</param>
/// <param name="extraClass">Optional additional CSS class(es) beyond "pg-icon".</param>
/// <param name="color">Optional inline color value (e.g. "#3a7bd5"). Icons use fill:currentColor if null.</param>
public static IHtmlContent PgIcon(this IHtmlHelper helper, string? iconId, string? extraClass = null, string? color = null)
{
    if (string.IsNullOrWhiteSpace(iconId))
        return HtmlString.Empty;

    var classes = string.IsNullOrWhiteSpace(extraClass)
        ? "pg-icon"
        : $"pg-icon {extraClass}";

    var styleAttr = string.IsNullOrWhiteSpace(color) ? "" : $""" style="color:{color}" """;

    return new HtmlString(
        $"""<svg class="{classes}" aria-hidden="true"{styleAttr}><use href="{PgSvgSpritePath}#{iconId}"/></svg>""");
}
```

### Usage in views

`@using Nop.Plugin.CW.Core.Extensions` is already in the plugin's `_ViewImports.cshtml`, so no additional import is needed.

```cshtml
{{!-- product group icon with color from data model --}}
@Html.PgIcon(pg.Icon, null, pg.Colour)

{{!-- with an extra CSS class (e.g. in the group header) --}}
@Html.PgIcon(Model.Icon, "pg-header-icon", Model.Colour)

{{!-- icon with no color override (inherits from parent via CSS currentColor) --}}
@Html.PgIcon(Model.Icon)
```

### Admin area pattern (for reference)

The admin DataTables columns render pg icons via inline JS — this is intentional (DataTables renders cell content as HTML strings, not Razor). The `PgIcon` helper is for Razor views only.

```javascript
// Admin DataTables column renderer (already in place — do not change)
return data
    ? '<svg class="pg-icon" style="color:' + (row.Colour || "#000") + '" aria-hidden="true">'
      + '<use href="/Plugins/CW.Core/Content/icons/pg-icons.svg#' + data + '"/></svg>'
    : '';
```

---

## 11. Views

### File layout

```
Views/SubsidyCatalog/
├── Index.cshtml                        Main catalog page
├── _ProductGroups.cshtml               Accordion list (AJAX-reloadable partial)
├── _ProductGroupHeader.cshtml          Header row per group (icon, name, qty circles, credit chip)
├── _ProductsGrid.cshtml                Product grid/list per group
└── SubsidyBar/
    ├── Default.cshtml                  Full subsidy bar (for catalog page and AJAX reload)
    └── Flyout.cshtml                   Compact version (optional, for mini header widget)
```

### 11.1 `Index.cshtml`

```cshtml
@model SubsidyCatalogModel
@{
    Layout = "_ColumnsOne";
    NopHtml.AppendPageCssClassParts("html-subsidy-catalog-page");
    NopHtml.AddTitleParts(T("Plugins.CW.Core.SubsidyCatalog.PageTitle").Text);
    NopHtml.AppendScriptParts(ResourceLocation.Footer, Url.Content("~/Plugins/CW.Core/Content/SubsidyCatalog/subsidy-catalog.js"));
    NopHtml.AppendCssFileParts("~/Plugins/CW.Core/Content/SubsidyCatalog/subsidy-catalog.css");
}

<div class="page subsidy-catalog-page">
    <div class="page-body">
        <div class="subsidy-bar-container">
            @await Component.InvokeAsync("SubsidyBar", new { flyout = false })
        </div>
        <div class="catalog-controls">
            <div class="product-viewmode">
                <span>@T("Catalog.ViewMode")</span>
                @foreach (var mode in Model.AvailableViewModes)
                {
                    <a class="viewmode-icon @mode.Value @(mode.Selected ? "selected" : "")"
                       data-viewmode="@mode.Value" href="#">@mode.Text</a>
                }
            </div>
        </div>
        <div class="product-groups-container @(Model.ViewMode == "list" ? "productgroups-list" : "productgroups-grid")">
            @await Html.PartialAsync("~/Plugins/CW.Core/Views/SubsidyCatalog/_ProductGroups.cshtml", Model)
        </div>
    </div>
</div>

@* Pass server-side URLs to the JS module *@
<script>
    window.subsidyCatalog = {
        itemsUrl: '@Url.Action("Items", "SubsidyCatalog")',
        barUrl: '@Url.Action("Bar", "SubsidyCatalog")',
        headersUrl: '@Url.Action("ProductGroupHeaders", "SubsidyCatalog")',
        cartUrl: '@Url.RouteUrl("ShoppingCart")'
    };
</script>
```

### 11.2 `_ProductGroups.cshtml`

```cshtml
@model SubsidyCatalogModel

@foreach (var productGroup in Model.ProductGroups)
{
    @* A group is "exhausted" when no remaining qty AND no remaining credit AND no global credit is allocated *@
    var isExhausted = productGroup.AvailableQuantity == 0
                   && productGroup.ShoppingCartQuantity >= productGroup.AvailableQuantity
                   && productGroup.RemainCredit <= 0
                   && productGroup.AllocatedGlobalCredit == 0;

    <div class="product-group" id="product-group-@productGroup.Id">
        <div class="product-group-header" id="product-group-header-@productGroup.Id">
            <div class="product-group-header-info">
                @await Html.PartialAsync("~/Plugins/CW.Core/Views/SubsidyCatalog/_ProductGroupHeader.cshtml", productGroup)
            </div>
            <div class="toggle-icon @(isExhausted ? "collapsed" : "expanded")">
                <div class="arrow"></div>
            </div>
        </div>
        <div class="products-container">
            <div class="products-wrapper">
                @await Html.PartialAsync("~/Plugins/CW.Core/Views/SubsidyCatalog/_ProductsGrid.cshtml", productGroup)
            </div>
        </div>
    </div>
}

<script>
    $(document).ready(function () {
        $('.toggle-icon').on('click', function () {
            $(this).closest('.product-group').find('.products-container').slideToggle();
            $(this).toggleClass('collapsed expanded');
        });
        // Collapse exhausted groups on load
        $('.toggle-icon.collapsed').each(function () {
            $(this).closest('.product-group').find('.products-container').hide();
        });
    });
</script>
```

### 11.3 `_ProductGroupHeader.cshtml`

```cshtml
@model SubsidyCatalogProductGroupModel

@{
    var remainQty = Model.AvailableQuantity - Model.ShoppingCartQuantity;
    var usedQty   = Model.TotalQuantity - Model.AvailableQuantity;
    var isExhausted = Model.AvailableQuantity == 0
                   && Model.ShoppingCartQuantity >= Model.AvailableQuantity
                   && Model.RemainCredit <= 0
                   && Model.AllocatedGlobalCredit == 0;
}

@Html.PgIcon(Model.Icon, "pg-header-icon", Model.Colour)
<span class="pg-header-name">@Model.Name</span>

@if (Model.TotalQuantity > 0)
{
    <div class="pg-header-qty">
        @if (Model.TotalQuantity > 6)
        {
            <div class="qty-badge available">@remainQty</div>
            <div class="qty-badge in-cart">@Model.ShoppingCartQuantity</div>
            <div class="qty-badge used">@usedQty</div>
        }
        else
        {
            @for (var i = 0; i < remainQty; i++) { <div class="circle available"></div> }
            @for (var i = 0; i < Model.ShoppingCartQuantity; i++) { <div class="circle in-cart"></div> }
            @for (var i = 0; i < usedQty; i++) { <div class="circle used"></div> }
        }
    </div>
}

@if (!string.IsNullOrEmpty(Model.RemainCreditFormatted))
{
    <span class="pg-header-credit">@Model.RemainCreditFormatted</span>
}

@if (isExhausted)
{
    <span class="pg-header-badge exhausted">@T("Plugins.CW.Core.SubsidyCatalog.ProductGroup.Exhausted")</span>
}
```

### 11.4 `_ProductsGrid.cshtml`

```cshtml
@model SubsidyCatalogProductGroupModel

@if (Model.Products.Any())
{
    <div class="@(Model.ViewMode == "list" ? "product-list" : "product-grid")">
        <div class="item-grid">
            @foreach (var product in Model.Products)
            {
                <div class="item-box">
                    @await Html.PartialAsync("_ProductBox", product)
                </div>
            }
        </div>
    </div>
}

<script>
    $(document).ready(function () {
        setTimeout(configProductDetailsLink, 100);
    });

    function configProductDetailsLink() {
        // Redirect product image/title link clicks to the add-to-cart button
        // so customers don't navigate away from the catalog page for simple products
        $(".product-details-link").each(function () {
            var $item = $(this).closest('.product-item');
            var $qtyWrapper = $item.find('.qty-wrapper');
            if ($item.length > 0 && $qtyWrapper.length === 0) {
                var $addBtn = $item.find('.product-box-add-to-cart-button');
                if ($addBtn.length > 0) {
                    $(this).attr("href", "javascript:void(0);")
                           .attr("title", $addBtn.text());
                    $(this).find('img').attr("title", $addBtn.text());
                    $(this).on('click', function (e) {
                        e.stopPropagation();
                        e.preventDefault();
                        $addBtn.click();
                        return false;
                    });
                }
            }
        });
    }
</script>
```

### 11.5 `SubsidyBar/Default.cshtml`

```cshtml
@model SubsidyBarModel

@{
    NopHtml.AppendCssFileParts("~/Plugins/CW.Core/Content/icons/cw-icons.css");
    NopHtml.AppendCssFileParts("~/Plugins/CW.Core/Content/SubsidyCatalog/subsidy-bar.css");
    var iconClass = Model.PackNameIsGroup    ? "packname-group"
                  : Model.PackNameIsCustomer ? "packname-customer"
                  : "packname-no-customer";
}

<div class="subsidy-bar">

    <!-- LEFT: Available -->
    <div class="subsidy-available">
        <div class="subsidy-bar-title"><span>@T("Plugins.CW.Core.SubsidyBar.Available.Title")</span></div>
        <div class="subsidy-available-container">

            <!-- PackName identity — color set on the container; icons inherit via fill: currentColor -->
            <div class="bar-packname-identity" style="color: @Model.PackNameColour">
                <div>
                    @Html.PackNameTypeIcon(iconClass, "packname-identity-icon")
                    <span class="packname-name">@Model.PackNameName</span>
                </div>
                @if (!string.IsNullOrEmpty(Model.PackNameBranch))
                {
                    <div>
                        @Html.CwIcon("cw-icon-truck", "packname-identity-icon")
                        <span class="packname-branch">@Model.PackNameBranch</span>
                    </div>
                }
                @if (!string.IsNullOrEmpty(Model.PackNameDepartment))
                {
                    <div>
                        @Html.CwIcon("cw-icon-department", "packname-identity-icon")
                        <span class="packname-department">@Model.PackNameDepartment</span>
                    </div>
                }
                @if (!string.IsNullOrEmpty(Model.PackNameCostCenter))
                {
                    <div>
                        @Html.CwIcon("cw-icon-costcenter", "packname-identity-icon")
                        <span class="packname-costcenter">@Model.PackNameCostCenter</span>
                    </div>
                }
                @if (!string.IsNullOrEmpty(Model.PackNamePOEntry))
                {
                    <div>
                        @Html.CwIcon("cw-icon-po", "packname-identity-icon")
                        <span class="packname-po">@Model.PackNamePOEntry</span>
                    </div>
                }
            </div>

            <!-- Available credit / qty items -->
            <div class="subsidy-bar-items available-items">
                @if (Model.RemainCredit > 0 || Model.TotalCredit > 0)
                {
                    <div class="bar-item global-credit-item">
                        @Html.CwIcon("cw-icon-Globe")
                        <span class="pg-name">@T("Plugins.CW.Core.SubsidyBar.GlobalCreditRemaining")</span>
                        <span class="pg-credit">@Model.RemainCreditFormatted</span>
                    </div>
                }

                @foreach (var pg in Model.ProductGroups)
                {
                    var remainQty = pg.AvailableQuantity - pg.ShoppingCartQuantity;
                    var usedQty = pg.TotalQuantity - pg.AvailableQuantity;

                    <div class="bar-item pg-item">
                        @Html.PgIcon(pg.Icon, null, pg.Colour)
                        <span class="pg-name">@pg.Name</span>
                        <div class="pg-usage">
                            @if (pg.TotalQuantity > 6)
                            {
                                <div class="qty-badge available">@remainQty</div>
                                <div class="qty-badge in-cart">@pg.ShoppingCartQuantity</div>
                                <div class="qty-badge used">@usedQty</div>
                            }
                            else
                            {
                                @for (var i = 0; i < remainQty; i++) { <div class="circle available"></div> }
                                @for (var i = 0; i < pg.ShoppingCartQuantity; i++) { <div class="circle in-cart"></div> }
                                @for (var i = 0; i < usedQty; i++) { <div class="circle used"></div> }
                            }
                            @if (!string.IsNullOrEmpty(pg.RemainCreditFormatted))
                            {
                                <span class="pg-credit">+ @pg.RemainCreditFormatted</span>
                            }
                            @if (pg.TotalQuantity == 0 && string.IsNullOrEmpty(pg.RemainCreditFormatted) && Model.TotalCredit == 0)
                            {
                                <span class="pg-unavailable">@T("Plugins.CW.Core.SubsidyBar.NotAvailable")</span>
                            }
                        </div>
                    </div>
                }

                @if (Model.RemainCredit <= 0 && !Model.ProductGroups.Any())
                {
                    <div class="bar-item">
                        <span>@T("Plugins.CW.Core.SubsidyBar.NothingAvailable")</span>
                    </div>
                }
            </div>

            <!-- Legend + optional topup button -->
            @if (Model.ProductGroups.Any(pg => pg.TotalQuantity > 0) || Model.EditSubsidyTopup)
            {
                <div class="subsidy-bar-legend">
                    @if (Model.EditSubsidyTopup)
                    {
                        @* Bootstrap modal replaces old Kendo Window *@
                        <button type="button" class="btn btn-sm btn-outline-secondary change-topup-btn"
                                data-toggle="modal" data-target="#subsidyTopupModal">
                            @T("Plugins.CW.Core.SubsidyBar.ChangeTopup")
                        </button>
                        <div class="modal fade" id="subsidyTopupModal" tabindex="-1" role="dialog" aria-hidden="true">
                            <div class="modal-dialog modal-lg" role="document">
                                <div class="modal-content">
                                    <div class="modal-header">
                                        <h5 class="modal-title">@T("Plugins.CW.Core.SubsidyBar.ChangeTopup.Title")</h5>
                                        <button type="button" class="close" data-dismiss="modal"><span aria-hidden="true">&times;</span></button>
                                    </div>
                                    <div class="modal-body" id="subsidyTopupModalBody">
                                        <div class="text-center"><div class="spinner-border" role="status"></div></div>
                                    </div>
                                </div>
                            </div>
                        </div>
                        <script>
                            $('#subsidyTopupModal').on('show.bs.modal', function () {
                                $('#subsidyTopupModalBody').load('@Url.Action("ChangeTopup", "SubsidyPackName")');
                            });
                        </script>
                    }
                    @if (Model.ProductGroups.Any(pg => pg.TotalQuantity > 0))
                    {
                        <div class="legend-item"><div class="circle available"></div><span>@T("Plugins.CW.Core.SubsidyBar.Legend.Available")</span></div>
                        <div class="legend-item"><div class="circle in-cart"></div><span>@T("Plugins.CW.Core.SubsidyBar.Legend.InCart")</span></div>
                        <div class="legend-item"><div class="circle used"></div><span>@T("Plugins.CW.Core.SubsidyBar.Legend.Used")</span></div>
                    }
                </div>
            }
        </div>
    </div>

    <!-- RIGHT: Cart summary -->
    <div class="subsidy-cart">
        <div class="subsidy-bar-title">
            <span>@string.Format(T("Plugins.CW.Core.SubsidyBar.Cart.Title").Text, Model.PackNameName)</span>
        </div>
        <div class="subsidy-bar-items cart-items">
            @if (!string.IsNullOrEmpty(Model.ShoppingCartCreditFormatted))
            {
                <div class="cart-item global-credit-item">
                    @Html.CwIcon("cw-icon-Globe")
                    <span class="pg-name">@T("Plugins.CW.Core.SubsidyBar.GlobalCreditUsed")</span>
                    <span class="pg-credit">@Model.ShoppingCartCreditFormatted</span>
                </div>
            }

            @foreach (var pg in Model.ProductGroups.Where(x => x.ShoppingCartQuantity > 0 || !string.IsNullOrEmpty(x.ShoppingCartCreditFormatted)))
            {
                <div class="cart-item">
                    @Html.PgIcon(pg.Icon, null, pg.Colour)
                    <span class="pg-name">@pg.Name</span>
                    <div class="pg-cart-usage">
                        <span class="pg-qty">@pg.ShoppingCartQuantity</span>
                        @if (!string.IsNullOrEmpty(pg.ShoppingCartCreditFormatted))
                        {
                            <span class="pg-credit">+ @pg.ShoppingCartCreditFormatted</span>
                        }
                    </div>
                </div>
            }

            @if (string.IsNullOrEmpty(Model.ShoppingCartCreditFormatted) &&
                 !Model.ProductGroups.Any(x => x.ShoppingCartQuantity > 0 || !string.IsNullOrEmpty(x.ShoppingCartCreditFormatted)))
            {
                <div class="cart-item">
                    <span>@T("Plugins.CW.Core.SubsidyBar.Cart.Empty")</span>
                </div>
            }
        </div>
    </div>

</div>
```

---

## 12. Client-Side Script (`subsidy-catalog.js`)

> **Cart refresh, as built:** the script does **not** intercept `$.ajaxComplete` on the add-to-cart URL as the code below
> shows. The bar and the product-group headers subscribe to the one cart-changed event, `cwCartChangedEvent` on `document`,
> raised by `Content/Common/cw-cart-events.js` (`CWCart.notifyChanged()`; it hooks nop's `AjaxCart.success_process` once) —
> `subsidy-system.md` §8.7. The listing below predates that and is kept for the rest of its content.


```javascript
// ~/Plugins/CW.Core/Content/SubsidyCatalog/subsidy-catalog.js
(function ($) {
    'use strict';

    var cfg = window.subsidyCatalog || {};

    function loadBar() {
        $('.subsidy-bar-container').load(cfg.barUrl);
    }

    function loadHeaders() {
        $.getJSON(cfg.headersUrl, function (data) {
            if (!data) return;
            $.each(data, function (id, html) {
                $('#product-group-header-' + id + ' .product-group-header-info').html(html);
            });
        });
    }

    function getViewMode() {
        var $sel = $('[data-viewmode].selected');
        return $sel.length > 0 ? $sel.data('viewmode') : 'grid';
    }

    function applyViewClass(viewMode) {
        $('.product-groups-container')
            .toggleClass('productgroups-list', viewMode === 'list')
            .toggleClass('productgroups-grid', viewMode !== 'list');
    }

    function loadItems() {
        var viewMode = getViewMode();
        $('.product-groups-container').load(cfg.itemsUrl + '?viewMode=' + viewMode, function () {
            applyViewClass(viewMode);
        });
    }

    $(document).ready(function () {

        // View mode toggle
        $('[data-viewmode]').on('click', function () {
            if (!$(this).hasClass('selected')) {
                $('[data-viewmode]').removeClass('selected');
                $(this).addClass('selected');
                loadItems();
            }
            return false;
        });

        // NOP 4.9 does not fire custom jQuery events from AjaxCart — intercept via
        // $.ajaxComplete filtered by the add-to-cart URL. Both catalog and details
        // add-to-cart URLs contain "addproducttocart". response.updatetopcartsectionhtml
        // being present means the cart was actually modified (NOP omits it on failure).
        // AjaxCart.success_process already handles the success notification via response.message.
        $(document).ajaxComplete(function (event, xhr, settings) {
            if (!settings.url || settings.url.toLowerCase().indexOf('addproducttocart') === -1) return;
            try {
                var response = JSON.parse(xhr.responseText);
                if (response && !response.redirect && response.updatetopcartsectionhtml) {
                    loadBar();
                    loadHeaders();
                }
            } catch (e) { }
        });

        // Subsidy topup changed
        $(document).on('subsidyTopupChangedEvent', function () {
            loadBar();
        });

        // Product-group topup changed
        $(document).on('subsidyGroupTopupChangedEvent', function () {
            loadBar();
            loadHeaders();
        });

        // Apply initial view class
        applyViewClass(getViewMode());
    });

})(jQuery);
```

---

## 13. DI Registration

In `NopStartup.ConfigureServices` (CW.Core):

```csharp
services.AddScoped<ISubsidyCatalogCartService, SubsidyCatalogCartService>();
services.AddScoped<ISubsidyCatalogModelFactory, SubsidyCatalogModelFactory>();
```

No changes to existing service registrations.

---

## 14. Locale Resources

Add to `InstallAsync`:

```csharp
await _localizationService.AddOrUpdateLocaleResourceAsync(new Dictionary<string, string>
{
    ["Plugins.CW.Core.SubsidyCatalog.PageTitle"]                  = "My Allowances",
    ["Plugins.CW.Core.SubsidyCatalog.ProductGroup.Exhausted"]     = "Completed",
    ["Plugins.CW.Core.SubsidyBar.Available.Title"]                = "Available",
    ["Plugins.CW.Core.SubsidyBar.GlobalCreditRemaining"]          = "Global Allowance Remaining",
    ["Plugins.CW.Core.SubsidyBar.GlobalCreditUsed"]               = "Global Allowance Used",
    ["Plugins.CW.Core.SubsidyBar.NothingAvailable"]               = "Nothing available",
    ["Plugins.CW.Core.SubsidyBar.NotAvailable"]                   = "N/A",
    ["Plugins.CW.Core.SubsidyBar.Cart.Title"]                     = "Cart — {0}",
    ["Plugins.CW.Core.SubsidyBar.Cart.Empty"]                     = "No items in cart",
    ["Plugins.CW.Core.SubsidyBar.Legend.Available"]               = "Available",
    ["Plugins.CW.Core.SubsidyBar.Legend.InCart"]                  = "In Cart",
    ["Plugins.CW.Core.SubsidyBar.Legend.Used"]                    = "Used",
    ["Plugins.CW.Core.SubsidyBar.ChangeTopup"]                    = "Change Topup",
    ["Plugins.CW.Core.SubsidyBar.ChangeTopup.Title"]              = "Change Topup",
});
```

Add to `UninstallAsync`:

```csharp
await _localizationService.DeleteLocaleResourcesAsync("Plugins.CW.Core.SubsidyCatalog");
await _localizationService.DeleteLocaleResourcesAsync("Plugins.CW.Core.SubsidyBar");
```

---

## 15. ViewLocationExpander

Register view paths in the existing CW.Core `ViewLocationExpander`:

```
~/Plugins/CW.Core/Views/SubsidyCatalog/{0}.cshtml
~/Plugins/CW.Core/Views/SubsidyCatalog/SubsidyBar/{0}.cshtml
```

If the current expander already covers `~/Plugins/CW.Core/Views/{1}/{0}.cshtml` (controller-named subfolder pattern), no changes are needed — the `SubsidyCatalog` controller will resolve automatically.

---

## 16. Public Store Navigation

Add a navigation link so packnames can reach the catalog:

```csharp
// Suggested tab in customer navigation (after "My Subsidies" tab at Id = 22)
// RouteUrl: "Plugin.CW.Core.SubsidyCatalog"
// CssClass: "subsidy-catalog"
// Tab Id: 23
```

Inject via the existing customer navigation override in CW.Core (`CustomerNavigationModelFactory` or equivalent event consumer).

---

## 17. Open Questions / Implementation Notes

1. **`PackNameCredit.SubsidyCredits` property name:** The TIG49 `PackNameCredit` DTO was built from `Domain/Subsidies/DTOs/PackNameCredit.cs`. Confirm the list of subsidy credits uses the property name `SubsidyCredits` (not `AllowanceCredits` from the old project). Adjust all references if different.

2. **Price calculation for cart items:** `GetCartItemLinePriceAsync` in the service uses `IPriceCalculationService.GetFinalPriceAsync`. Verify whether `IShoppingCartService.GetUnitPriceAsync(ShoppingCartItem, bool)` is available and preferable in this NOP 4.9 build — it handles attribute-combination pricing, tiered pricing, and discounts that `GetFinalPriceAsync` may not automatically apply.

3. **`PackNameShoppingCartItem` repository — `StoreId` column:** Confirm `StoreId` is exposed in the `vw_CORE_PN_PackNameShoppingCartItem` view and is mapped in `PackNameShoppingCartItem`. If not, filter by the `ShoppingCartItem.StoreId` join, or omit the store filter if the view is already store-scoped.

4. **`GetAllSubsidyPackNamesByPackNameIdAsync` — method availability:** The `OrderOutsidePeriod` injection step (§6.2 Step 2) requires loading all subsidy-packname assignments for the packname regardless of ordering period. Check if `ISubsidyOrderingService` or `ISubsidyService` already has this method, or if one needs to be added.

5. **Gender filtering — attribute names:** The spec assumes a SpecificationAttribute named `"Gender"` with options containing "Mens", "Boys", "Unisex", "Womens", "Girls". Confirm the actual attribute and option names used in this store's product catalog. These constants should live in `CWCoreDefaults` or a `SubsidyCatalogDefaults` class.

6. **`ProductGroupHeaders` AJAX — `RenderViewToStringAsync`:** Check if CW.Core has a utility for rendering a partial view to a string. If not, the simplest alternative is to skip per-group header JSON and instead load the full `_ProductGroups.cshtml` on cart events (replace the entire container). This is less granular but simpler and equally correct.

7. **`EditSubsidyTopup` modal route:** `@Url.Action("ChangeTopup", "SubsidyPackName")` assumes a controller + action that does not yet exist. Set `EditSubsidyTopup = false` until the topup management flow is built.

8. **`displayBarNotification` JS function:** This is a NOP public-store global. The `Nop.Resources` object contains localized strings. Verify the exact key for the "product added" message and the function signature in this NOP 4.9 build.

---

## 18. Implementation Checklist

- [ ] `SubsidyCartUsageItem` DTO (`Domain/Subsidies/DTOs/SubsidyCartUsageItem.cs`)
- [ ] `ISubsidyCatalogCartService` interface (`Services/Subsidies/ISubsidyCatalogCartService.cs`)
- [ ] `SubsidyCatalogCartService` implementation (`Services/Subsidies/SubsidyCatalogCartService.cs`)
- [ ] `ISubsidyCatalogModelFactory` interface (`Factories/ISubsidyCatalogModelFactory.cs`)
- [ ] `SubsidyCatalogModelFactory` implementation (`Factories/SubsidyCatalogModelFactory.cs`)
- [ ] `SubsidyCatalogModel` (`Models/Subsidies/SubsidyCatalogModel.cs`)
- [ ] `SubsidyCatalogProductGroupModel` (`Models/Subsidies/SubsidyCatalogProductGroupModel.cs`)
- [ ] `SubsidyBarModel` (`Models/Subsidies/SubsidyBarModel.cs`)
- [ ] `SubsidyCatalogController` (`Controllers/SubsidyCatalogController.cs`)
- [ ] `SubsidyBarViewComponent` (`Components/SubsidyBarViewComponent.cs`)
- [ ] `Index.cshtml` — main catalog page
- [ ] `_ProductGroups.cshtml` — accordion partial
- [ ] `_ProductGroupHeader.cshtml` — group header partial
- [ ] `_ProductsGrid.cshtml` — products grid/list partial
- [ ] `SubsidyBar/Default.cshtml` — full bar
- [ ] `SubsidyBar/Flyout.cshtml` — compact bar (optional)
- [ ] `subsidy-catalog.js` + `subsidy-catalog.css` + `subsidy-bar.css`
- [ ] Route registrations in `RouteProvider.cs`
- [ ] DI registrations in `NopStartup.cs`
- [ ] Locale resources in plugin lifecycle (`InstallAsync` / `UninstallAsync`)
- [ ] View path registration in `ViewLocationExpander`
- [ ] Public store navigation entry (customer nav tab)
- [ ] Manual test: bar reloads correctly on add-to-cart and remove-from-cart
- [ ] Manual test: exhausted groups collapse on load
- [ ] Manual test: `OrderOutsidePeriod` groups appear with zero credits
- [ ] Manual test: `OnlyAllocatedProducts` — products outside pg do not consume global credit in bar
