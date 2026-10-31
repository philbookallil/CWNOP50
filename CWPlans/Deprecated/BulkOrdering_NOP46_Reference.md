# Bulk Ordering — nopCommerce 4.6 (`Nop.Plugin.Jim2.BE`) Technical Reference

> **4.9 history.** Copied from the 4.90 source (`NOP49/CWPlans/`) on 2026-10-31, when CW.Core moved to nopCommerce 5.00. It is kept as the record of the 4.9 work and is **not updated for 5.00**: file paths, line numbers and versions are 4.9's. For how CW.Core works now, read the references in `CWPlans/` and `CWPlans/CWCore_Port_Reference.md`.

> **Status:** reference material for the 4.9 `CW.Core` rebuild. Nothing here is a design decision for 4.9 —
> it is a description of what the 4.6 code actually does, so the rebuild starts from fact rather than memory.
>
> **Source of every statement in this document is a file under `NOP46/Nop.Plugin.Jim2.BE/`.** Line numbers are
> from the working copy on disk at the time of writing and are given as `File.cs:NNN`. Anything that could not
> be established by reading the code is marked **[unverified]** and says what would settle it.
>
> **Scope.** Bulk Ordering only. The `BulkOrderController` also hosts *Refill Ordering*, which shares the
> controller, the category-navigation component and some page-size plumbing but is a separate feature driven by
> a Jim2 API stock-refill call. Refill is described only where it touches bulk ordering. *Favourites*
> (`Views/Favourites/*`, `Jim2FavouritesController`) is a near-verbatim copy of the bulk-order views and the
> add-to-cart form protocol; it is called out in §19 because the duplication is one of the things the rebuild
> should not reproduce.

---

## 1. What the feature is

A grid-style ordering screen for trade/B2B customers. Instead of visiting one product page at a time, the
customer sees a paged list of products; each product that has attributes expands into a **quantity matrix**
(one quantity box per attribute-value combination) showing per-combination price, stock, ETA and
"already in cart" state. The customer types quantities across many products and combinations, then presses
**Add all to Cart** once.

Three surfaces exist:

| Surface | Entry | View |
|---|---|---|
| The bulk ordering page | `/bulkordering` | `Views/BulkOrder/BulkOrdering.cshtml` |
| The per-product attribute matrix, loaded by AJAX into the page | `/BulkOrder/BulkOrderingItem/{id}` | `Views/BulkOrder/_BulkOrderAttributes.cshtml` |
| The same matrix embedded in the **product details page** | product page | `Views/Product/_BulkProductAttributes.cshtml` + `_BulkAddToCart.cshtml` |

---

## 2. Routing and request lifecycle

### 2.1 Routes (`RouteConfig.cs`)

| Name | Pattern | Target | Line |
|---|---|---|---|
| `BulkOrdering` | `{lang}/bulkordering/` | `BulkOrder.BulkOrdering` | 241 |
| `BulkOrderingPaged` | `{lang}/bulkordering/page/{pageNumber:min(0)}` | `BulkOrder.BulkOrderingPaged` | 245 |
| `BulkOrderAll` | `{lang}/BulkOrderAll/{customerGuid?}` | `Jim2ShoppingCart.BulkOrderAll` | 250 |
| `AddBulkProductToCart-Details` | `addbulkproducttocart/details/{productId}/{shoppingCartTypeId}` | `Jim2ShoppingCart.AddBulkProductToCart_Details` | 270 |
| `AddBulkProductToWishList-Details` | `addbulkproducttowishlist/details/{productId}/{shoppingCartTypeId}` | `Jim2ShoppingCart.AddBulkProductToWishList_Details` | 255 |
| `AddBulkProductsToWishList-Details` | `addbulkproductstowishlist/details/{productId}/{shoppingCartTypeId}` | `Jim2ShoppingCart.AddBulkProductsToWishList_Details` | 260 |
| `AddBulkProductToQuote-Details` | `addbulkproducttoquote/details/{productId}/{shoppingCartTypeId}` | `Jim2ShoppingCart.AddBulkProductToQuote_Details` | 265 |

`BulkOrderingItems` and `BulkOrderingItem` have **no named route** — the views call them through
`Url.Action("BulkOrderingItems", "BulkOrder")` / `Url.Action("BulkOrderingItem", "BulkOrder", new { id })`,
i.e. via the default MVC route.

### 2.2 The three-request page load

This is the single most important shape to understand, because it is where the performance profile comes from.

```
1.  GET /bulkordering                    -> BulkOrdering()             -> BulkOrdering.cshtml
    Builds the page shell only: filters, sort, page-size, view-mode, order list, category tree.
    loadLists: true, loadProducts: FALSE — no products are fetched on this request.

2.  (on document.ready) GET /BulkOrder/BulkOrderingItems?...    -> BulkOrderingItems()
    loadLists: false, loadProducts: TRUE — returns _BulkOrder.cshtml, the product rows for the page,
    injected into #itemsContainer with $().load().

3.  For EACH product row that has attributes, the row emits its own document.ready script that does
    GET /BulkOrder/BulkOrderingItem/{productId}       -> BulkOrderingItem()
    returning _BulkOrderAttributes.cshtml, injected into #product-details-{productId}.
```

So a page of 15 attributed products is **17 HTTP requests**, each re-resolving customer, store, cart, packname
and settings. (`BulkOrdering.cshtml:234` for step 2; `_BulkOrder.cshtml:234` for step 3.)

### 2.3 Controller actions (`Controllers/BulkOrderController.cs`)

| Action | Line | Attributes | Behaviour |
|---|---|---|---|
| `BulkOrdering(BulkOrderCommand)` | 150 | `[CheckAccessPublicStore(ignore: true)]` | Gate on `ShowBulkOrderingAsync` → else redirect `Homepage`. Normalises `PageNumber`/`PageSize` (default `bulkPageSize = 15`). If `CategoryId == 0 && FilterMode` empty → forces `OrderId = 0`, `FilterMode = AllProducts`, and resets `PageSize` to 15 when `VerticalAttributeMainId > 0`. Calls the factory with `loadLists: true, loadProducts: false`, then `SetBulkOrderPageOptions`. |
| `BulkOrderingItems(BulkOrderCommand)` | 183 | `[CheckAccessPublicStore(true)]`, `[CheckLanguageSeoCode(true)]` | **No `ShowBulkOrderingAsync` gate.** Calls factory with `loadLists: false, loadProducts: true`; returns `_BulkOrder.cshtml` as a partial. |
| `BulkOrderingPaged(int? pageNumber, int orderId, int PageSize, BulkOrderCommand)` | 194 | none | Pager target. Same as `BulkOrdering` but **without the `ShowBulkOrderingAsync` gate**, and dereferences `pageNumber.Value` unguarded. Returns the full `BulkOrdering.cshtml` view. |
| `BulkOrderingItem(int id)` | 212 | `[CheckAccessPublicStore(true)]`, `[CheckLanguageSeoCode(true)]` | Loads the product; `null` → `AccessDeniedView()`. **No `ShowBulkOrderingAsync` gate and no ACL/store-mapping check on the product.** Returns `_BulkOrderAttributes.cshtml`. |

Page-size options are built in `SetBulkOrderPageOptions` (`BulkOrderController.cs:64`):
`15, 30, 50, 100`, or `5, 10, 15, 30, 50, 100` when `VerticalAttributeMainId > 0` (i.e. when products render a
2-D or 3-D matrix and pages are therefore much heavier).

### 2.4 Access control

`ShowBulkOrderingAsync` (`BulkOrderServices.cs:1116`):

```
if (!_jim2StoreSettings.EnableBulkOrdering) return false;
if (!BulkOrderRoleIds.Any())               return true;      // empty list = everyone
return BulkOrderRoleIds.Intersect(customer roles).Any();
```

Only `BulkOrdering` calls it. `BulkOrderingItems`, `BulkOrderingPaged` and `BulkOrderingItem` are reachable
without it. Product-level visibility is still enforced inside the queries for `BulkOrderingItems`
(store mapping + ACL + custom visibility, see §4), but **`BulkOrderingItem/{id}` applies none of those** — it
takes any product id and renders its price/stock matrix.

---

## 3. Filters — `BulkOrderFilterTypeEnum`

`Domain/BulkOrder/BulkOrderFilterTypeEnum.cs`:

