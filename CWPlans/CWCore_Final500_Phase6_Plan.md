# CW.Core move to the final 5.00 — phase 6: outputs compared with 4.9

Phase 6 of `CWPlans/CWCore_Final500_Strategy.md` (§4; beta port strategy §4.3). Every document, e-mail and report
CW.Core produces is made on 5.00 and on 4.9 for the same order, and compared. **Done when:** no difference that is not
a 5.00 change (each difference is named and explained here). Written 2026-10-31.

## 1. The two sites

| | Site | Database | Signed in as admin at |
|---|---|---|---|
| 5.00 | `https://localhost:59579`, used as **`https://127.0.0.1:59579`** | `testnop50` | `127.0.0.1` |
| 4.9 | `https://localhost:65254` | the 4.9 dev database | `localhost` |

Browsers do not separate cookies by port and both sites use nop's cookie names: signing in to one site on `localhost`
signs the other out. So 5.00 is opened on `127.0.0.1` for this phase, a different host, and the two sign-ins stay apart.
Do not open 5.00 on `localhost` during the run.

## 2. What is compared

| # | Output | Made from | 4.9 code |
|---|---|---|---|
| P1 | Invoice PDF, packname order with subsidy (goods and shipping) | admin order → *Invoice (PDF)* | `PdfOverrideService.PrintOrderToPdfAsync`, `CWInvoiceDocument` |
| P2 | Invoice PDF, packname order with store credit and a subtotal discount | same | same (`CWInvoiceTotals`) |
| P3 | Invoice PDF, plain order (no packname) — control | same | nop's |
| P4 | Packing slip PDF, packname order shipped to a destination | admin shipment → *Packaging slip (PDF)* | `PrintPackagingSlipToPdfAsync`, `CWShipmentDocument` |
| P5 | Return / exchange note PDF | storefront return history and admin return request | `CWPdfService.PrintReturnExchangeRequestToPdfAsync` |
| E1 | *Order placed* e-mails (customer, store owner) of P1 and P2's orders: packname rows, settlement totals, exchange fees | *Message queue* | `MessageTokenProviderOverride.WriteTotalsAsync`, `AddOrderTokensAsync` |
| E2 | *Shipment sent* e-mail of P4's shipment: the destination | *Message queue* | `AddShipmentTokensAsync` |
| E3 | `Return.Receipt` | the return of P5 | `ExchangeWorkflowMessageService` |
| E4 | `ReturnAuthorisedPayment.CustomerNotification` / `ReturnAuthorisedExcepted.CustomerNotification` | the return approved | same |
| E5 | `Customer.Subsidy.CustomerRegistrationImport` (welcome) | a manager's *Add packname* with an account | `PackNameWorkflowMessageService` |
| R1 | Profit report with subsidy added back | *Reports → Sales summary / profit* | `OrderReportOverrideService.ProfitReportAsync` |
| R2 | Credit-notes reports (main, liability) | *CW Admin → Credit Notes Report* | `CreditNotesController` |
| R3 | Subsidy history of a packname | customer page / subsidy admin | `subsidy_get_history` |
| R4 | Packname Excel export | Manage Store → PackNames → export | `PackNameBulkService` |
| T | Theme check (`CLAUDE.md` §10.4) | the 5.00 site's themes | — |

PDFs are read as text (`pdftotext -layout`) and compared line by line; e-mails as their queued HTML body; reports as
their grid rows or file content. Layout differences that come from nop 5.00's own PDF renderer (SkiaSharp 4, the new
font) or its e-mail templates are expected and named as such.

## 3. Same order on both sites

Each case is made on both sites with the same customer role, packname kind, products, quantities, shipping method,
discount and payment method, so the amounts match where the code matches. Where the two databases' ids or sample data
differ, the case says what was matched.

## 4. Side effects

Orders (Check / Money Order), shipments, return requests, a credit note and one manager-registered customer on each
site's dev database. E-mails are only queued (no e-mail account is set up to send them). No setting is changed, or if
one is, it is recorded in §5.1.

## 5. Results

Run 2026-10-31 against both sites (tracker artifact `d23f2628-ad7a-4538-b27e-d18498ff98d4`). **14 of 14 checks
pass**, one of them (E1/E2) after a fix. **Approved 2026-10-31.**

### 5.1 Data and settings

