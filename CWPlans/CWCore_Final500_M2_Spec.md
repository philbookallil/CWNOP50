# CW.Core move to the final 5.00 — M2: what the final release changed

Part M2 of `CWPlans/CWCore_Final500_Strategy.md` (§3, §2.3). Written 2026-10-30 against the files as committed after M1;
changes 8–11 added the same day (§5).

**Labels.** **REPLACE** — find the FIND block (whole lines, at the lines given) and put the REPLACE block in its place.
**WHOLE FILE** — replace the file's entire content with the block. **DELETE FILE** — remove the file from the project
folder. Where one file takes several blocks they are listed bottom to top; apply them in the order numbered.

**How this spec was checked.** Every FIND block was cut from the file as committed and matched once, at the lines
given. The changes were applied to a copy of the six files (and the new file added): braces and parentheses balance, and CW's
`SearchProductsAsync` then differs from the final nop method only by CW's own lines (the allocated-product filter and
its fast paths) and formatting. **Not done this time:** the build and the test run — Claude's build environment cannot
reach nuget.org or the .NET downloads this session. They are the first steps of §4.

---

## 1. What the final release changed, against CW.Core

| nop file (beta → final) | What changed | CW.Core |
|---|---|---|
| `Nop.Services/Catalog/ProductService.cs` | `SearchProductsAsync`: the AI recommendation branch runs only for a keyword search, and its result is joined to the filtered query (it was ordered, unfiltered); a category's products are ordered by their display order **in the category** (`pc.DisplayOrder`), not by the category's own (`c.DisplayOrder`). `GetNumberOfProductsInCategoryAsync` counts distinct products | **Changes 1–2**: CW's copy of `SearchProductsAsync` takes both. CW does not override the count |
| `Nop.Web/Controllers/ReturnRequestController.cs` | The return post caps each quantity at the units ordered and not yet requested (it accepted any number); the request's customer is the order's (a guest's withdrawal link); the customer is no longer saved on each line; a resource key typo fixed | **Change 3** (comment only): `ReturnRequestSubmitFilter` still refuses more than CW's return page offers, which counts shipped units only; its remark said nop checks nothing. No CW view or code uses the renamed key |
| `Nop.Services/Gdpr/GdprService.cs` | Deletes the cart lines of every custom wishlist (`customWishlistId: 0`) and the wishlists | CW's cart override passes `customWishlistId` to nop's list overload unchanged. **Change 11** closes a gap the beta had too: the cart lines the override hides were never deleted |
| `Nop.Web/Controllers/CustomerController.cs`, `Factories/CustomerModelFactory.cs`, `Views/Customer/Login.cshtml` | OTP lock checked before the code; text fields capped at 1000 characters on registration and *My account*; newsletter names kept when the form is shown again; the phone sign-in inputs rendered only when sign-in by phone is on | CW forks none of them (its customer copy is the **admin** controller, unchanged in the final) and phone sign-in stays off. **Changes 8–10** give CW's packname registration nop's new 1000-character cap |
| `Nop.Web/Areas/Admin/Controllers/ProductController.cs` | Vendor checks on the related / cross-sell / associated pop-ups; 3D file type and size checks | No change: CW forks the product edit view's *Info* tab, whose nop view and model did not change |
| `Nop.Services/Reminders/*` (3 tasks) | Batches by customer id instead of skipping rows; a follow-up whose template is inactive selects no one | No change: CW does not override them. They read cart lines and orders straight from the tables, as on the beta |
| `PriceCalculationService`, `PriceLists/*`, `DefaultPermissionConfigManager` | Price-list adjustment applied to the computed base price (new `ApplyAdjustmentPrice(decimal, PriceList)` overload); vendors lose the price-list permissions | No change: price lists stay off; CW implements neither interface |
| `Nop.Data/EntityRepository.cs` | A bulk insert of nothing returns at once | No change |
| `Nop.Web.Framework`: `ThemeableRazorViewEngine`, its registration, `Nop.Web.csproj` | §2 of the strategy | No change needed; **changes 4–7** remove the dead view-location code found while checking it (strategy §2.3) |
| `GeoLookupService`, `SearchTermService`, `ArtificialIntelligenceService`, admin `ReminderController`, two admin views, the upgrade migrations, `defaultResources.nopres.xml` | Caching, null checks, text fixes | No CW tie. The resource file adds no key, so no clash with CW's |

Every view CW forks, and every nop method CW copies other than `SearchProductsAsync` (`CLAUDE.md` §10.3), is the same in
the beta and the final. No change needs `UpdateAsync`, so `plugin.json` keeps its version.

---

## 2. Changes
### 1. REPLACE — the category join in the restricted search — order by the product's display order in the category

`C:\Users\edpab\source\repos\CW50\Plugins\Nop.Plugin.CW.Core\Services\Catalog\ProductOverrideService.cs` — lines 431–437

Depends on: nothing. 7 lines become 6 (the `join c in _categoryRepository` line goes; `group c.DisplayOrder` becomes `group pc.DisplayOrder`).

FIND:
```csharp
                var productCategoryQuery =
                    from pc in _productCategoryRepository.Table
                    join c in _categoryRepository.Table on pc.CategoryId equals c.Id
                    where (!excludeFeaturedProducts || !pc.IsFeaturedProduct) &&
                          categoryIds.Contains(pc.CategoryId)
                    group c.DisplayOrder by pc.ProductId into gr
                    select new { ProductId = gr.Key, DisplayOrder = gr.Min() };
```

REPLACE WITH:
```csharp
                var productCategoryQuery =
                    from pc in _productCategoryRepository.Table
                    where (!excludeFeaturedProducts || !pc.IsFeaturedProduct) &&
                          categoryIds.Contains(pc.CategoryId)
                    group pc.DisplayOrder by pc.ProductId into gr
                    select new { ProductId = gr.Key, DisplayOrder = gr.Min() };
```
### 2. REPLACE — the AI recommendation branch of the restricted search — keyword searches only, results joined to the query

`C:\Users\edpab\source\repos\CW50\Plugins\Nop.Plugin.CW.Core\Services\Catalog\ProductOverrideService.cs` — lines 236–265

Depends on: nothing (it sits above change 1, so change 1's lines are not moved by it). 30 lines become 32. The `if (!string.IsNullOrEmpty(keywords))` block that followed the branch now opens before it; its body below `var langs` is untouched, so its closing brace is unchanged.

FIND:
```csharp
        //try to use AI-powered recommendation provider to search products
        if (!showHidden && await _aiRecommendationPluginManager.LoadPrimaryPluginAsync(customer, storeId) is IAiRecommendationPlugin aiRecommendationPlugin)
        {
            try
            {
                var (results, totalItems) = await aiRecommendationPlugin
                    .SearchProductsAsync(keywords, categoryIds, manufacturerIds, productTagId, filteredSpecOptions, pageIndex, pageSize);
                if (!results.Any())
                    throw new Exception("AI-powered recommendation provider. No products found by the specified parameters");

                var sortedResult = await productsQuery
                    .OrderBy(orderBy, _localizedPropertyRepository, languageId > 0 ? languageId : (await _workContext.GetWorkingLanguageAsync()).Id, results)
                    .ToListAsync();

                return new PagedList<Product>(sortedResult, pageIndex, pageSize, totalItems);
            }
            catch
            {
                if (!_artificialIntelligenceSettings.UseStandardSearchWhenNoResults)
                {
                    return await productsQuery
                        .OrderBy(orderBy, _localizedPropertyRepository, languageId > 0 ? languageId : (await _workContext.GetWorkingLanguageAsync()).Id)
                        .ToPagedListAsync(pageIndex, pageSize);
                }
            }
        }

        if (!string.IsNullOrEmpty(keywords))
        {
            var langs = await _languageService.GetAllLanguagesAsync(showHidden: true);
```

REPLACE WITH:
```csharp
        if (!string.IsNullOrEmpty(keywords))
        {
            //try to use AI-powered recommendation provider to search products
            if (!showHidden && await _aiRecommendationPluginManager.LoadPrimaryPluginAsync(customer, storeId) is { } aiRecommendationPlugin)
            {
                try
                {
                    var (results, totalItems) = await aiRecommendationPlugin
                        .SearchProductsAsync(keywords, categoryIds, manufacturerIds, productTagId, filteredSpecOptions, pageIndex, pageSize);

                    if (!results.Any())
                        throw new Exception("AI-powered recommendation provider. No products found by the specified parameters");

                    var sortedResult = await productsQuery
                        .Join(results, product => product.Id, id => id, (product, id) => product)
                        .OrderBy(orderBy, _localizedPropertyRepository, languageId > 0 ? languageId : (await _workContext.GetWorkingLanguageAsync()).Id, results)
                        .ToListAsync();

                    return new PagedList<Product>(sortedResult, pageIndex, pageSize, totalItems);
                }
                catch
                {
                    if (!_artificialIntelligenceSettings.UseStandardSearchWhenNoResults)
                    {
                        return await productsQuery
                            .OrderBy(orderBy, _localizedPropertyRepository, languageId > 0 ? languageId : (await _workContext.GetWorkingLanguageAsync()).Id)
                            .ToPagedListAsync(pageIndex, pageSize);
                    }
                }
            }

            var langs = await _languageService.GetAllLanguagesAsync(showHidden: true);
```
### 3. REPLACE — the remarks of `ReturnRequestSubmitFilter` (comment only)

`C:\Users\edpab\source\repos\CW50\Plugins\Nop.Plugin.CW.Core\Filters\ReturnRequestSubmitFilter.cs` — lines 21–26

Depends on: nothing. 6 lines become 7.

FIND:
```csharp
/// <remarks>
/// With <i>Exchanges</i> on, a customer's return goes through CW's wizard, which checks it and records its exchange data;
/// nop's post takes a guest's withdrawal link only (the wizard is for the order's own signed-in customer). On every path
/// the quantities are those CW's return page offers: nop's post checks none, so it would register any number of units,
/// shipped or not.
/// </remarks>
```

REPLACE WITH:
```csharp
/// <remarks>
/// With <i>Exchanges</i> on, a customer's return goes through CW's wizard, which checks it and records its exchange data;
/// nop's post takes a guest's withdrawal link only (the wizard is for the order's own signed-in customer). On every path
/// the quantities are those CW's return page offers, which counts shipped units only: nop's post caps each quantity at
/// the units ordered and not yet requested, shipped or not, so a post above CW's count is refused here rather than
/// reduced by nop.
/// </remarks>
```
### 4. REPLACE — remove the `ViewPathsRegistrar` registration

`C:\Users\edpab\source\repos\CW50\Plugins\Nop.Plugin.CW.Core\Infrastructure\NopStartup.cs` — lines 202–203

Depends on: nothing; **apply with change 7** (the registration names the class change 7 deletes). 2 lines become 1.

FIND:
```csharp
        services.AddScoped<ViewPathsRegistrar>();
        services.Configure<RazorViewEngineOptions>(options =>
```

REPLACE WITH:
```csharp
        services.Configure<RazorViewEngineOptions>(options =>
```
### 5. WHOLE FILE — `ViewsPathsRegistry`: one list of the nine locations

`C:\Users\edpab\source\repos\CW50\Plugins\Nop.Plugin.CW.Core\Infrastructure\ViewsPathsRegistry.cs`

Depends on: **apply with change 6** (the expander reads this class). The file keeps its byte-order mark: paste into the open file.

What goes, all dead (strategy §2.3): the second, identical list (`partialViewsPaths`, dropped by the expander's `Distinct()`); the location `~/Plugins/CW.Core/Areas/Admin/Views/{2}/{1}/{0}.cshtml` (`{2}` is the area: in the admin area it points at `Areas/Admin/Views/Admin/…`, which does not exist, and on the storefront it collapses to `Areas/Admin/Views//{1}/{0}`, which only finds what the location before it already finds); the four static `Register*` / `Unregister*` methods, called only by `ViewPathsRegistrar`. What stays, in the same order: the nine locations that make CW's forks replace nop's views. The file goes from 121 lines to 29.

```csharp
namespace Nop.Plugin.CW.Core.Infrastructure;

/// <summary>
/// The view locations CW.Core searches before the theme's and nop's
/// </summary>
/// <remarks>
/// nop asks for its views by name, so a CW view at one of these locations with the same name as a nop view replaces it.
/// {0} is the view name and {1} the controller name.
/// </remarks>
public static class ViewsPathsRegistry
{
    private const string PLUGIN_FOLDER = "CW.Core";

    /// <summary>
    /// Gets the view locations, in the order they are searched
    /// </summary>
    public static IReadOnlyList<string> ViewsPaths { get; } =
    [
        $"~/Plugins/{PLUGIN_FOLDER}/Views/{{0}}.cshtml",
        $"~/Plugins/{PLUGIN_FOLDER}/Views/{{1}}/{{0}}.cshtml",
        $"~/Plugins/{PLUGIN_FOLDER}/Views/Shared/{{0}}.cshtml",
        $"~/Plugins/{PLUGIN_FOLDER}/Views/Shared/{{1}}/{{0}}.cshtml",
        $"~/Plugins/{PLUGIN_FOLDER}/Views/Shared/Components/{{1}}/{{0}}.cshtml",
        $"~/Plugins/{PLUGIN_FOLDER}/Areas/Admin/Views/{{1}}/{{0}}.cshtml",
        $"~/Plugins/{PLUGIN_FOLDER}/Areas/Admin/Views/Shared/{{0}}.cshtml",
        $"~/Plugins/{PLUGIN_FOLDER}/Areas/Admin/Views/Shared/{{1}}/{{0}}.cshtml",
        $"~/Plugins/{PLUGIN_FOLDER}/Areas/Admin/Views/Shared/Components/{{1}}/{{0}}.cshtml"
    ];
}
```

### 6. WHOLE FILE — `ViewLocationExpander`: CW's locations first, then the rest

`C:\Users\edpab\source\repos\CW50\Plugins\Nop.Plugin.CW.Core\Infrastructure\ViewLocationExpander.cs`

Depends on: **change 5**. Same search order as today; gone are the admin-only `~/Plugins/Nop.Plugin.CW.Core/Areas/Admin/Views/Home/{0}.cshtml` (no such folder: the plugin deploys to `Plugins/CW.Core`), the concatenation of the duplicate list and its `Distinct()`. The file goes from 56 lines to 33.

```csharp
using Microsoft.AspNetCore.Mvc.Razor;

namespace Nop.Plugin.CW.Core.Infrastructure;

/// <summary>
/// Puts CW.Core's view locations (<see cref="ViewsPathsRegistry"/>) ahead of every other location, the theme's and nop's
/// included, on the storefront and in the admin area alike
/// </summary>
/// <remarks>
/// nop asks for its views by name, so a CW view with the same name at one of these locations is the one that renders,
/// and a theme cannot replace it: the theme's locations come after CW's.
/// </remarks>
public class ViewLocationExpander : IViewLocationExpander
{
    /// <summary>
    /// The locations do not depend on the request, so nothing is added to the lookup's cache key
    /// </summary>
    /// <param name="context">View location expander context</param>
    public void PopulateValues(ViewLocationExpanderContext context)
    {
    }

    /// <summary>
    /// CW.Core's locations first, then every location already registered that is not one of them
    /// </summary>
    /// <param name="context">View location expander context</param>
    /// <param name="viewLocations">The locations registered before this expander runs</param>
    /// <returns>The locations in the order they are searched</returns>
    public IEnumerable<string> ExpandViewLocations(ViewLocationExpanderContext context, IEnumerable<string> viewLocations)
    {
        return ViewsPathsRegistry.ViewsPaths.Concat(viewLocations.Where(location => !ViewsPathsRegistry.ViewsPaths.Contains(location)));
    }
}
```

### 7. DELETE FILE — `ViewPathsRegistrar`

`C:\Users\edpab\source\repos\CW50\Plugins\Nop.Plugin.CW.Core\Infrastructure\ViewPathsRegistrar.cs`

Depends on: **change 4**. Nothing injects the class or calls its methods — checked in CW.Core, the other CW plugins, `CW.Common`, the tests and the 4.9 source. The `.csproj` lists no `.cs` file, so nothing else changes.

### 8. REPLACE — cap the account's text fields at 1000 characters, as nop's registration does

`C:\Users\edpab\source\repos\CW50\Plugins\Nop.Plugin.CW.Core\Services\PackNames\PackNameRegistrationService.cs` — lines 151–188

Depends on: **change 10** (the `using` for `CommonHelper`). 38 lines stay 38; ten assignments change (gender, first and last name, both street addresses, postcode, city, county, phone, fax). Date of birth, country and state are not text and stay as they are. nop never stores the phone as typed (it saves its formatted E.164 number); CW does, so it gets the same cap. nop's registration also caps company, time zone and VAT number: CW sets the company from the branch name and the time zone from the store, and never sets a VAT number.

FIND:
```csharp
        if (_customerSettings.GenderEnabled)
            customer.Gender = data.Gender;

        if (_customerSettings.FirstNameEnabled)
            customer.FirstName = data.FirstName;

        if (_customerSettings.LastNameEnabled)
            customer.LastName = data.LastName;

        if (_customerSettings.DateOfBirthEnabled)
            customer.DateOfBirth = data.DateOfBirth;

        if (_customerSettings.StreetAddressEnabled)
            customer.StreetAddress = data.StreetAddress;

        if (_customerSettings.StreetAddress2Enabled)
            customer.StreetAddress2 = data.StreetAddress2;

        if (_customerSettings.ZipPostalCodeEnabled)
            customer.ZipPostalCode = data.ZipPostalCode;

        if (_customerSettings.CityEnabled)
            customer.City = data.City;

        if (_customerSettings.CountyEnabled)
            customer.County = data.County;

        if (_customerSettings.CountryEnabled)
            customer.CountryId = data.CountryId;

        if (_customerSettings.CountryEnabled && _customerSettings.StateProvinceEnabled)
            customer.StateProvinceId = data.StateProvinceId;

        if (_customerSettings.PhoneEnabled)
            customer.Phone = data.Phone;

        if (_customerSettings.FaxEnabled)
            customer.Fax = data.Fax;
```

REPLACE WITH:
```csharp
        if (_customerSettings.GenderEnabled)
            customer.Gender = CommonHelper.EnsureMaximumLength(data.Gender, 1000);

        if (_customerSettings.FirstNameEnabled)
            customer.FirstName = CommonHelper.EnsureMaximumLength(data.FirstName, 1000);

        if (_customerSettings.LastNameEnabled)
            customer.LastName = CommonHelper.EnsureMaximumLength(data.LastName, 1000);

        if (_customerSettings.DateOfBirthEnabled)
            customer.DateOfBirth = data.DateOfBirth;

        if (_customerSettings.StreetAddressEnabled)
            customer.StreetAddress = CommonHelper.EnsureMaximumLength(data.StreetAddress, 1000);

        if (_customerSettings.StreetAddress2Enabled)
            customer.StreetAddress2 = CommonHelper.EnsureMaximumLength(data.StreetAddress2, 1000);

        if (_customerSettings.ZipPostalCodeEnabled)
            customer.ZipPostalCode = CommonHelper.EnsureMaximumLength(data.ZipPostalCode, 1000);

        if (_customerSettings.CityEnabled)
            customer.City = CommonHelper.EnsureMaximumLength(data.City, 1000);

        if (_customerSettings.CountyEnabled)
            customer.County = CommonHelper.EnsureMaximumLength(data.County, 1000);

        if (_customerSettings.CountryEnabled)
            customer.CountryId = data.CountryId;

        if (_customerSettings.CountryEnabled && _customerSettings.StateProvinceEnabled)
            customer.StateProvinceId = data.StateProvinceId;

        if (_customerSettings.PhoneEnabled)
            customer.Phone = CommonHelper.EnsureMaximumLength(data.Phone, 1000);

        if (_customerSettings.FaxEnabled)
            customer.Fax = CommonHelper.EnsureMaximumLength(data.Fax, 1000);
```

### 9. REPLACE — cap the e-mail and username passed to nop's registration

`C:\Users\edpab\source\repos\CW50\Plugins\Nop.Plugin.CW.Core\Services\PackNames\PackNameRegistrationService.cs` — lines 129–135

Depends on: **change 10**. 7 lines stay 7; lines 2 and 3 of the block change.

FIND:
```csharp
        var registrationRequest = new CustomerRegistrationRequest(customer,
            data.Email,
            _customerSettings.UsernamesEnabled ? data.Username : data.Email,
            password,
            _customerSettings.DefaultPasswordFormat,
            registration.StoreId,
            true);
```

REPLACE WITH:
```csharp
        var registrationRequest = new CustomerRegistrationRequest(customer,
            CommonHelper.EnsureMaximumLength(data.Email, 1000),
            CommonHelper.EnsureMaximumLength(_customerSettings.UsernamesEnabled ? data.Username : data.Email, 1000),
            password,
            _customerSettings.DefaultPasswordFormat,
            registration.StoreId,
            true);
```

### 10. REPLACE — `using Nop.Core;` for `CommonHelper`

`C:\Users\edpab\source\repos\CW50\Plugins\Nop.Plugin.CW.Core\Services\PackNames\PackNameRegistrationService.cs` — line 1

Depends on: nothing. 1 line becomes 2. (The file's byte-order mark is not part of the text the editor shows; nothing to do about it.)

FIND:
```csharp
using Nop.Core.Domain.Common;
```

REPLACE WITH:
```csharp
using Nop.Core;
using Nop.Core.Domain.Common;
```

### 11. NEW FILE — `CustomerPermanentlyDeletedEventConsumer`: the GDPR delete takes the hidden packname cart lines too

`C:\Users\edpab\source\repos\CW50\Plugins\Nop.Plugin.CW.Core\Services\Orders\CustomerPermanentlyDeletedEventConsumer.cs`

Depends on: nothing. Create the file with this content (Visual Studio adds the byte-order mark). nop registers every `IConsumer<>` it finds, so nothing else is wired. nop's GDPR delete (`GdprService.PermanentDeleteCustomerAsync`) deletes the cart lines `GetShoppingCartAsync` returns and then publishes `CustomerPermanentlyDeleted`; the lines CW's cart override hides (packname lines the packname cart view leaves out) are still there, and this consumer deletes them through the cart service, so each line's packname mapping goes with it (`CORE_PN_PackNameCartItem` cascades) and CW's deleted event for it is published.

```csharp
using Nop.Core.Domain.Gdpr;
using Nop.Core.Domain.Orders;
using Nop.Data;
using Nop.Services.Events;
using Nop.Services.Orders;

namespace Nop.Plugin.CW.Core.Services.Orders;

/// <summary>
/// Deletes the cart lines a permanent customer delete (GDPR) leaves behind
/// </summary>
/// <remarks>
/// nop deletes the lines the cart service returns for the customer, and CW's cart override leaves out of that answer the
/// packname lines the packname cart view does not show. Every line still in the customer's cart once nop has finished is
/// deleted here, through the cart service, so each line's packname mapping goes with it and its deleted events are
/// published.
/// </remarks>
public class CustomerPermanentlyDeletedEventConsumer : IConsumer<CustomerPermanentlyDeleted>
{
    #region Fields

    protected readonly IRepository<ShoppingCartItem> _shoppingCartItemRepository;
    protected readonly IShoppingCartService _shoppingCartService;

    #endregion

    #region Ctor

    public CustomerPermanentlyDeletedEventConsumer(IRepository<ShoppingCartItem> shoppingCartItemRepository,
        IShoppingCartService shoppingCartService)
    {
        _shoppingCartItemRepository = shoppingCartItemRepository;
        _shoppingCartService = shoppingCartService;
    }

    #endregion

    #region Methods

    /// <summary>
    /// Handles the event
    /// </summary>
    /// <param name="eventMessage">The event message</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public async Task HandleEventAsync(CustomerPermanentlyDeleted eventMessage)
    {
        var leftBehind = await _shoppingCartItemRepository.GetAllAsync(query =>
            query.Where(item => item.CustomerId == eventMessage.CustomerId));

        foreach (var item in leftBehind)
            await _shoppingCartService.DeleteShoppingCartItemAsync(item, resetCheckoutData: false);
    }

    #endregion
}
```

---

## 3. Unit tests (already in `Tests/CW.Tests`)

- `Core/Services/Orders/CustomerPermanentlyDeletedEventConsumerTests.Every_line_still_in_the_deleted_customers_cart_is_deleted_through_the_cart_service`
  (new): the deleted customer's cart and wishlist lines are deleted through the cart service, another customer's are
  not. **The test project does not build until change 11 is applied** (the test names the new class).
- `Core/Services/PackNames/PackNameRegistrationServiceTests.The_account_text_fields_are_cut_to_1000_characters_as_nops_registration_cuts_them`
  (new): a manager's registration with 1200-character values sends nop's registration a 1000-character e-mail and
  username and saves the ten text fields at 1000 characters. It fails until changes 8–10 are applied.
- `Core/Infrastructure/ViewLocationExpanderTests.The_plugins_nine_locations_come_first_in_search_order_once_each`
  (new): on the storefront and in the admin area the search order is exactly CW's nine locations, then nop's. It fails
  until changes 5–6 are applied (today both areas get a tenth location, the `{2}` one, and the admin area also the dead
  `Home` location) and guards the override
  mechanism from then on. The three existing tests of the class are unchanged.

`SearchProductsAsync`'s restricted path runs SQL through nop's repositories, which the suite does not set up (no
database); changes 1–2 are covered by the runtime checks below.

---

## 4. Checks after applying

1. Rebuild the solution: 0 errors.
2. `dotnet test Tests\CW.Tests --filter "FullyQualifiedName~CW.Tests.Core"` — **628** passing (625 + the three new tests);
   then `dotnet test Tests\CW.Tests` for every CW suite.
3. Runtime checks wait for M3 (the new database and CW.Core installed). M3's pass includes, for this part:
   - **T1** a packname restricted to its allocated products: a category page lists only allocated products, in the
     order of their *display order in that category* (set two products to 2 and 1 and check they swap);
   - **T2** the same packname: a keyword search lists only allocated products; with no AI recommendation plugin
     installed the search behaves as before;
   - **T3** a customer without a restriction: category page and search unchanged (they go through nop's own method);
   - **T4** nop's return post with a quantity above the shipped units is refused with CW's message — with *Exchanges*
     off, or on a guest's withdrawal link (with *Exchanges* on and no link, the filter sends nop's post back to the page
     before the quantity check: the customer's way to return is the wizard);
   - **T5** a forked storefront view (*Order details*) and a forked admin view (*Customer edit*) render CW's version, and
     a CW page opened by path (*packname checkout*) loads — the view-location changes leave lookups as they were;
   - **T6** a manager's *Add packname* with an account, and an Excel import row, with a first name over 1000
     characters: the customer is saved with the first 1000;
   - **T7** a customer with a packname line the packname cart view leaves out (its packname deactivated): *GDPR —
     permanently delete* removes it with the rest of the cart (`ShoppingCartItem` has no row left for the customer).

---

## 5. Decisions (2026-10-30)

- **GDPR delete and hidden packname lines:** fixed in this part (change 11). Found while checking `GdprService`; the
  beta had the same gap.
- **Text fields of a manager-registered account:** capped at 1000 characters like nop's registration (changes 8–10).
  Found by the review; the beta had the same gap.