| Member | Value | Meaning as documented in the enum |
|---|---|---|
| `StockRefill` | 1 | Refill feature; used by `GetVisibleCategoryIdsByFilter` only |
| `ContractItems` | 2 | Only the customer's contract items within the viewed category |
| `LatestOrdered` | 3 | Products the customer ordered in the last 3 months; or, when `OrderId > 0`, the products of that one order |
| `BestSellers` | 5 | Best sellers overall; products that never sold are excluded |
| `AllProducts` | 6 | No filter |

Note there is **no member 4**, and `StockRefill` is never offered in the bulk-order filter dropdown
(`SetAvailableFilters`, `BulkOrderModelFactory.cs:288` adds only `AllProducts`, `ContractItems` — when
`IsContractedItemsAsync(customer)` — , `LatestOrdered` — when the customer has ≥1 order — and `BestSellers`).

`FilterMode` travels as the **enum member name string** (`"AllProducts"`, …) in the query string and in the
form, and is parsed with `Enum.TryParse` (`BulkOrderModelFactory.cs:1085`). `_ = Enum.TryParse(...)` discards the
result, so an unrecognised value silently becomes `(BulkOrderFilterTypeEnum)0`, which matches no case in the
`switch` at `BulkOrderModelFactory.cs:1125` — the product list is then simply empty, with no error.

---

## 4. Product queries

All five live in `BulkOrderServices.cs` as `Search*Async` privates with thin public `Get*Async` wrappers
(`:1081`–`:1114`). They share a base predicate:

```csharp
!p.Deleted && p.Published && p.VisibleIndividually
&& DateTime.UtcNow >= (p.AvailableStartDateTimeUtc ?? DateTime.MinValue)
&& DateTime.UtcNow <= (p.AvailableEndDateTimeUtc   ?? DateTime.MaxValue)
```

| Query | Line | Extra source | Category handling | Sorting |
|---|---|---|---|---|
| `SearchSelectedOrderItemsAsync` | 435 | join to distinct `ProductId` of `OrderItem` where `OrderId == orderId && !o.Deleted` | `pc.CategoryId == categoryId \|\| c.ParentCategoryId == categoryId`, then `.Distinct()` | `OrderBy(...)` extension |
| `SearchLatestOrdersItemsAsync` | 506 | distinct `ProductId` of the customer's orders in the store since `UtcNow-3 months` | same | `OrderBy(...)` |
| `SearchBestSellersItemsAsync` | 576 | `BestsellersReportLine` grouped by `ProductId` over all non-deleted orders in store, ordered by `TotalQuantity` (or `TotalAmount`) desc | same | **none** — `orderBy` is not a parameter of the public wrapper; `OrderByEnum.OrderByQuantity` is always used |
| `SearchContractedItemsOnlyAsync` | 650 | `_aclService.ApplyContractItemsOnlyConstraint` | `categoryIds` list; groups `ProductCategory` by `ProductId` and joins | `OrderBy(...)` |
| `SearchProductsAsync` | 714 | none | as above, plus: if the requested category **is** the contracted-items category, the Registered and Guests role ids are stripped from `customerRoleIds` before ACL is applied (`:741`–`:751`) | `OrderBy(...)` |

Every query then applies, in this order (order differs slightly between methods — see the note below):

1. `_storeMappingService.ApplyStoreMapping(query, storeId)`
2. `_aclService.ApplyAcl(query, customerRoleIds)`
3. `_aclService.ApplyCustomVisibilityConstraint(query, customer)`

`ApplyCustomVisibilityConstraint` (`Services/Security/Jim2AclService.cs:490`) is a no-op unless
`Jim2DefaultSettings.ContractOnlySupport` is on **and** the customer is flagged contract-items-only; then it
restricts to the customer's contract product ids. `ApplyContractItemsOnlyConstraint` (`:513`) restricts to the
contract items category subtree.

> **Ordering inconsistency.** The three constraint calls are applied in three different orders:
> `SearchSelectedOrderItemsAsync` / `SearchLatestOrdersItemsAsync` / `SearchBestSellersItemsAsync` do
> `ApplyAcl` → `ApplyCustomVisibilityConstraint` (`:467`/`:469`, `:543`/`:545`, `:632`/`:634`);
> `SearchContractedItemsOnlyAsync` does `ApplyContractItemsOnlyConstraint` → `ApplyCustomVisibilityConstraint`
> → `ApplyAcl` (`:676`, `:679`, `:681`); `SearchProductsAsync` does `ApplyCustomVisibilityConstraint` → role
> trimming → `ApplyAcl` (`:739`, `:741`–`:751`, `:753`). Since all are `IQueryable` compositions the result set
> is the same; the difference is cosmetic but makes the five paths read as if they differ.

Paging is at the database layer via `ToPagedListAsync(pageIndex, pageSize)`. `int.MaxValue` page sizes are
decremented by 1 "because some databases don't support int.MaxValue".

### 4.1 Sorting

`Jim2ProductSortingEnum` (`Domain/Catalog/Jim2ProductSortingEnum.cs`) — a Jim2-specific copy of nop's
`ProductSortingEnum` with SKU added:

`Position = 0, NameAsc = 5, NameDesc = 6, SkuAsc = 7, SkuDesc = 8, PriceAsc = 10, PriceDesc = 11, CreatedOn = 15`

`JimProductExtensions.OrderBy` (`Services/Catalog/JimProductExtensions.cs:24`) left-joins `LocalizedProperty`
for name sorting, and otherwise sorts on the column. `Position` on an already-ordered query is a no-op
(preserves the best-sellers order); otherwise it is `DisplayOrder, Id`.

`PrepareSortingOptionsAsync` (`BulkOrderModelFactory.cs:323`) builds the dropdown from
`CatalogSettings.ProductSortingEnumDisabled` / `ProductSortingEnumDisplayOrder`, and returns early when
`CatalogSettings.AllowProductSorting` is false.

> **Trap for the rebuild.** `PrepareSortingOptionsAsync` runs only under `loadLists: true`
> (`BulkOrderModelFactory.cs:1120`), i.e. on requests that do **not** load products; the request that *does*
> load products (`BulkOrderingItems`) takes `command.OrderBy` straight from the query string and casts it
> `(Jim2ProductSortingEnum)command.OrderBy` where `OrderBy` is `int?` (`BulkOrderServices.cs:1083`, `:1088`,
> `:1103`, `:1113`). A lifted `int? -> enum` cast throws `InvalidOperationException` when the value is null.
> The page's JS always appends `&OrderBy=` + the dropdown value, and `$('#bulk-orderby').val()` returns `null`
> when the select has no options — which is exactly the `AllowProductSorting == false` case
> (`BulkOrdering.cshtml`, `loadItems`). **[unverified]** — what settles it: set `catalogsettings.allowproductsorting`
> to false on a 4.6 store and load `/bulkordering`; either the item list 500s or model binding coerces the
> literal string `null`/empty to `0`.

---

## 5. Category navigation and visible categories

`BulkOrderCategoryNavigationViewComponent` (`Components/…`) parses `filterMode`, forces `orderId = 0` unless the
filter is `LatestOrdered` or `StockRefill`, and calls
`IBulkOrderModelFactory.PrepareCategoryNavigationModelAsync(categoryId, orderId, productId: 0, filtertype)`.

`PrepareCategoryNavigationModelAsync` (`BulkOrderModelFactory.cs:1050`) resolves the active category, then:

- `GetVisibleCategoryIdsByFilter(filtertype, storeId, orderId)` → the set of category ids that contain at least
  one product visible under the current filter, **plus all their ancestors**;
- `PrepareCategorySimpleModelsAsync(0, categoryIds)` (`:153`) builds the tree recursively from
  `GetAllCategoriesAsync(storeId)`, skipping any category not in the id set.

`GetVisibleCategoryIdsByFilter` (`BulkOrderServices.cs:1460`) is the only cached thing in the feature:

```
Key: BulkOrderDefaults.VisibleCategoryIdsByBulkOrderFilterCacheKey
     "Jim.visiblecategoryids.bybulkorderfilter.{filterType}-{customerRoleIds}-{storeId}-
      {contractedItemsCategoryId}-{contractOnly}-{orderId}-{customerId}"
Prefix: "Jim.visiblecategoryids.bybulkorderfilter."
```
(`Services/BulkOrder/BulkOrderDefaults.cs`.) `customerId` is included **only** when the filter is
`LatestOrdered` with `orderId == 0`; for every other filter it is `0`, so the entry is shared across customers
with the same role set. Note the cache key is not invalidated by any `ICacheEventConsumer` — it lives for the
default cache time and goes stale when products/categories change.