- **Matching data.** A $20 credit note to Steve (customer 4) on each site. 4.9: packname 20 *test test* topped up $30;
  order **164** (2 × Fahrenheit 451, subsidy $30, credit $20, payable $4). 5.00: order **9** (Steve's carts for Steve
  Gates, Test 1b and Arthur Holmes; subsidy $973, credit $20), order **10** (Steve Gates → Sydney, Test 1b → Granville),
  shipments 6 (order 9, Arthur's destination) and one for order 10's second destination, return request 1 (order 6),
  manager-registered packname *T6 Long First Name*. Older 4.9 orders (#154, #3), shipment 77 and return #103 were used
  where they already matched.
- **Settings changed on 5.00 (§4 said none would be).** The tax settings were set to 4.9's so the amounts read alike:
  `taxsettings.pricesincludetax = True`, `taxsettings.taxdisplaytype = IncludingTax`, `taxsettings.displaytaxsuffix =
  True`, `taxsettings.shippingistaxable = True`. Before the run they were the sample data's defaults. **Kept** (decided
  2026-10-31): `testnop50` tests with 4.9's tax configuration from now on.

### 5.2 The defect found, and its fix

The order and shipment e-mails printed the order's first address instead of each destination (E1, E2). Cause: the
beta's phase 1 removed the two template-rewrite calls from `CWCorePlugin.InstallOrUpdateAsync` "until phase 3" and they
never came back. Fixed by `CWPlans/CWCore_Final500_Phase6Fixes_Spec.md`, applied 2026-10-31 (plugin version
`2026_10_31_DATA_0.1`; the update ran at startup). After it the eight order templates carry
`%Order.ShippingDestinations%` and the three shipment templates the shipment's own address tokens.

### 5.3 Results

| # | Result | What was compared, and the differences |
|---|---|---|
| P1 / P2 | Pass | Invoices 4.9 #164 / #154 against 5.00 #9 / #6: same blocks and rows in the same order (one *Shipping Information* block per destination, a *For:* line per item, *Total before subsidy and credit*, *Subsidy (items)*, *Store credit*, *Payable balance*). Differences are data and settings: the shipping *incl tax* suffix follows `shippingistaxable`; *Subsidy (shipping)* appears only when the store's subsidy pays shipping; 4.9's *Print:* / *Logo:* lines are its products' attributes. **Not exercised:** a subtotal discount (no order of either site had one) |
| P3 | Pass | Plain order (4.9 #3, 5.00 #8): nop's own invoice on both, no CW rows |
| P4 | Pass | Packing slip, 4.9 shipment 77 against 5.00 shipment 6: the shipment's own address and method, a *For:* line per item |
| P5 | Pass | Return note (4.9 #103, 5.00 #1): same blocks; 4.9's *Return Attributes* only for an item with attributes |
| E1 | Pass after the fix | *Order placed*: order 10's e-mail has one *Shipping Address (packname)* block per destination with its method, as 4.9's #4267; totals block as 4.9's |
| E2 | Pass after the fix | *Shipment sent*: the shipment's destination (Granville), not the order's first (Sydney); template bodies byte-identical to 4.9's |
| E3 | Pass | `Return.Receipt`: identical apart from the order number |
| E4 | Pass | Nothing to send on either site: the two *ReturnAuthorised…* templates are not installed by CW.Core on 4.9 or 5.00. nop's *New return request* e-mails match line for line |
| E5 | Pass | Welcome e-mail: same blocks and nop's password-recovery link; 4.9's lists *Employee number* and *Subsidy* because its packname has them |
| R1 | Pass | Profit with subsidy added back, store credit not: 4.9 $4 + $30 = $34; 5.00 $9,341.56 − $39.80 shipping + $973 = $10,274.76 |
| R2 | Pass | Credit-note liability page and both grids: same columns; figures consistent with the data (5.00: 1 issued, 1 consumed, $0 liability) |
| R3 | Pass | Packname subsidy history: same rows and reason texts; 5.00's $473 + $500 = order 9's $973. **Not exercised on 5.00:** top-up and return-restore rows (no such data) |
| R4 | Pass | Packname export: same 12 columns and value formats; rows on both sites match field for field |
| T | Pass | 5.00 theme is DefaultClean (customers cannot choose), which ships none of the five forked views. ThemeCore and the CW themes are not in this repository yet: re-run `CLAUDE.md` §10.4 step 2 when they arrive |

### 5.4 Open

- The two *ReturnAuthorised…* templates are referenced by code and installed nowhere, on 4.9 too: outside this move,
  noted for later.
- Not exercised here, left to phase 7: a subtotal discount on an invoice, and the top-up and restore rows of the
  subsidy history on 5.00.
- The 5.00 checkout button did not submit once (order 9) and did for order 10: watch for it in phase 7.
