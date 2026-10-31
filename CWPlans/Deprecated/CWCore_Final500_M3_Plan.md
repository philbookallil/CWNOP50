# CW.Core move to the final 5.00 — M3: the new database and the runtime pass

Part M3 of `CWPlans/CWCore_Final500_Strategy.md` §4. Written 2026-10-30. No code changes: M3 installs CW.Core on the
final 5.00's database, fills it with test data and checks at runtime what the build cannot see.

**State (2026-10-30):** steps 1–4 done (CW.Core installed and checked, test-data scripts ready). Next: step 5.

---

## 1. Steps

| # | Who | Step | Done when |
|---|---|---|---|
| 1 | Eduardo | **Install CW.Core**: Admin → Configuration → Local plugins → *CW Core* → Install, then restart the site. Open Admin → System → Log and note any error the install wrote | The plugin shows as installed; the site starts |
| 2 | Eduardo | **Run `CWPlans/TestData/CWCore_M3_InstallCheck.sql`** (read only) on `testnop50`, and on `testnop50beta` if it is still there; paste the grids | — |
| 3 | Claude | **Check the install** from the grids: 43 tables, 11 views, 2 functions, 2 procedures, nothing missing or left over; resources, permissions, settings and the schema migration as on the beta; the settings of §2 | Every row matches, or a defect goes to Eduardo as a question |
| 4 | Claude | **Test data** (`CWPlans/TestData/`, local): the beta's scripts fit this database (§5) and are copied; the catalogue the deferred tests need and the vendor settings are set up through nop's admin at the start of the run (group 0) | Scripts in place |
| 5 | Eduardo | In SSMS on `testnop50`: `CWCore_Phase4a_TestData.sql` PART 1 (select from its start to *END OF PART 1*, F5), then `CWFreight_TestShippingMethods.sql`; restart the site (nop caches settings) | — |
| 6 | Claude | **Runtime pass** (§3) in the browser, results in a tracker artifact; defects come back as questions | Every check passes |

## 2. Settings to keep as decided (beta strategy §7.3)

| Setting | Value | Why |
|---|---|---|
| `ReturnRequestSettings.GuestReturnRequestsAllowed` | off | the withdrawal form (D3) |
| `OrderSettings.AutoCancelEnabled` | off | auto-cancel of unpaid orders would bypass CW's cancel (subsidy and credit returned) |
| `OtpSettings.LoginByPhoneEnabled` | off | sign-in by phone code skips nop's sign-in path, so CW's landing and packname set-up |
| `ShoppingCartSettings.VendorEnabled`, vendors | off, none active | stashed packname lines would vanish or double |
| Price lists | none active | per-role prices with the packname role set (beta §5.1) |
| `ReminderSettings.*Enabled` | used (D3) | as Eduardo sets them |

A fresh install may switch some on by default; PART 2 of the check shows them.

## 3. Runtime pass