Inside, it builds the same base product query, applies one filter-specific shape
(`ApplySelectedOrderProductsQuery` / `ApplyLatestOrdersProductsQuery` / `ApplyBestSellersProductsQuery` /
`ApplyContractItemsOnlyConstraint` / `ApplyRefillStockProductsQuery`), applies store mapping + ACL + custom
visibility, projects the distinct `ProductCategory.CategoryId`, then walks parents in a `while` loop against
`_categoryRepository.GetAll(query => query, cache => default, false)` — **all categories, uncached**
(`BulkOrderServices.cs:1523`).

The navigation view (`Views/Shared/Components/BulkOrderCategoryNavigation/Default.cshtml`) renders a nested
`<ul>`, single-select, each link a full page navigation to `BulkOrdering` with `CategoryId`, `FilterMode`,
`IsMobile`; a `getView()` JS hook appends `ViewMode`, `orderId`, `PageSize` to the href on click.

---

## 6. Model graph

All under `Models/BulkOrder/`. All are `record`s.

### `BulkOrderCommand : BasePageableModel`
`OrderBy (int?)`, `FilterMode (string)`, `ViewMode (string)`, `OrderId (int)`, `IsMobile (bool)`, `CategoryId (int)`.
This is the single request object for every bulk-order read.

### `BulkOrderRouteValues : IRouteValues`
`CategoryId, FilterMode, ViewMode, OrderId, PageSize, PageNumber, OrderBy` — used by the nop `PagerModel` to
rebuild pager links against `BulkOrderDefaults.RouteActionName` (`"BulkOrderingPaged"`).

### `BulkOrderingModel : BasePageableModel` — the page
`CategoryId, OrderId, FilterMode, IsMobile, ViewMode, OrderBy, PagerModel, ShowImages, ImageSize (string, e.g. "300px"),
ShowQtyArrows, ShowProductStock`, plus `AvailableViewModes, AvailableOrders, AvailableFilters, PageSizeOptions,
AvailableSortOptions` (all `SelectListItem`) and `Items : IList<BulkOrderProductModel>`.

### `BulkOrderProductModel` — one row in the list
`ProductId, Sku, ImageUrl, Title, AlternateText, ProductName, ProductSeName, ProductPublished, ProductPrice (string),
ProductPriceValue (decimal), QuantityInCart (string, e.g. "3 in cart"), QuantityInCartValue (int),
ShowAttributes, ShowETAInfo, HasPreOrder, AvailableForPreOrder, ProductInfo : ProductAttributePriceAvailability,
AllowedQuantities, HasAttributes, Quantity`.

`HasAttributes` and `Quantity` are declared but never assigned by the factory
(`PrepareListItemModelAsync`, `BulkOrderModelFactory.cs:562`) and never read by `_BulkOrder.cshtml` — dead.

### `BulkOrderProductDetailsModel` — one product's matrix
`ProductId, Sku, ImageUrl, Title, AlternateText, ProductName, ProductSeName, ProductPublished,
ProductPrice, ProductPriceValue, ProductPriceRange, ShowAttributesPrice, SameAttributesPrice,
HasAvailableAttribute, TaxSuffix, ShowProductStock, ShowQtyArrows,
HorizontalAttribute / VerticalAttribute / SecondVerticalAttribute : BulkOrderAttributeModel,
QuantityInCart, AttributesInCart : IDictionary<string,int>, AttributeInfo : IDictionary<string,ProductAttributePriceAvailability>,
Warehouses : IList<string>, ShowETAInfo, AvailableForPreOrder, HasPreOrder,
ShowAttributeImages, AttributeImageSize, AttributeImageSquaresSizeValue, AttributeImageSquaresSize,
AllowedQuantities, Quantity, ViewMode`.

`Quantity` and `ViewMode` are never assigned — dead.

**The two dictionaries are the heart of the design.** Both are keyed by a composed string:

```
no attributes :  "{productId}"
1 attribute   :  "{productId}_{attr1MappingId}_{value1Id}"
2 attributes  :  "{productId}_{attr1MappingId}_{value1Id}-{attr2MappingId}_{value2Id}"
3 attributes  :  "{productId}_{attr1MappingId}_{value1Id}-{attr2MappingId}_{value2Id}-{attr3MappingId}_{value3Id}"
```

built at `BulkOrderModelFactory.cs:234`–`:240`. **The same string is the HTML input name**, prefixed
`itemquantity_` — see §11. That coupling (dictionary key = DOM id = form key = server parse key) is the
protocol of the whole feature.

### `BulkOrderAttributeModel : BaseNopEntityModel`
`Id` (= `ProductAttributeMapping.Id`), `ProductId`, `ProductAttributeId`, `Name`,
`Values : IList<ProductAttributeValueModel>` where the nested record carries
`Id, Name, ColorSquaresRgb, ImageSquaresPictureModel, HasImage, HasImageSquares, PictureModel,
PriceAdjustment (string), PriceAdjustmentValue`.

### `ProductAttributePriceAvailability`
`StockInfo : StockInfoModel`, `AttributePrice (decimal?)`, `AttributePriceFormatted`, `PreOrder`, `CallForPrice`,
`FirstPODate (string)`, `FirstPOQtys (int)`, `MaxAvailable (int)`.

### `BulkOrderBoxtModel` (sic — "Boxt")
The view-model for one quantity cell, constructed **in Razor**, not in the factory
(`_BulkOrderAttributes.cshtml:546`, `:809`, `:1035` — one per layout branch):
`ProductId, AttributeId (the composed key), IsItemInCart, ItemTitle, ItemTitleValue, StockInfo, ShowQtyArrows,
ShowProductStock, AttributePrice, ShowAttributesPrice, ShowETAInfo, AttributeETA, AttributeETADate,
CallForPrice, PreOrder, AvailableForPreOrder, MaxAvailable, Warehouses, PictureModel, ShowAttributePicture,
AttributePictureSize, AllowedQuantities`.

---

## 7. Attribute-role resolution (which attribute becomes which axis)

Three settings name the axes: `HorizontalAttributeId`, `VerticalAttributeMainId`, `VerticalAttributeSecId`
(these are `ProductAttribute` ids, not mapping ids).

`MainAttribute` (`BulkOrderServices.cs:1156`):
```
if product has HorizontalAttributeId        -> that
if product has VerticalAttributeMainId      -> that
if product has VerticalAttributeSecId       -> that
if mappings != null && mappings.Count <= 3  -> mappings.FirstOrDefault().ProductAttributeId
else 0
```
`SecondAttribute` (`:1175`) and `TerciaryAttribute` (`:1207`) fill the remaining axes, first from the configured
ids, then by taking the next unused mapping when `Count <= 3`.

Consequences, all load-bearing:

- **The matrix is hard-capped at three attributes.** `ShowBulkOrderingInProductPageAsync` rejects `> 3`
  outright (`:1381`), and `SetBulkModelItems` only builds a row model when
  `productAttributeMappings.Count < 4` (`BulkOrderModelFactory.cs:384`, `:414`). A product with 4 authorised
  attributes is **silently dropped from the list** — it is counted in the paged total but produces no row, so
  a page of 15 can render 12 items with no explanation.
- The fallback branches use `mappings.Count <= 3` with no null guard in `SecondAttribute`/`TerciaryAttribute`
  (`MainAttribute` has one). `MainAttribute` also does `FirstOrDefault().ProductAttributeId` — an NRE on an
  empty list. Every current call site guards `Count > 0` first, so it is latent, not live.
- Attribute *ordering* on the axes comes from the settings, not from `ProductAttributeMapping.DisplayOrder`.
  Two products that use the same three attributes always lay out the same way; a product using three
  unconfigured attributes lays out in mapping order, which is `Id` order from
  `GetProductAttributeMappingsByProductIdAsync`.

Only attributes and values the customer is authorised for are used:
`_jimProductAttributeMappingService.AuthorizeAsync(mapping.Id)` (`BulkOrderModelFactory.cs:382`, `:412`, `:668`) and
`_jim2ProductAttributeValueService.AuthorizeAsync(value.Id)` (`:887`).

