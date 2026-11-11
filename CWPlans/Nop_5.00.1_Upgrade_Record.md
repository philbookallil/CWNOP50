# nopCommerce 5.00 → 5.00.1 — upgrade record

Applied by Eduardo on 2026-11-11; checked by Claude the same day. The unmodified release was checked from a `NOP501/` copy, removed once the check was done.
`CLAUDE.md` §10.6 gives the procedure this record follows.

## 1. Is the release fully applied?

Yes. The working tree's nop code (`Libraries/`, `Presentation/`, `Build/`, `Tests/Nop.Tests`, the 34 nop plugins,
`Directory.Build.props`, `deploy.cmd`, `Deploying.Readme.txt`) was compared file by file with `NOP501/`, ignoring line
endings. Every file matches. The only difference is `NopCommerce.sln`, and it differs only by the CW projects
(`CWLibraries`, `CW.Common`, `CW.Tests`, the four `CW.*` plugins, `ExternalAuth.SSO`, `Integration.Odoo`,
`Payments.NABGateway`). The release's own solution entries are all present.

Not compared: `wwwroot/lib_npm` (its `package.json` and `package-lock.json` are identical, so its contents are the
same), `wwwroot/images`, and `App_Data` files that are not source (`appsettings.json`, data protection keys, logs).

## 2. What 5.00.1 changed

The "5.00" baseline is the last commit (`ae8ccef`). 37 files differ, excluding line endings.

| Area | Change | Files |
|---|---|---|
| Version | `NopVersion.MINOR_VERSION` "0" → "1". `CURRENT_VERSION` stays "5.00" | `Nop.Core/NopVersion.cs` |
| HTML sanitizing | `CommonHelper.EnsureOnlyAllowedHtml` is moved from the Forums plugin into `Nop.Core`. It removes every tag outside a fixed list, then runs `HtmlSanitizer`. **`HtmlSanitizer` 9.2.1039 is now a package of `Nop.Core`** (it was only in Forums before) | `Nop.Core/CommonHelper.cs`, `Nop.Core.csproj`, Forums `ForumService.cs` and `.csproj` |
| Product description | Admin product Create / Edit now sanitizes `FullDescription` and its localized versions **whenever `AdminAreaSettings.RichEditorAllowJavaScript` is off** (the install default), for administrators too, not only vendors. Vendors' specification-attribute values and product-import descriptions are sanitized too | `Admin/Controllers/ProductController.cs`, `ImportManager.cs` |
| Vendor access | A vendor can no longer add other vendors' products to a category, manufacturer, filter level value, price list or discount. A vendor can no longer read or change another vendor's discount. A vendor's discount is always "assigned to SKUs" | `Category`, `Manufacturer`, `FilterLevelValue`, `PriceList`, `Discount` admin controllers |
| Default permissions | The Vendors role no longer gets product tags, filter level values or product attributes. This reaches **new databases only**: `PermissionService.InsertPermissionsAsync` adds missing records at start-up and never changes existing mappings | `DefaultPermissionConfigManager.cs` |
| Price lists import / export | The price calculation type is exported and imported as a drop-down. An empty manual price imports as empty instead of 0 | `ExportManager.cs`, `ImportManager.cs` |
| URLs under a virtual directory | `WebHelper.ModifyQueryString` no longer removes the path base from the middle of a URL. `RemoveApplicationPathFromRawUrl` returns the raw URL when it does not start with the path base (it used to return empty) | `WebHelper.cs`, `LocalizedUrlExtenstions.cs`, `WebHelperTests.cs` |
| robots.txt | The `Host:` line is no longer written | `CommonModelFactory.cs`, its test |
| IIS headers | The Content-Security-Policy in `web.config` adds `blob:` to `connect-src` | `web.config` |
| Upgrade migration | `SearchTermMigration` (4.90 → 5.00 upgrades only) now waits for its truncate | `SearchTermMigration.cs` |
| PayPal Commerce (5.00.6 → 5.00.7) | `ToUpper` / `ToLower` replaced by the invariant versions. Status checks no longer fail under cultures such as Turkish. No API or behaviour change otherwise | `PayPalCommerceServiceManager.cs`, `Order.cs`, `PaymentToken.cs` |
| PunchOut (5.00.1 → 5.00.2) | New "Unit of measure code" setting; editing an identity now hashes the shared secret, and sign-in compares hashes; the cart is cleared at session end | 9 files |
| Forums (5.00.7 → 5.00.9) | Uses the core sanitizer; search pager uses route links | 4 files |

No Razor view in `Presentation/` changed, and no `.js` or `.css` file changed.

## 3. Effect on the CW plugins

**No code change is needed.** Each point below was checked against the code in `Plugins/Nop.Plugin.CW.*`,
`ExternalAuth.SSO`, `Integration.Odoo`, `Payments.NABGateway`, `CWLibraries/`, `Tests/CW.Tests` and
`Themes/CW.Standard`.