One test run, one tracker artifact. Asked once for the whole run (Eduardo's rule); the side effects are in §4.

| Group | What |
|---|---|
| **0. Set-up in admin** | Vendors off (§2: *Allow customers to apply for vendor account* off, the two sample vendors deactivated); the sub-sub-category *Apparel → Shoes → Running* with both sample shoes; size × colour combinations for *adidas Consortium Campus 80s Running Shoes* (stock by attributes, one combination with 2 units); *Obey Propaganda Hat* requires *Nike Tailwind Loose Short-Sleeve Running Shirt* (added automatically); a product group and a subsidy assigned to the test packnames; a packname whose name has `&` with a closed ordering period |
| **A. Install** | The *CW Admin* menu and every CW admin page load (MultiStore, branches, departments, cost centres, packname lists and packnames, import, subsidies, product groups, credit notes and reports, exchanges configuration, return requests, the order page's packname card, bulk ordering, store and default settings); the customer edit page shows CW's tabs |
| **B. Views (strategy §2, M2 T5)** | A forked storefront view (*Order details*) and a forked admin view (*Customer edit*) render CW's version; CW pages opened by path load under `DefaultClean`: the packname checkout (address and payment partials refresh), the subsidy catalogue (top-up dialogs), the bulk ordering page |
| **C. M2's checks** | T1–T7 of `CWCore_Final500_M2_Spec.md` §4 |
| **D. Storefront flows, one each** | Sign in as a packname manager → landing; packname selector and info strip; subsidy catalogue and bar; packname cart and checkout to a placed order (Check / Money Order); order details and history; My Subsidies, My Store Credit; the returns wizard on a shipped order; bulk ordering add to cart |
| **E. Control case** | An ordinary customer with no packname: catalogue, cart and nop's checkout work unchanged |
| **F. Feature switches** | Each CW feature switched off and on in one running site: its entry points close and reopen |

M4 (5h's deferred tests T5, T8b, T9–T16, T18, T19) follows on the same data.

## 4. Side effects of the run (dev database only)

- Creates customers, packnames, carts, orders (Check / Money Order), shipments, return requests, credit notes and
  subsidy consumption.
- Impersonates customers from the admin; signs out only an impersonation that is running.
- Switches CW features and the settings above off and on, and leaves them as they started.
- **T7 permanently deletes (GDPR) one test customer** created for it; nothing else is deleted — records are
  deactivated instead.
- No real payment, no real credentials: sign-in forms (login, registration, password recovery) are Eduardo's steps.

## 5. Results

**Step 2–3, install check (2026-10-30)** — `CWCore_M3_InstallCheck.sql` on `testnop50`:

| Check | Result |
|---|---|
| Database objects | 43 / 43 tables, 11 / 11 views, 2 / 2 functions, 2 / 2 procedures; nothing missing, nothing left over |
| Schema migration | `CW.Core schema` applied 2026-10-30 19:30 |
| Resources | 1,162 = the `Plugins.CW.Core.*` keys in the code |
| Permissions | 10 = the code's three permission managers (`Orders.AccessPayments` is CW's though not named `CW.`; the check's `CW.%` filter counted 9 and 10 role mappings) |
| `MultiStoreSettings` | the 5 rows the install writes (the feature switches and the own-placement payment methods) |
| Settings of §2 | auto-cancel, phone sign-in, guest returns (withdrawal), vendor carts: off. Reminders: all three on. No price lists. **Vendors: *Allow customers to apply for vendor account* is on and the sample data's two vendors are active** — switched off in group 0 |
| Data | sample data installed; store 1 `https://localhost:59579/`; customers 4–8 are Steve Gates, Arthur Holmes, James Pan, Brenda Lindgren, Victoria Terces — the ids the beta's scripts use |

**Step 4, test data.** nop's installation data and `Nop.Core` are identical in the beta and the final, so the beta's
scripts fit: country and state ids (Australia 14, NSW 46, QLD 48, SA 49, VIC 51) and the sample customers' ids are the
same. Copied to `CWPlans/TestData/`: `CWCore_Phase4a_TestData.sql` and `CWCore_Phase4e_TestData.sql` unchanged;
`CWFreight_TestShippingMethods.sql` and `CWSubsidy_TestPastSeason.sql` with the database name changed to `testnop50`.
The sample data has no attribute combinations, no required product, no third category level and no subsidy: group 0
creates them in the admin.

**Step 5, runtime pass (2026-10-30)**: 31 of 31 checks pass. **M3 approved by Eduardo 2026-10-30.** The uninstall check
(*Install / uninstall clean*) is deferred by his decision: the data above is kept for M4, and the uninstall runs later. Results, one row per check, are in the tracker
artifact *CW.Core M3 runtime pass — test run*. Data the run left on `testnop50`:

| What | Rows |
|---|---|
| Group 0 | category 17 *Running*; product 27's combinations 1–12; product 33 requires 29; product group 1; subsidies 1 (Summer Uniform 2026, packnames 1 and 13) and 2 (Closed Season 2026 H1, packname 23 *Tom & Jerry*) |
| Orders | 6 (Steve, subsidy-paid, delivered), 7 (Test 1b, pending), 8 (James, nop's checkout, Complete) |
| Returns | return #1 on order 6 (Exact Exchange, Pending) |
| Return rules, store 1 | reasons 1–3 active (exchange + credit; reason 2 with fees and back in stock); actions Replacement (exchange) and Store Credit (credit), linked to every reason; statuses Received and Return authorized |
| T1 | category 17 display orders swapped (Nike Floral 1, adidas 2) |
| T6 / T7 | packname 24 *T6 Long First Name* (inactive); its customer 11 permanently deleted (GDPR) |
| Settings | every switch is as it started; GDPR switched on for T7 only, then off |

**Findings**, none of them a regression from the port, and what was decided (2026-10-30):

1. **Empty return lists block the wizard with no message.** With *Exchanges* on and no active reason rule for the
   store (the state after a fresh install), the return page shows required but empty *Return reason* / *Return action*
   lists. The submit does nothing and no message appears. "No rule = not offered" is decided behaviour (5g, changes
   47–50). **Decided: the page shows a message instead of the form** — `CWCore_Final500_M3Fixes_Spec.md` changes 1–3, 6.
2. **Actions are filtered with *Exchanges* off.** `ReturnRequestOverrideModelFactory.PrepareSubmitReturnRequestModelAsync`
   gates the reason filter on the switch, but always builds the actions from `GetActiveActionsByReasonIdAsync`. With
   *Exchanges* off, nop's page offers only the actions with an active CW rule (Repair hidden), not nop's list.
   **Decided: fix** — with *Exchanges* off the page keeps nop's lists and the cascade is not wired (M3 fixes, changes 3–4).
3. **T4 wording in `CWCore_Final500_M2_Spec.md` §4.** With *Exchanges* on and no withdrawal token, `ReturnRequestSubmitFilter`
   sends nop's post back before the quantity check. The check, and CW's message, apply with *Exchanges* off or a
   guest token; the test was run that way. **Done:** the M2 spec's T4 now says so (documentation only).
4. **T6 on the import.** The Excel import refuses a first name over 1000 characters (its per-column `MaxLength`
   check) before writing. So the M2 cut matters for the storefront form only, and is a backstop on the import.
   **Decided: both refuse** — the form checks the account's lengths as the import does (M3 fixes, change 5); the cut
   stays as a backstop.
5. **Outside CW.Core.** `ExternalAuth.SSO` has no `logo.jpg` (four 404s for `/Admin/Plugin/null` on the plugin list).
   `Misc.RFQ` is installed on `testnop50`: its *Request a quote* button shows on the packname checkout and *Requests for
   quote / Quotes* in the account menu. `AIRecommendation.GoogleAI` is installed and enabled but did not serve searches.
   **Decided: not changed now** (only CW.Core is in scope); recorded in the strategy's §5 for later.
6. **Home-page console errors** seen early in the run (`$`, `Swiper`, `mainMenu` not defined) did not come back; the
   remaining console errors are Visual Studio's Browser Link sockets and resources the browser pane blocks.

T7 confirmed in SQL by Eduardo: `SELECT COUNT(*) FROM ShoppingCartItem WHERE CustomerId = 11` returned 0.