`IsBulkOrderingCompatible` (`Services/Catalog/JimProductAttributeExtensions.cs:33`) accepts only
`DropdownList`, `ColorSquares`, `ImageSquares`, `RadioList`. **It is used only by
`ShowBulkOrderingInProductPageAsync`** — the bulk *list* does not check it, so a product whose attribute is a
`TextBox` or `Checkboxes` still renders a matrix on `/bulkordering`, built from `ShouldHaveValues()`
(`BulkOrderModelFactory.cs:880`), which for a textbox yields no values.

Optional (non-required) attributes get an extra **empty** value appended so the matrix has a
"no selection" column (`BulkOrderModelFactory.cs:928`).

---

## 8. Building one product's matrix — `PrepareBulkOrderProductDetailsModelItem`

`BulkOrderModelFactory.cs:663`. Sequence:

1. Load authorised attribute mappings.
2. Load the customer's cart for the store; if a **packname** is selected
   (`_packNameContext.GetCurrentPackNameAsync`), also load `GetAllShoppingCartAsync` — the cart across all
   packnames — because stock must be checked against everything already reserved, not just this packname's lines.
3. **Warm the Jim2 caches** (`:687`–`:711`): for every `ProductAttributeCombination` of the product, look up the
   Jim2 variant by combination SKU and, if not already cached, add the SKU to a batch for
   `SetCacheProductListPriceByCustomerAsync` / `SetCacheProductListJim2StockQuantityAsync`. These are dispatched
   as `Task.Run(...)` and awaited with `Task.WhenAll`.
4. Build the shell model (SKU, name, se-name, `ShowQtyArrows = !HideQtyArrows` (`:721`), `ShowProductStock` (`:722`),
   `ShowAttributeImages`, image sizes).
5. `GetFinalPriceAsync(product, customer, store, 1)` → the product-level price (tier-price aware, taxed,
   currency-converted) — `BulkOrderServices.cs:1329`.
6. `quantityInCart` = sum of quantities of this product in `fullCart ?? cart`.
7. If there are attributes → `PrepareProductAttributeModelsAsync` (the private at
   `BulkOrderModelFactory.cs:852`); else build the single `AttributeInfo["{productId}"]` entry directly.
8. Pre-order flag; `ParseAllowedQuantities(product)` → `AllowedQuantities` (nop's `Product.AllowedQuantities`
   CSV; when non-empty the quantity input becomes a `<select>`).

### 8.1 The cartesian walk

`PrepareProductAttributeModelsAsync` (`:852`) builds `BulkOrderAttributeModel`s for all mappings, sorts them
into main / secondary / tertiary buckets, then walks:

```
for each value of HorizontalAttribute
  for each value of VerticalAttribute            (if present)
    for each value of SecondVerticalAttribute    (if present)
       -> PrepareProductAttributeModelsAsync(overload at :200)
```

and the overload, **per cell**:

- composes `attributesXml` by calling `AddProductAttributeToXMLAsync` once per axis
  (`BulkOrderServices.cs:1235`), which itself does `GetProductAttributeMappingByIdAsync` +
  `IsConditionMetAsync` + `AuthorizeAsync`;
- computes `quantityInCart` for that exact combination via
  `FindAllShoppingCartItemInTheCartAsync` (packname mode) or `FindShoppingCartItemInTheCartAsync`;
- composes the `attributeID` key;
- calls `ProductPriceAvailavility(product, store, customer, attributesXml, quantityInCart, productPrice)`;
- formats the price, widens `priceRange`, and sets `showAttributesPrices = true` if the cell price differs from
  the product price;
- ORs into `hasAvailableAttribute`:
  `!product.CallForPrice && product.Published && !cell.CallForPrice && (cell.StockInfo.IsInStock || cell.PreOrder)
   && (cell.MaxAvailable > 0 || AllowOutofStock)`.