| Check | Result |
|---|---|
| Copied nop method bodies (`CLAUDE.md` §10.3) | None of their nop source files changed (`ShipmentService`, `PdfService`, `InvoiceDocument`, `ProductService`, `MessageTokenProvider`, `OrderProcessingService`, `ShoppingCartService`, `CustomerService`, `CustomerController`, `CustomerModelFactory`, `OrderReportService`, `Reminder/Index.cshtml`) |
| CW.Core's forked storefront views (§10.4) and the `CW.Standard` theme's views | No nop view changed |
| Overridden services and controllers | No CW class derives from or replaces any changed class (`ProductController`, `CategoryController`, `DiscountController`, `ManufacturerController`, `PriceListController`, `FilterLevelValueController`, `WebHelper`, `CommonModelFactory`, `ImportManager`, `ExportManager`, `DefaultPermissionConfigManager`), and none calls `ModifyQueryString` or `RemoveApplicationPathFromRawUrl` |
| `plugin.json` `SupportedVersions: ["5.00"]` | Still loads: nop compares it with `CURRENT_VERSION`, which is still "5.00" |
| Package versions (lesson #11) | No CW project references `HtmlSanitizer` or `AngleSharp`. Host versions the SSO plugin pins (`Microsoft.IdentityModel.*` 8.16.0) are unchanged. `CW.Tests` uses the same test packages as `Nop.Tests` 5.00.1 |
| PayPal Commerce and CW.Core's own-placement path (`OwnPlacementPaymentMethods`, `OrderProcessingOverrideService`) | PayPal still calls `IOrderProcessingService.PlaceOrderAsync` the same way; only string casing changed |
| CSP `connect-src blob:` | CW.Core's packname Excel export downloads through a `blob:` link; a link download is not governed by `connect-src`, so it worked before and still does. ThemeCore's preview sets its own `frame-ancestors` header, unaffected |
| Vendor fixes, default permissions | CW keeps vendors off (`CLAUDE.md` §4) |
| ThemeCore's custom-CSS sanitizer | Its own code, independent of `HtmlSanitizer` |

## 4. Decision for the business

**Admin product descriptions lose tags on save.** With `RichEditorAllowJavaScript` off, saving a product in admin
now keeps only these tags in the full description: `br, hr, b, i, u, a, div, ol, ul, li, blockquote, img, span, p, em,
strong, font, pre, h1–h6, address, cite, code`. Every other tag is deleted (its text stays), including `table`,
`tr`, `td`, `th`, `tbody`, `sup`, `sub`, `small`, `iframe`, `video`, `section` and `figure`. A size chart written as a
table becomes a run of text the first time someone saves the product.

`Integration.Odoo` writes `FullDescription` from Odoo's `website_description` through `IProductService`, so the sync
is not affected. A synced product edited in admin afterwards is affected. Odoo's website editor output usually uses
`section` and often tables.

Options:

1. Accept it. Descriptions with tables or embedded video are maintained in Odoo only, never re-saved in nop admin.
2. Turn on *Allow JavaScript in rich editor* (`AdminAreaSettings.RichEditorAllowJavaScript`). Nothing is sanitized
   for administrators, as in 5.00. This also lets the editor keep scripts, so it is safer only if every admin is trusted.
3. A CW change that keeps sanitizing scripts but allows tables. This requires overriding nop's product controller
   or its save path, and nop gives no hook for that. Not recommended unless options 1 and 2 are both refused.

To see the effect on the dev site: give a product a table in its description, save it in admin, and reopen it.

**Status (2026-11-11):** sent to the manager. Until they answer, nop's 5.00.1 behaviour stays as it is (option 1 by default); nothing is changed.

## 5. Housekeeping on the dev database

- **PunchOut is installed on `testnop50`.** It has no `UpdateAsync`, so its new resource keys and its
  `UnitOfMeasureCode` setting are not installed on this database. Its Configure page shows the raw key for the new
  field, and its code falls back to "EA". CW does not use PunchOut. Uninstall it, or ignore it.
- The Vendors role keeps the seven catalog permissions 5.00.1 removed from new installs. This doesn't matter while
  vendors are off.
- `CLAUDE.md` §3.2 says `Misc.WebApi.Frontend`, `Misc.RFQ`, `Misc.Dynamics365` and `Misc.Brevo` are not installed;
  `App_Data/plugins.json` on this site lists all four as installed (with every other nop plugin). This was already true
  before 5.00.1.

## 6. Not done

- No build or test run: the shell used for this check has no .NET SDK. Run `dotnet test Tests/CW.Tests` once
  (CW.Core filter `FullyQualifiedName~CW.Tests.Core`).
- `NOP501/` was deleted after the check (2026-11-11).
