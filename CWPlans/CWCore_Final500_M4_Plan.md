# CW.Core move to the final 5.00 — M4: the bulk-ordering tests left for the final release

Part M4 of `CWPlans/CWCore_Final500_Strategy.md` (§4). The runtime tests of part 5h that the beta could not run
(beta port strategy §7.4): T5, T8b, T9–T16, T18, T19 of `NOP50Beta/CWPlans/Deprecated/CWCore_Phase5h_Spec.md` §6, whose
steps and expected results apply unchanged. Written 2026-10-31.

## 1. Where and as whom

`testnop50`, store 1, Claude's built-in browser. Steve Gates (customer 4, packname manager) impersonated from the
admin, *Bulk ordering* on (it is, since M3's F.1 left every switch as it started).

## 2. Data — all from M3, nothing new

| Test | Uses |
|---|---|
| T5 | *Apparel → Shoes → Running* (category 17) |
| T8b–T13 | packname *Steve Gates* (restricted to product group 1): *adidas Consortium Campus 80s* (27), size × colour combinations, combination 1 (Size 8, Red) with 2 in stock; pins are the selects above its grid |
| T14 | *Obey Propaganda Hat* (33) requires *Nike Tailwind* shirt (29), added automatically |
| T15 | packname 23 *Tom & Jerry*, subsidy 2's ordering period closed |
| T16 | *Build your own computer* (1): its *Software* attribute is a checkbox list |
| T18 | every card |
| T19 | orders 6 and 7 (Steve's packnames) for *Latest ordered*; order 8 (James) as the other customer's id |

## 3. Side effects

Cart lines on Steve's packnames, and his activity log. Nothing is ordered. Temporary changes, all set back after the
run (checked): store 1's `BulkOrderPageSizeOptions` overridden to `1,15,30,50,100` (T5's and T12's paging: no category
holds more than 15 of the sample products) and `BulkOrderMaxCellsPerProduct` lowered from 400 to 6 (T8b–T13 need
select pins, which appear only over the cell budget; the sample grids have at most 24 cells); product 33's required
product set to 2 for one add (T14).

## 4. Results

**Run 2026-10-31: 12 of 12 pass.** Results, one row per test, in the tracker artifact *CW.Core M4 bulk-ordering tests
— test run*. No defect from the port. What differs from 5h's wording:

- **T11:** Claude's browser has no DevTools *Offline* switch; the grid request was made to fail as offline does (the
  page's `fetch` for `/bulkordering/grid` rejected with a network error).
- **T14:** with M3's data the required product (the Tailwind shirt) has a required *Size*, which nop cannot choose when
  it auto-adds, so nop refuses with its generic warning (shown as text, as T14 checks). The auto-add itself was
  checked with a required product that has no options.
- **T15:** the closed-ordering cart warning cannot be reached from the bulk page. The selector disables the packname,
  and once it is selected anyway the catalogue gate shows it no product, so an add from an older page is refused with
  *This product is no longer available.* The closed-ordering text shows in the info strip with `&` as `&`, and the bulk
  page decodes every warning to text (`BulkOrderLogic`), so the encoded name would read correctly had it been reached.
  The 5h expectation predates the gate's "a packname that cannot order sees nothing".

Also in the log during the run, neither from CW.Core's port: *Resource string (common.loading) is not found* (nop's
own `OnePageCheckout.cshtml` uses the key too, and CW's packname checkout copies it; 5.00 has no such resource), and
*Google Analytics. Error processing shopping cart from server side* (nop's `Widgets.GoogleAnalytics`, not configured
on `testnop50`).