**Cost.** For a product with 10 colours × 12 sizes × 3 fits the walk is 360 cells; each cell performs 3
attribute-XML compositions, a combination lookup (`GetProductAttributeCombinationAsync` parses every
combination's XML), a cart scan, a price calculation and a stock calculation. This is the dominant cost of the
feature and the main thing the rebuild has to change.

### 8.2 Price-display collapse

After the walk (`:1009`–`:1046`):

- If cell prices vary from the product price but **all cells agree with each other**
  (`priceRange.From == priceRange.To`), the model sets `SameAttributesPrice = true`, puts the single value in
  `ProductPriceRange`, and turns per-cell price display back off.
- Otherwise `ShowAttributesPrice` stays on; if `Jim2DefaultSettings.ShowProductAttributesPrice` is **off**, the
  per-cell price column is suppressed and a `"from – to"` range string is shown at product level instead.
- `TaxSuffix` comes from `IJimPriceFormatterService.GetProductTaxSuffixAsync(product.IsTaxExempt)`.

Warehouse column headers are the distinct union of warehouse names across all cells
(`:1005`), gated on `ShowStockQuantity() && ShowQtyPerWarehouse`.

---

## 9. Price and availability per cell — `ProductPriceAvailavility`

`BulkOrderServices.cs:1255`.

**Price.** If `attributesXml` is non-empty, find the `ProductAttributeCombination`. If found → `GetUnitPriceAsync`
→ `ITaxService.GetProductPriceAsync` → `ConvertFromPrimaryStoreCurrencyAsync`. If **not** found → the cell price
is just the product price (no attribute adjustment applied). No attributes → product price.

`GetUnitPriceAsync` (`:806`) is a **copy of nop's `ShoppingCartService.GetUnitPriceAsync`** modified to take the
combination directly (to skip re-resolving it from XML). Its shape:

- `product.CustomerEntersPrice` → the entered price;
- else `IJimProductPriceService.GetProductsPriceByCustomerAsync(product, customerId, sku, quantity)` — the Jim2
  customer-specific price, keyed on the **combination SKU** when present. If it returns a value, that value is
  passed to `GetFinalPriceAsync` as the base;
- else sum `GetProductAttributeValuePriceAdjustmentAsync` over the parsed attribute values, plus a scan for
  `TextBox` attributes whose values have an `AssociatedProductId` and pass
  `IJim2ProductAttributeValueService.AuthorizeAsync`, then `GetFinalPriceAsync`;
- `ShoppingCartSettings.GroupTierPricesForDistinctShoppingCartItems` → quantity for tier pricing is the total of
  that product already in the cart;
- rounding under `ShoppingCartSettings.RoundPricesDuringCalculation`.

> This is exactly the "copied nop method body" hazard `CLAUDE.md §10.1` describes. If the rebuild copies it
> again it must be listed there.

**Availability** — switch on `product.ManageInventoryMethodId` (`:1290`):

| Method | Handler | Note |
|---|---|---|
| `ManageStock` | `GetStockForProductAsync` (`:200`) | **only when `BackorderMode == NoBackorders`.** With backorders allowed nothing runs, so `StockInfo.IsInStock` stays `false` and the cell renders disabled. |
| `ManageStockByAttributes` | `GetStockForProductAttributesAsync` (`:221`) | uses `combination.StockQuantity`. |
| `Jim2ManageInventoryMethod.Jim2Inventory` | `GetStockMessageForJim2AttributesAsync` (`:249`) | the live-stock path: warehouses, PO/ETA. |
| default (`DontManageStock`) | nothing | no stock info at all. |

If `!product.Published`, availability is skipped entirely and `StockInfo.StockMessage = "UP"` — the sentinel the
views test for "unpublished" (`_BulkOrder.cshtml:109`, `_BulkOrderBox.cshtml:12`).

`VerifyCallForPriceAndPreOrder` (`:156`) is the shared decision:

```
if product.CallForPrice                              -> CallForPrice = true
else if price is null/0 and HandledNoPrice           -> CallForPrice = true
else if !inStock and ShowETA and PreOrderOutOfStock
        and inETA > 0                                -> PreOrder = true; MaxAvailable = inETA - qtyInCart
else if !inStock and HandledNoStock                  -> CallForPrice = true

if not CallForPrice and not PreOrder and !AllowOutofStock:
    MaxAvailable = inStock - qtyInCart
    if ShowETA and PreOrderOutOfStock: MaxAvailable += inETA
```

Note `MaxAvailable` stays `0` when `AllowOutofStock` is on — the views then rely on `AllowOutofStock` itself to
keep the input enabled.

> **Observed defect.** `GetStockForProductAttributesAsync` (`:221`) ends with an *unconditional*
> `productAttributeAvailability.CallForPrice = product.CallForPrice;` (`:243`), after
> `VerifyCallForPriceAndPreOrder` has already decided. So for `ManageStockByAttributes` products the
> `HandledNoPrice` / `HandledNoStock` rules are computed and then thrown away. Confirmed by reading; the two
> other inventory paths do not do this.

`GetStockMessageForJim2AttributesAsync` (`:249`) is the richest path:

- returns immediately if `!product.DisplayStockAvailability`;
- gets `GetAllActiveWarehousesAsync`, then `GetProductJim2StockQuantity` → `GetStockQuantities` / `GetPOList` →
  `GetStockQty` / `GetETAQty`;
- `stockQty > 0` → either the per-warehouse breakdown (`GetStockMessageWithQuantity`, `:394`, when
  `DisplayStockQuantity && ShowStockQuantity()`), or the `InStockText` / `Products.Availability.InStock` label;
- `stockQty == 0` → if `AllowOutofStock`, shows `NoStockText` **and still sets `IsInStock = true`**; else
  `IsInStock = false` and the message is `NoStockText` or the literal `"0"`;
- `SetETAData` (`:378`) puts the first PO date (formatted through the resource
  `Jim2.ShoppingCart.BulkAddToCart.ETA.POItem.InfoText` = `"{0:dd/M/yy}"`) and its quantity on the model;
- when `combination == null` and required attributes are not all selected, it returns early or checks whether any
  existing combination covers the selected ids, honouring
  `CatalogSettings.AttributeValueOutOfStockDisplayType`.

---

## 10. Cart and packname awareness

- The list row (`PrepareListItemModelAsync`, `BulkOrderModelFactory.cs:562`) resolves the cart **per product**:
  `GetShoppingCartAsync`, `GetCurrentPackNameAsync`, and `GetAllShoppingCartAsync` when a packname is selected.
  For 15 products that is 15–45 cart resolutions on one request.
- `QuantityInCart` is a **pre-formatted English string** — `quantityInCart + " in cart"`
  (`:609`, `:757`) — not localised, produced in the factory rather than the view.
- The same happens in `_BulkOrderAttributes.cshtml` for cell titles: `"Items in cart " + valueAttInCart`
  (`:173`, `:565`, `:828`, `:1055`), built in Razor.
- The whole point of `GetAllShoppingCartAsync` is that when ordering **on behalf of a packname**, availability
  must be netted against every packname's reservations, not just the current one.

**How the packname actually gets onto the cart line.** Not through anything in the bulk-order code. The bulk add
calls plain `IShoppingCartService.AddToCartAsync` with no packname argument
(`Jim2ShoppingCartController.cs:303`, `:353`, `:403`, `:444`); `ShoppingCartOverrideService.AddToCartAsync`
(`Services/Orders/ShoppingCartOverrideService.cs:2449`) reads
`IPackNameContext.GetCurrentPackNameAsync(customer)` (`:2462`) and inserts a `PackNameCartItem` against the new
line (`:3093`). `GetShoppingCartAsync` (`:2668`) and `GetAllShoppingCartAsync` (`:2725`) are overridden the same
way. So bulk ordering inherits packname behaviour from the cart service exactly as any other add-to-cart path
does, and needs no packname handling of its own — `CW.Core` has the equivalent override
(`Services/Orders/ShoppingCartOverrideService.cs`), so this carries over unchanged.

---

## 11. Views

| File | Lines | Renders |
|---|---|---|
| `Views/BulkOrder/BulkOrdering.cshtml` | 346 | Page shell: title, view-mode toggle, order dropdown, filter dropdown, sort dropdown, page-size dropdown, two **Add all to Cart** submit buttons, category nav (`side-2`), empty `#itemsContainer`, three hidden forms, and ~200 lines of inline JS. |
| `Views/BulkOrder/_BulkOrder.cshtml` | 279 | One `.product-item` per `BulkOrderProductModel`: image, name, SKU, price (+ RRP / % saved), "n in cart", then either the no-attribute quantity block or a `.attributes-loader` placeholder plus the AJAX call for the matrix. Then the pager and `validateqtyvalue`. |
| `Views/BulkOrder/_BulkOrderAttributes.cshtml` | 1115 | The matrix. Contains a hidden `#product-price-data-{id}` block whose HTML is copied over `#product-info-price-{id}` on ready (`:1113`) — the price is recomputed here and patched into the row. |
| `Views/BulkOrder/_BulkOrderBox.cshtml` | 128 | One quantity cell: title, optional attribute picture, quantity input or `<select>`, ± arrows, stock value(s), price, ETA. |
| `Views/BulkOrder/_StockMessage.cshtml` | 59 | Stock value; if the message is longer than 10 characters it becomes a click-to-open tooltip (`stock-tooltip-container`) with its own inline `$(document).ready` handler per cell. |
| `Views/Shared/Components/BulkOrderCategoryNavigation/Default.cshtml` | 115 | Recursive category `<ul>` via a local `@functions` block. |
| `Views/Product/_BulkProductAttributes.cshtml` | 833 | The product-page variant of the matrix. |
| `Views/Product/_BulkAddToCart.cshtml` | 17 | The product-page "Add to Cart" button, wired to `AjaxBulkCart.addbulkproducttocart_details`. |

CSS: `Content/BulkOrder/bulk-order.css` (1139 lines, 7 media queries, mostly flexbox with 2 grid uses) registered
in `BulkOrdering.cshtml:20`; `Content/BulkOrder/bulk-order-product.css` (618 lines) registered in
`Views/Product/ProductTemplate.Simple.cshtml:22`.

### 11.1 The disabled rule

Repeated verbatim in three places (`_BulkOrder.cshtml:113`, `_BulkOrderAttributes.cshtml:186`,
`_BulkOrderBox.cshtml:13`):

```csharp
itemIsUnpublish = StockMessage == "UP"
itemDisabled = itemIsUnpublish
            || CallForPrice
            || (!IsInStock && !PreOrder)
            || (MaxAvailable <= 0 && !AllowOutofStock)
```

and the add-to-cart button choice:

```csharp
if (!ProductPublished)      -> "This product is not available anymore"
else if (CallForPrice)      -> button posting to AddBulkProductToWishList-Details
else if (InStock || PreOrder || AvailableForPreOrder) -> "Add to cart" (or "Pre-order")
else                        -> nothing at all
```

Three copies of a five-clause boolean is the single clearest candidate for a computed model property in the
rebuild.

### 11.2 Structural duplication in `_BulkOrderAttributes.cshtml`

The file has three nearly identical layout branches — 3-attribute, 2-attribute, 1-attribute — and inside each
branch the `attribute-title-column` block is **nested inside itself three times** (`:385`, `:440`, `:490`;
`:648`, `:703`, `:753`; `:874`, `:929`, `:979`). The nesting is not conditional; it emits the same header column three times,
one inside another, on every render. The `no-abbr-title` / `abbr-title` class pairs then hide/show the copies by
media query. Roughly 700 of the file's 1115 lines are this duplication.

---

## 12. Client side

### 12.1 Files

| File | Registered where | Purpose |
|---|---|---|
| `lib/public.ajaxbulkcart.js` (170 lines) | `Views/Shared/_HeaderVoyage.cshtml:55` — i.e. **on every page of the theme** | `AjaxBulkCart` object: `addbulkproducttowish_details`, `addallbulkproducttowish_details`, `addbulkproducttocart_details`, `success_process`. |
| `lib/public.productquantitybulk.js` (57 lines) | `Views/Shared/_HeaderVoyage.cshtml:57` | `ProductQuantityBulk.init(parentSelector)` — binds `.increase-bulk` / `.decrease-bulk` to the sibling `.qty-input`. |

Two things to know about `public.ajaxbulkcart.js`:

- **`AjaxBulkCart.init(...)` is never called anywhere** (verified by grep across `.cs`, `.cshtml`, `.csproj`).
  Its four configuration fields therefore stay at their defaults.
- `success_process` reads `AjaxCart.topcartselector`, `AjaxCart.topwishlistselector`,
  `AjaxCart.flyoutcartselector` and `AjaxCart.usepopupnotifications` — nop core's object, **not its own**
  (`:113`–`:141`). It works only because core's `AjaxCart` is initialised by the layout. `ajaxFailure` does
  `alert(this.localized_data.AjaxCartFailure)` where `this` is the ajax settings object, so
  `localized_data` is undefined there.

### 12.2 State in the URL

`BulkOrdering.cshtml` keeps `PageNumber, PageSize, ViewMode, CategoryId, FilterMode, OrderId, OrderBy` in the
query string via `history.replaceState` (`bulkOrderingURLBuilder`, `:140`; `setUrlParams`, `:188`). Changing
page size, sort, or view mode re-runs `loadItems()` (AJAX); changing **filter** or **order** does a full
`location.href` navigation (`:327`, `:337`).

`isMobileView()` is `matchMedia("(max-width: 900px)")`; on mobile the view mode is forced to `tiled` and a
resize handler switches back on widening (`:266`).

### 12.3 The three forms

```html
<form asp-route="BulkOrderAll" id="send-bulkform"          method="post">  <!-- the visible page + its qty inputs -->
<form asp-route="BulkOrderAll" id="send-bulkformproducts"  method="post">  <!-- hidden; used for single-product add -->
<form asp-controller="Jim2ShoppingCart" asp-action="BulkOrderWishlist" id="send-wishlist-products" method="post">
```

- **Add all to Cart** = plain submit of `send-bulkform` (button `name="addtocartbutton"`), a full page POST.
- **Add to cart on one product** = `addItemsToCart(productId)` (`BulkOrdering.cshtml:166`), which copies the *visible*
  `itemquantity_*` inputs of that product into the hidden form and clicks its submit button — **also a full page
  POST**, not AJAX. The AJAX helpers are used only on the product-details page and for the call-for-price →
  wishlist path.

> **Observed defect (selector).** `addItemsToCart` uses
> `$('*[id*=itemquantity_' + productId + ']:visible')` — a *contains* match. For `productId = 12` the selector
> also matches `itemquantity_120_…`, `itemquantity_123_…` and so on, so pressing "Add to cart" on product 12
> submits the quantities typed against every product whose id starts with `12` and is on the same page.
> The same pattern appears in `public.ajaxbulkcart.js:126` for clearing inputs after a successful add.
> **[unverified in a live store]** — what settles it: on a 4.6 store put two products on the same bulk page
> whose ids are e.g. 12 and 123, type a quantity against both, press Add to cart on 12, and see whether 123 is
> added too.

### 12.4 Quantity validation

`validateqtyvalue(val, elementId, maxAvailable)` (`_BulkOrder.cshtml:267`) adds `.max-exceed` /
`.product-max-exceed` classes when the typed quantity exceeds `MaxAvailable`, revealing the
`Jim2.ShoppingCart.BulkQuantityExceedsStock` message. It is **display only** — nothing blocks submission, and
the handler is attached only on the branch where `AllowOutofStock` is false and there are no
`AllowedQuantities` (`_BulkOrder.cshtml:189`, `_BulkOrderAttributes.cshtml:262`, `_BulkOrderBox.cshtml:61`).

---

## 13. Add to cart — the form-key protocol

Server side: `Controllers/Jim2ShoppingCartController.cs`.

### 13.1 `AddItemsToCartFromBulkOrderList` (private, `:236`)

Iterates **every key** of the posted `IFormCollection`, keeps those containing `itemquantity_`, parses the
integer value, skips `<= 0`, then splits the remainder of the key:

```
key.Replace("itemquantity_","")  ->  "12_5_77-6_91-7_15"
split on '-'   -> ["12_5_77", "6_91", "7_15"]
segment count 3+ -> productId=12, attr1=5,val1=77, attr2=6,val2=91, attr3=7,val3=15
segment count 2  -> two attributes
segment count 1  -> split on '_':  length 1 -> no attributes; length >= 2 -> one attribute
```

For each parsed item it composes `attributesXml` with `AddProductAttributeAsync` and calls
`AddToCartAsync` (or `AddToQuoteAsync` for `Quotelist`) with `addRequiredProducts: true`, counting successes and
accumulating warnings. For the **no-attribute** case it first builds an XML of all `IsPreSelected` attribute
values (`:385`).

Key properties of this design:

- **Not atomic.** Items are added one at a time; a failure part-way leaves everything before it in the cart.
- **No total-quantity or item-count guard.**
- Warnings from all items are concatenated into one flat list, not attributed to the item that produced them.
- The `addtocart` form field (a comma-separated product id list emitted by `_BulkOrder.cshtml:25`) is parsed into
  `allIdsToAdd` at `:242` and then **never used**.
- Segment parsing indexes `IDsList[1]` / `IDsList[2]` without bounds checks; a malformed key throws.
- The whole method is **duplicated verbatim** as `AddItemsToCartFromFavouritesList` (`:469`), differing only in
  that it never takes the quote branch.

### 13.2 Public actions

| Action | Line | Cart type | Result |
|---|---|---|---|
| `BulkOrderAll` → `AddItemsToCartFromBulkOrderList(Guid?, IFormCollection)` | 2233 | ShoppingCart | Notifications, then `RedirectToAction("BulkOrdering", …)` preserving `FilterMode`, `ViewMode`, `orderId`, `CategoryId`. On zero added → `RedirectToRoute("BulkOrdering")` (filters lost). |
| `BulkOrderWishlist` → `AddItemsToWishlistFromBulkOrderList` | 2292 | Wishlist | Redirects to `/product-enquiry` on success. Reads the filter from form key `filter-mode` (the page posts `FilterMode`; see below). |
| `AddBulkProductToWishList_Details` | 2396 | Wishlist | JSON. |
| `AddBulkProductsToWishList_Details` | 2453 | Wishlist | JSON with updated wishlist header count. |
| `AddBulkProductToQuote_Details` | 2518 | Quotelist | JSON. |
| `AddBulkProductToCart_Details` | 2573 | ShoppingCart | JSON; activity log `PublicStore.AddToShoppingCart`; honours `DisplayCartAfterAddingProduct`; returns updated top-cart and flyout-cart HTML. |

All are `[HttpPost]`; the two list actions add `[FormValueRequired("addtocartbutton")]`. Permission gate on the
list actions is `StandardPermissionProvider.EnableShoppingCart`. The `customerGuid` parameter is resolved to a
`pageCustomer` and then **not used** — the add always runs against `_workContext.GetCurrentCustomerAsync()`
(`:2242`).

> **Inconsistency.** `AddItemsToWishlistFromBulkOrderList` reads `form["filter-mode"]` (`:2311`) while
> `AddItemsToCartFromBulkOrderList` reads `form["FilterMode"]` (`:2266`). The page posts a hidden input named
> `FilterMode` (`BulkOrdering.cshtml:93`, `:112`) *and* a select named `filter-mode` (`:73`) — both are present in
> `send-bulkform`, so both happen to resolve. `send-bulkformproducts` posts only `FilterMode`.

---

## 14. Bulk ordering on the product details page

Gate: `ShowBulkOrderingInProductPageAsync(productId)` (`BulkOrderServices.cs:1355`) returns false when **any** of:

1. `IJim2ProductService.IsBulkOrderLocked(productId)` — per-product override
   `Jim2Product.LockBulkOrderOverride ? LockBulkOrder : !EnableStandardProductBulkOrder`
   (`Services/Products/Jim2ProductService.cs:4333`; columns added by
   `Data/Migrations/Products/Jim2ProductLockBulkOrderFieldVersion.cs`);
2. `!ShowBulkOrderingAsync(customer)`;
3. the product has any tier price with `Quantity > 1`;
4. authorised attribute mappings count is `0` or `> 3`;
5. any authorised mapping fails `IsBulkOrderingCompatible()`;
6. `MainAttribute(...)` resolves to `0`.

`ProductTemplate.SimpleVoyage.cshtml:43` calls it, and when true swaps `_ProductAttributes` →
`_BulkProductAttributes`, `_BasicAddToCart` → `_BulkAddToCart`, `_AddToWishlist` → `_BulkAddToWishlist`,
`_AddToQuote` → `_BulkAddToQuote`, and suppresses `_Availability`. `CallForPrice` for the button is
`Model.ProductPrice.CallForPrice && !bulkProductModel.HasAvailableAttribute`.

Note the gate is enforced **only in the Voyage product template**. `ProductTemplate.Simple.cshtml` registers the
CSS but does not branch.

`SendUserToBulkOrdering` (`Jim2DefaultSettings:399`) redirects a qualifying customer to `/bulkordering` after
login — used at `Jim2WebLogonController.cs:1463`, `:1500` and
`Services/Customers/Jim2CustomerRegistrationService.cs:133`.

---

## 15. Settings

All live on `Jim2DefaultSettings` (`Domain/Settings/Jim2DefaultSettings.cs`, `#region Bulk Order`, `:367`–`:410`),
are per-store overridable, and are edited on the Jim2 **store settings** page
(`Views/Settings/StoreSettings.cshtml`, model `Models/Settings/Jim2StoreSettingsModel.cs:640`, saved in
`Controllers/Admin/Jim2SettingController.cs`).

| Setting | Type | Read at | Effect |
|---|---|---|---|
| `EnableBulkOrdering` | bool | `BulkOrderServices.cs:1120` | master switch |
| `BulkOrderRoleIds` | `List<int>` | `:1123` | empty = all customers; else role intersection |
| `BulkOrderingView` | int | `:1132` | `0` → `"tiled"`, else `"list"` — default view mode |
| `BulkPageImageSize` | int | `:1139` | product image px, default `300` |
| `ShowProductImages` | bool | factory `:1153` | show product images on the list |
| `ShowAttributeImages` | bool | factory `:723` | show a picture per attribute value (**only for the attribute literally named `Jim2AttributeNames.Colour`**, factory `:912`) |
| `AttributeImageSize` | int | `:1144` | px, default `100` |
| `AttributeImageSquaresSize` | int | `:1149` | px, default `MediaSettings.ImageSquarePictureSize`; the view floors it at `100px` (`_BulkOrderAttributes.cshtml:302`) |
| `HideQtyArrows` | bool | factory `:721`, `:1160` | inverted into `ShowQtyArrows` |
| `ShowProductAttributesPrice` | bool | factory `:1024`, views | show the per-cell price column; when off a price *range* is shown at product level |
| `ShowProductStock` | bool | factory `:722`, `:1161` | show availability column |
| `BulkorderingShowETA` | bool | factory `:258`, `:616`, `:766` | show ETA date/qty (also needs `FirstPODate` non-empty and `FirstPOQtys > 0`) |
| `HorizontalAttributeId` | int | `:1158` | first matrix axis (columns) |
| `VerticalAttributeMainId` | int | `:1161`, controller `:154` | second axis (rows); also switches the page-size option list |
| `VerticalAttributeSecId` | int | `:1164` | third axis |
| `SendUserToBulkOrdering` | bool | logon services | post-login redirect |
| `EnableStandardProductBulkOrder` | bool | `Jim2ProductService.cs:4341` | default for the product-page bulk view (per-product override on `Jim2Product`) |

Also consumed by bulk ordering but owned elsewhere in the settings class: `AllowOutofStock`, `NoStockText`,
`InStockText`, `HandledNoPrice`, `HandledNoStock`, `PreOrderOutOfStock`, `ShowQtyPerWarehouse`,
`ShowStockQty` / `ShowStockQtyCustomerRoleIds`, `ContractOnlySupport`.

---

## 16. Localisation keys

Installed in `Jim2Plugin.cs:1668`–`:1706`:

```
Jim2.Bulk.Ordering                     "Bulk Ordering"
Jim2.Bulk.Ordering.Quantity            "Quantity"          Jim2.Bulk.Ordering.MinQuantity   "Qty"
Jim2.Bulk.Ordering.Availavility        "Availability"      Jim2.Bulk.Ordering.MinAvailavility "Avl"
Jim2.Bulk.Ordering.Price               "Price"
Jim2.Bulk.Ordering.ETADate             "ETA Date"          Jim2.Bulk.Ordering.ETAQty        "Comming Qty"
Jim2.Bulk.Ordering.MinETA              "ETA"               Jim2.Bulk.Ordering.MinETAFormat  "{0} by {1}"
Jim2.Bulk.Ordering.Unpublish           "This product is not available anymore"
Jim2.Bulk.Ordering.PreOrder            "Items with no availability will be pre-ordered"
Jim2.BulkOrder.AddAllToCart            "Add all to Cart"   Jim2.BulkOrder.AddToCart         "Add to Cart"
Jim2.BulkOrder.AddToCart.{Success|Error|NoAddedItems}
Jim2.BulkOrder.AddToWishlist.{Success|Error|NoAddedItems}
Jim2.BulkOrder.AddToQuote.{Error|NoAddedItems}
Jim2.BulkOrder.ProductHasBeenAddedToTheQuotelist.Link
Jim2.ShoppingCart.BulkAddToCart        "Add to Cart"
Jim2.ShoppingCart.BulkAddToCart.ETA.POItem.InfoText   "{0:dd/M/yy}"
Jim2.ShoppingCart.BulkQuantityExceedsStock            "Your quantity exceeds stock on hand"
```

Note the misspellings baked into the key names (`Availavility`, `Comming`) and that `MinETAFormat` is installed
but never used (the view concatenates `"@Model.AttributeETA by @Model.AttributeETADate"` inline,
`_BulkOrderBox.cshtml:126`). Several user-visible strings are **not** localised at all: `"View as"`, `"Orders"`,
`"View"`, `"No products to show"`, `"All Products"`, `"Select OrderId - MM/dd/yy"`, `"Contract Items Only"`,
`"Latest ordered products"`, `"Best Sellers"`, `"Add to cart"`, `"n in cart"`, `"Items in cart n"`, `"Categories"`
(hardcoded in `BulkOrdering.cshtml`, `BulkOrderModelFactory.SetAvailableFilters`/`SetAvailableOrders`,
`_BulkOrder.cshtml`, `Default.cshtml`).

---

## 17. Caching

| Key | Defined | Scope |
|---|---|---|
| `Jim.visiblecategoryids.bybulkorderfilter.{filter}-{roles}-{store}-{contractCatId}-{contractOnly}-{orderId}-{customerId}` | `BulkOrderDefaults.cs` | category tree contents; no event-based invalidation |
| `Jim.pres.bulkorder.productattribute.image.picture.{pictureId}-{secure}-{store}-{size}` | `Infrastructure/Cache/Jim2ModelCacheDefaults.cs:345` | attribute-value picture model |
| `Jim.pres.bulkorder.productattribute.imagesquare.picture.{pictureId}-{secure}-{store}-{size}` | `:358` | image-squares picture model |

Everything else — prices, stock, cart lookups, attribute models — is recomputed on every request. The Jim2
live-stock and customer-price caches (`IJimProductPriceService`, `IProductOverrideService`) are separate and are
*warmed* by the factory (§8 step 3) rather than owned by bulk ordering.

---

## 18. Dependency injection

`Infrastructure/DependencyRegistrar.cs:305`:

```csharp
services.AddScoped<IBulkOrderServices, BulkOrderServices>();
services.AddScoped<IBulkOrderModelFactory, BulkOrderModelFactory>();
```

`BulkOrderServices` takes **31 constructor dependencies**; `BulkOrderModelFactory` takes **28**. Both are single
classes covering bulk ordering *and* refill.

---

## 19. Observed defects and fragilities

Each entry states the file and what was read. Items marked **[unverified]** name the check that would settle them.

| # | Where | What |
|---|---|---|
| D1 | `BulkOrderServices.cs:243` | `GetStockForProductAttributesAsync` overwrites `CallForPrice` unconditionally after `VerifyCallForPriceAndPreOrder`, discarding the `HandledNoPrice`/`HandledNoStock` decisions for `ManageStockByAttributes` products. |
| D2 | `BulkOrderServices.cs:1290` | `ManageStock` products with `BackorderMode != NoBackorders` get **no** stock computation, so every cell renders disabled. |
| D3 | `BulkOrdering.cshtml:164`, `public.ajaxbulkcart.js:126` | `id*=itemquantity_{productId}` is a substring match; product 12 also matches products 120/123/… on the same page. **[unverified in a live store]** |
| D4 | `BulkOrderServices.cs:1083` etc. | `(Jim2ProductSortingEnum)command.OrderBy` on an `int?` throws when null; the normalising code runs only on the request that does not load products. **[unverified]** |
| D5 | `BulkOrderModelFactory.cs:394`, `:419` | Products with ≥4 authorised attributes are counted in the paged total but silently produce no row. |
| D6 | `BulkOrderController.cs:183, 194, 212` | `BulkOrderingItems`, `BulkOrderingPaged` and `BulkOrderingItem` do not call `ShowBulkOrderingAsync`; `BulkOrderingItem/{id}` additionally applies no store-mapping/ACL/visibility check to the product it renders. |
| D7 | `BulkOrderController.cs:196` | `pageNumber.Value` dereferenced without a null check. |
| D8 | `Jim2ShoppingCartController.cs:242`, `:2242` | `allIdsToAdd` and `pageCustomer` are computed and never used. |
| D9 | `Jim2ShoppingCartController.cs:236` vs `:469` | `AddItemsToCartFromBulkOrderList` and `AddItemsToCartFromFavouritesList` are ~230 duplicated lines each. |
| D10 | `Jim2ShoppingCartController.cs:2266` vs `:2311` | `FilterMode` vs `filter-mode` form key mismatch between the cart and wishlist actions. |
| D11 | `_BulkOrderAttributes.cshtml` | `attribute-title-column` is nested inside itself three times per branch, unconditionally, and the whole layout exists in three near-identical copies. |
| D12 | `public.ajaxbulkcart.js` | `AjaxBulkCart.init` is never called; `success_process` reads `AjaxCart.*` globals; `ajaxFailure` dereferences `this.localized_data` on the ajax settings object. |
| D13 | `_HeaderVoyage.cshtml:55,57` | Both bulk scripts are registered on **every** page of the theme, not only on bulk-order pages. |
| D14 | `BulkOrderModelFactory.cs:382` + `:412` | `GetProductAttributeMappingsByProductIdAsync` + per-mapping `AuthorizeAsync` runs **twice per product per request** (once in the cache-warming loop, once in the model loop). |
| D15 | `BulkOrderModelFactory.cs:562` | Cart, packname and full-cart are resolved **per product**, inside `PrepareListItemModelAsync`, rather than once per request. |
| D16 | `BulkOrderModelFactory.cs:389`–`:406`, `:696`–`:709` | Scoped services (`IJimProductPriceService`, `IProductOverrideService`) are invoked from `Task.Run` in parallel. Scoped data-access components are not thread-safe; whether this bites depends on what those implementations touch. **[unverified]** — what settles it: check whether either implementation resolves a LinqToDB `DataConnection`/`IRepository` on the calling scope. |
| D17 | `BulkOrderModelFactory.cs:1085` | `Enum.TryParse` result discarded — an unknown `FilterMode` yields an empty list with no message. |
| D18 | `BulkOrderServices.cs:1170` | `MainAttribute` does `FirstOrDefault().ProductAttributeId` — latent NRE; `SecondAttribute`/`TerciaryAttribute` lack the null guard `MainAttribute` has. |
| D19 | factory + views | User-visible strings built in C# (`"n in cart"`, `"Items in cart n"`) and hardcoded in Razor (`"Add to cart"`, `"All Products"`, filter names) — not localisable. |
| D20 | `Views/Favourites/*` | The Favourites feature is a near-verbatim fork of the bulk-order views and form protocol. |

---

## 20. Performance profile

Cost per bulk-order page view, in service calls:

| Stage | Count |
|---|---|
| HTTP requests | `1 + 1 + (number of attributed products on the page)` — 17 for a 15-item page |
| Product query | 1 paged query per item request |
| Per product in the list | 2 × attribute-mapping fetch + `AuthorizeAsync` per mapping; 1 cart fetch (+1 full-cart fetch and 1 packname resolve when a packname is selected); 1 `GetFinalPriceAsync`; 1 `ProductPriceAvailavility`; 1 picture URL |
| Per attributed product (separate request) | 1 combination list + per-combination Jim2 variant lookup for cache warming; then **one cell pass per combination of authorised attribute values** |
| Per cell | 1–3 × `AddProductAttributeToXMLAsync` (each: mapping fetch + condition check + authorise); 1 × `GetProductAttributeCombinationAsync` (parses **every** combination's XML); 1 cart scan; 1 price calculation (which may call `GetShoppingCartAsync` again under `GroupTierPricesForDistinctShoppingCartItems`); 1 stock calculation (Jim2 path: warehouse list + stock/PO resolution) |

Worked example — one product with 10 colours × 12 sizes × 3 fits = **360 cells**: ~1 080 attribute-XML
compositions, 360 combination resolutions each scanning the full combination list, 360 cart scans, 360 price
calculations and 360 stock calculations, for **one** of the 15 products on the page.

The structural causes, in the order worth attacking:

1. **Per-cell everything.** Price, stock and cart quantity are computed one combination at a time instead of
   being loaded set-wise for the product and then indexed.
2. **Combination resolution inside the loop.** `GetProductAttributeCombinationAsync` re-parses all combinations
   for every cell; the combination set could be parsed once into a dictionary keyed by value-id tuple.
3. **XML as the interchange format.** The cell key already *is* the attribute tuple; it is converted to
   `attributesXml`, then parsed back, several times per cell.
4. **Per-product cart resolution** (D15) and **duplicate attribute-mapping fetches** (D14).
5. **One HTTP request per attributed product** for the matrix.
6. **No result caching** below the category-id level.

---

## 21. Jim2-specific coupling to be abstracted for CW.Core

The 4.6 code is welded to the Jim2 ERP integration. These are the seams the 4.9 rebuild has to define, because
CW has no Jim2:

| 4.6 dependency | What it provides | 4.9 needs |
|---|---|---|
| `IJimProductPriceService.GetProductsPriceByCustomerAsync` | customer/contract-specific price by SKU | a price source abstraction; nop `IPriceCalculationService` is the default, CW may add contract prices |
| `IProductOverrideService.GetProductJim2StockQuantity` / `GetStockQuantities` / `GetPOList` / `ShowETA` / `ShowStockQuantity` | live multi-warehouse stock and purchase-order ETA | an availability provider abstraction; nop `GetTotalStockQuantityAsync` + `ProductAttributeCombination.StockQuantity` are the built-in implementations |
| `Jim2ManageInventoryMethod.Jim2Inventory` | a fourth inventory method | 4.9 has only nop's three unless CW adds one |
| `IJim2AclService` (`ApplyAcl`, `ApplyCustomVisibilityConstraint`, `ApplyContractItemsOnlyConstraint`, `GetContractedItemsCategoryIdAsync`) | contract-items visibility | CW.Core already has `IAclOverrideService`; the contract-items concept has no CW equivalent yet — decide whether it maps onto subsidy product groups or is dropped |
| `IJim2ProductAttributeValueService.AuthorizeAsync`, `IJimProductAttributeMappingService.AuthorizeAsync` | per-attribute/per-value ACL | CW has no equivalent today; decide whether the rebuild needs it |
| `IJim2CardFileService`, `IJim2BranchService.GetCurrentCardCodeAsync`, `IJim2APIService.GetRefillStock` | Refill only | out of scope |
| `IJimPriceFormatterService` | tax-suffix-aware formatting | CW equivalent or nop `IPriceFormatter` |
| `IPackNameContext` | the currently selected packname | **already exists in CW.Core** (`Services/PackNames/PackNameContext.cs`) |

Things CW.Core has that 4.6 has no notion of, and which the rebuild must account for from day one:
**subsidies** (`ISubsidyService`, `ISubsidyAllocationService`, credit/quantity consumption order — `CLAUDE.md §14.5`),
**departments/cost centres**, **branches**, **credit notes**, and the existing
**`SubsidyCatalogController` / `SubsidyCatalogModelFactory`** surface, which is the closest existing 4.9 analogue
of a catalog-with-quantities screen and should be looked at before a second one is designed.

---

## 22. Known functional gaps against the 4.9 target

The stated 4.9 goals are "same functionality, more attributes, better performance, better design". Measured
against 4.6:

1. **More than three attributes** — 4.6 cannot do it. The axis model (`Horizontal`/`VerticalMain`/`VerticalSec`),
   the key format, the form-key parser (`case >= 3 / 2 / 1`), and the three Razor branches all hard-code three.
   Every one of those is a place the design has to change, and the key format is the one that ripples furthest
   (dictionary key = DOM id = form key = parse key).
2. **Attribute *combinations* vs the cartesian product** — 4.6 renders every value combination and discovers
   per-cell that no combination exists. With more attributes the cartesian product explodes; the rebuild should
   almost certainly drive the grid from `ProductAttributeCombination` rows rather than from the value cross-product.
3. **Atomicity** — see `NOP41` document, item I2.
4. **Localisation** — a material fraction of the UI text is hardcoded English.
5. **Access control on the AJAX endpoints** — D6.
6. **No admin surface** — there is no bulk-ordering admin screen; everything is store settings.
