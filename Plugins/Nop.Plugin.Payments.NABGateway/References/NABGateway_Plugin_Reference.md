# Payments.NABGateway — Plugin Reference

Card payments through the **NAB Transact XML API** (`xml-4.2`), with two admin reports (Order Payments, Failed
Transactions). CW-owned (Author: Total Image Group); SystemName `Payments.NABGateway`, locale keys
`Plugins.Payments.NABGateway.*`, table `NABXMLTransaction`.

Ported from the 4.90 source's `Plugins/Nop.Plugin.Payments.NABGateway` (`CLAUDE.md` §3.1) on 2026-10-22. Plugin version
`0.5_DATA`.

---

## 1. What it does

| Piece | Where |
|---|---|
| Payment method (`IPaymentMethod`, `PaymentMethodType.Standard`) | `NABXMLPaymentProcessor.cs` |
| Checkout card form | `Components/NABXMLPaymentViewComponent.cs`, `Views/PaymentInfo.cshtml`, `Validators/PaymentInfoValidator.cs` |
| Card kept between checkout steps | `Services/NABXMLCardDataStore.cs` (§3) |
| Gateway call (named `HttpClient`, XML in / out, debug log with card masked) | `Services/NABXMLGatewayClient.cs` |
| Every attempt, approved or not, stored for the reports | `Domain/NABXMLTransaction.cs`, `Services/NABXMLTransactionService.cs` |
| Configure page (gateway URL, merchant id, password, debug, ignore SSL), per store | `Controllers/NABXMLPaymentController.cs`, `Views/Configure.cshtml` |
| Reports (*NAB Payments* menu, before *Reports*), permission `Payments.NABGateway.Admin.AccessReports` | `Areas/Admin/…`, `Views/NABXMLTransaction/…`, `Content/nab-reports.js`, `Infrastructure/EventConsumer.cs` |

The payment is captured when the order is placed (`PaymentStatus.Paid`). Capture, refund, void and recurring payments
are not supported. The gateway stores no card data on our side: the reports keep the masked PAN NAB returns.

---

## 2. What the 4.90 → 5.00 port changed

| Change | Why |
|---|---|
| `net10.0`, `SupportedVersions` `5.00`, version `0.5_DATA` | §10.5 #1, #8 |
| Project references only `Nop.Web` (the unused `CW.Common` and the redundant `Nop.Web.Framework` references dropped) | §11 |
| `using Nop.Services.Helpers` for `IWebHelper` | §10.5 #2 |
| Migration: `Create.TableFor<T>()` → `this.CreateTableIfNotExists<T>()`, `MigrationProcessType.Installation`, version in `Data/MigrationVersions.cs` | §10.5 #4, §8.3–8.4 |
| **Card data no longer travels on `ProcessPaymentRequest`** — kept in memory instead | nop 5.00 removed the `CreditCard*` properties. §3 |
| `IsCreditCard()` → FluentValidation's `CreditCard()` (same Luhn check), plus a *required* rule | nop 5.00 removed `IsCreditCard()` |
| `Payment.Card*`, `Payment.Expir*` and `Admin.Common.Close` resources → plugin keys `Plugins.Payments.NABGateway.Payment.*` and `…Reports.Details.Close` | nop 5.00 no longer ships them; the form would have shown raw keys |
| Report views: `<script src="@Url.Content(…)">` → `src="~/Plugins/…"`, both scripts `asp-location="Footer"` | nop 5.00's script tag helper takes every `<script>` and strips the first character of a local `src` (it expects `~/`); `/Plugins/…` threw *"The path in 'value' must start with '/'"* — a 500 the build cannot see (§10.5 #5) |
| Removed: the commented-out demo-data seeder (`CreateDemoData`, `BuildDemoRawXml`), a stray `DocumentFormat.OpenXml.EMMA` using, a developer path comment | Dead code (§0.8) |

Unchanged and checked against 5.00: `BasePaymentController`, `BaseAdminController`, `BaseAdminMenuCreatedEventConsumer`,
`IAdminMenu.GetMenuItemUrl`, `DataTablesModel` (`SearchButtonId`, `FilterParameter`, `RenderCustom`),
`StoreScopeConfigurationViewComponent`, `_ConfigurePlugin`, Bootstrap 4.6 modal markup. The report script already
reads PascalCase JSON (§10.5 #6).

---

## 3. Card data between checkout steps — DECISION 2026-10-22

> **NOTE for the client.** In the 4.90 (and 4.1-era nop) checkout, the card number, expiry, cardholder name **and
> CVV** were written **in plain text to the database** (`GenericAttribute`, key `ProcessPaymentRequest`) when the
> shopper left the payment step, and read back when they confirmed the order. They were removed only after a
> *successful* order; a declined payment or an abandoned checkout left them in the database indefinitely.
>
> nopCommerce 5.00 removed the card fields from its payment request, so a card plugin can no longer do this. In the
> 5.00 plugin **the card never reaches the database**: it is held in the web server's memory, encrypted, for at most
> 15 minutes, and deleted the moment it is sent to NAB — approved or declined.

**How it works.** `GetPaymentInfoAsync` puts the card in `INABXMLCardDataStore` (singleton over `IMemoryCache`,
encrypted with ASP.NET Data Protection, purpose `Nop.Plugin.Payments.NABGateway.CardData`, keyed by store id +
customer id, 15 min — `NABXMLPaymentDefaults.CardDataLifetimeMinutes`) and returns an empty `ProcessPaymentRequest`.
`ProcessPaymentAsync` takes it (read and remove). A new card for the same customer replaces the old one.

**Consequences.**

- If the card is not there at Confirm — more than 15 minutes later, or on another server — the order is not placed
  and the shopper sees *"Your card details are no longer available…"* and re-enters the card. After an **app
  restart** nop itself refuses first: its `AppStartedConsumer` deletes every saved `ProcessPaymentRequest`, so Confirm
  fails with nop's *"Payment information is not entered"* before the plugin is called.
- **A web farm needs sticky sessions** (or the shopper re-enters the card when the confirm request lands elsewhere).
- After a declined payment the shopper re-enters the card (it was used once).

**Is this motivated by law?** Not by a law as such — nopCommerce does not state its reason in anything we found. The
effect lines up with the **PCI DSS** card-industry standard, which merchants are bound to by their acquirer (NAB)
contract rather than by statute:

- *Sensitive authentication data* (the CVV) must not be kept after authorisation, even encrypted (PCI DSS v4.0
  req. 3.3.1). The 4.90 behaviour broke this whenever a payment was declined or a checkout abandoned.
- From **31 March 2025** (v4.0 future-dated requirement 3.3.2), such data kept *before* authorisation must be encrypted
  with strong cryptography. The 4.90 behaviour kept it in plain text.

In Australia the **Privacy Act 1988 (APP 11)** is the law that applies: it requires reasonable steps to protect personal
information, which card data is. These references are given for the conversation with the client; the client's PCI
assessor (QSA) or acquirer should confirm the exact requirements for their merchant level.

**Not chosen.** The nop distributed cache (works across servers, but with the SQL Server cache type the encrypted card
would reach the database); a NAB hosted payment page / tokenisation, which takes card data off our servers entirely and
is the long-term answer to PCI scope — a separate piece of work.

---

## 4. Open items

1. **Branch in the payment description / reference** — the 4.1 plugin appended the staff member's branch
   (`IBranchService`, CW.Core). Dropped in 4.90 (TODO in `GeneratePaymentRequestDescriptionAsync`); needs CW.Core
   ported and a §10.2 coupling decision.
2. **`nab-reports.js` and the report views** still use global functions, a `<script>` with Razor URLs and a
   `$(document)` delegated handler — against §9.14–9.15. Works; tidy when the reports are next touched.
3. **Payments against the NAB test gateway** not yet run on 5.00 (needs test merchant credentials).

---

## 5. Test plan

### 5.1 Unit tests — `Tests/CW.Tests/Payments.NABGateway/`

43 cases, all passing (2026-10-23). Pure logic: no database, no network, no nop engine (`CLAUDE.md` §22 Note A). Run
with `dotnet test Tests/CW.Tests -p:SolutionDir=<repo root>`; add `--filter "FullyQualifiedName~Payments.NABGateway"`
for this suite only. The helpers they reach are `internal` (`InternalsVisibleTo CW.Tests` in the plugin's `.csproj`).

| File | Covers |
|---|---|
| `CardDataStoreTests` | `NABXMLCardDataStore`: save / take, used once, a new card replaces the old, one store and customer only, expiry at `CardDataLifetimeMinutes` (fake clock), the cached bytes hold no card number, name or CVV in plain text |
| `CardNumberAndReferenceTests` | `CardNumberHelper.Sanitize`; `BuildNameToken` — cut at 10 / 15, trailing `-` / `.` / spaces trimmed, spaces to dashes, empty input |
| `GatewayXmlTests` | The request XML (bare `NABTransactMessage`, UTF-8 declaration, section order, the transaction fields); the approved, declined and gateway-error responses; empty and malformed responses; the debug-log masking of card number and CVV |

Not unit-tested: `PaymentInfoValidator` (nop's `BaseNopValidator` resolves the localisation service from the engine,
so it needs nop's test bootstrap) and `ProcessPaymentAsync` end to end (needs the gateway, or a mocked HTTP handler).

### 5.2 Manual tests

Run 2026-10-22 on a developer's local site and database (one store); cases 6 and 8 run 2026-10-29 — results in the last
column.

**How it was run.** In the site's browser, signed in as the developer's administrator account; database state checked
with SQL alongside.

- **Setup.** Plugin installed from *Local plugins* and the site restarted. *Configure* saved with placeholders only
  (gateway URL `https://demo.transact.nab.com.au`, merchant ID `TEST0000`, no password, debug mode on). *Card Payment*
  switched on in *Payment methods*. The store had no products, so a test product was created (*NAB test product*,
  $10, no shipping, tax exempt) and put in the administrator's cart; checkout used the account's existing billing
  address. Two fake rows (one approved, one declined, masked cards, `@example.com` addresses) were inserted into
  `NABXMLTransaction` by SQL to exercise the reports. The card form used the published Visa test number 4111 1111 1111
  1111; nothing was sent to NAB.
- **Defect found and fixed.** Both report pages returned a 500: nop 5.00's script tag helper rejects a `/Plugins/…`
  `src` (§2). The views were corrected and the pages retested.
- **Removed afterwards** (2026-10-23). The test product (deleted from the admin, so nop keeps it flagged `Deleted`), the
  cart item and both `NABXMLTransaction` rows. No order was placed.
- **Left in place.** The plugin installed, *Card Payment* active, and the placeholder *Configure* values — replace them
  with the NAB test merchant's details before cases 3 and 5.
- **Cases 6 and 8, 2026-10-29.** Same site, now with CW.Core (part-ported), CW.Freight, CW.ThemeCore and the CW theme
  installed, so the checkout ran through CW.Core's overrides (a plain cart, no packname selected). The test product was
  un-deleted for the run and deleted again afterwards, and the cart item removed. For case 8 the permission was taken
  off Administrators in *Access control* and given back the same way; it is on Administrators again.

| # | Case | Expected | Result |
|---|---|---|---|
| 1 | Install the plugin; open *Configure* at default and at a store scope; save | Settings save per scope; table `NABXMLTransaction` exists | **Pass** at default scope (table, 49 resources, migration, permission; settings saved and reloaded). Store scope not testable — one store |
| 2 | Checkout, payment-info step: empty form / bad Luhn / bad CVV / expired month | Plugin-key messages (not raw `Payment.*` keys) | **Pass**. The empty form also shows *Card is expired*: the expiry selects default to January of this year (as in 4.90) |
| 3 | Valid test card, confirm within 15 min | NAB called; order Paid; *Order Payments* report shows the row with masked PAN | Not run — needs NAB test merchant credentials |
| 4 | After the payment-info step, query `GenericAttribute` where `Key = 'ProcessPaymentRequest'` | No card number or CVV in the JSON | **Pass** — JSON holds no card field; no row in `GenericAttribute` or `Log` contains the test card number |
| 5 | Declined test card | Error with the NAB reason; *Failed Transactions* row; confirm again → *card no longer available* | Not run — needs NAB test merchant credentials |
| 6 | Enter card, wait 15 min, confirm | *Card no longer available*; no gateway call, no report row | **Pass** (2026-10-29): Confirm 16 min 14 s after the card was entered showed *Payment error: Your card details are no longer available…*; no order, no `NABXMLTransaction` row, no card data in `GenericAttribute`; nop logged it as *Error while placing order*. On 2026-10-22 an app restart instead was refused by nop first with *Payment information is not entered* (§3) |
| 7 | Reports: search filters, *View* modal (all fields, raw XML, *Close* label), order / customer links | All work | **Pass** with two inserted test rows (after the §2 script fix). Order link shows N/A: the test rows have no matching order |
| 8 | Remove `Payments.NABGateway.Admin.AccessReports` from a role | Menu group hidden; report URLs refused | **Pass** (2026-10-29), with the permission removed from Administrators, its only role: the *NAB Payments* menu group was gone, and `Payments`, `Failed`, `Details` and the `PaymentsList` grid POST all went to *Access denied* (each logged). *Configure* still opened (it needs *Manage payment methods*). With the permission back, menu and reports returned |
| 9 | Switch *Card Payment* on in *Payment methods*; open checkout | *Card Payment* offered with its description; choosing it shows the card form | **Pass** — listed as *Card Payment — Pay with credit / debit card*; the card form opened. With it off, nop skipped the payment-method step (only one method left) |
