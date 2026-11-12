# 4.1 gap work — rounds plan and where to pick up

**Date:** 2026-11-09, updated 2026-11-12. **Owner:** Eduardo. **Status:** Round 1 closed; **Round 2 closed 2026-11-12** (R2-A, R2-B, R2-C); Round 3 next. This is the one document to open when the work resumes.

**What this is.** The order in which the items of the 4.1 → 5.00 gap list are being taken, what is done, and exactly
where the next session starts. It does not repeat the items: each one is described in its source document.

| Source | What it holds |
|---|---|
| `LegacyVersions/NOP41_To_500_Gap_Analysis.md` | Every 4.1 ability 5.00 lacks (groups A–J), with status; done items say so |
| `Subsidy_PackName_Open_Items.md` | The open items (OI numbers below); closed ones are removed, never struck through |
| `Manager_Questions_4.1_Gaps.md` | Q1–Q21, sent to the manager on 2026-11-05 — **no answers received**; from 2026-11-12 each question is put to Eduardo when a round needs it |

## Rounds

The plan agreed on 2026-11-05: first what is broken or already clear, then guards and code health, while the manager
answers the questions; the items that need his answers come after.

### Round 1 — exchanges and e-mails (closed 2026-11-09)

Two parts, each a spec applied by Eduardo, a unit-test suite, a site test run and an approval:

| Part | Items | Record |
|---|---|---|
| **R1-A** (approved 2026-11-09) | D1 / OI 2.1.1 approval e-mails installed; OI 5.6 (part) queued approval e-mail withdrawn on undo; *Notify completion* re-sends the template; D7 / OI 2.1.6 `%PackName.Name%` on the receipt and approval e-mails; F7 / OI 2.1.7 `%Subsidy.Details%` in the welcome e-mail; OI 5.5 plain-order invoice with store credit / exchange fees | `Deprecated/R1A_Return_Emails_Invoice_Spec.md` |
| **R1-B** (approved 2026-11-09) | D5 / OI 2.2.6 action dropdown follows the reason; D6 / OI 2.1.12 reason / action delete refused while used, rules cleaned otherwise; D4 / OI 2.1.5 dead list override deleted (name filters not ported); OI 5.4 no grouped parents in the replacement picker; OI 5.3 found already closed by the port; OI 5.6 (part) dead `IsColourAttribute` overload; the payment e-mail names the additional fee | `Deprecated/R1B_Admin_Returns_Picker_Spec.md` |

**Checked 2026-11-12:** all 91 changes of the two specs are in the code (commit `87ee115`). The ones whose text differs
from the spec were refined during the test run (the "For:" line wording, product-group credit in `%Subsidy.Details%`),
or later by the expiry-reminders work (currency and language in `%Subsidy.Details%`), or applied as an equivalent csproj
entry; the three D4 files are deleted.

Known limits carried from Round 1 (written in the specs, nothing scheduled): a packname whose name contains `"` prints no
"For:" line (nop's tokenizer); an edited or translated payment template does not get the fee sentence automatically;
the fee panel on the admin return page recalculates only after a save.

### Round 2 — small guards and code health (started 2026-11-12)

None of these needs a business decision. Three parts, each run the way Round 1's were:

| Part | Items (Open Items numbers) |
|---|---|
| **R2-A — guards** | 4.5 (`BranchDetail`: missing, deleted or other-store branch), 4.7 (`AddressEdit` GET **and** POST check the address belongs to the branch; `[CheckPermission]` on `SearchBranches`), 4.12 (branch validated on the customer save), 4.13 (`GetManagerStoreIdAsync` reads the list's store, drops deleted packnames and lists), 4.10 (by-product checkout view), 4.17 (`.fail` on the subsidies card loader) |
| **R2-B — code health** | 3.12 and 3.13 (dead / duplicate code), 3.9 (unused model fields), 3.4 (hard-coded labels), 3.3 (literal admin URLs), 3.20 (activity log on every address write and the storefront packname edit), 3.15 (decimal precision in the builders — before go-live, while it is only a builder edit) |
| **R2-C — the two sweeps** | 3.1 (`alert()`, 25 sites) and 3.2 (`@T` inside a `<script>`, 53 sites). **Decided 2026-11-12: its own part**, after R2-B |

**Re-validation (2026-11-12).** Every Round 2 item was re-read against the code (Round 1, the subsidy expiry reminders
and 5.00.1 came after the items were written). All are still open; Open Items has the re-check under each. What
changed in scope:

- **4.5** also covers a deleted branch and a branch of another store (whose shipping methods were offered and filtered
  against the wrong store).
- **4.7** covers the POST `AddressEdit` too — it can overwrite any address by id. `SearchBranches`'s only caller is
  Manage Store, so the guard is Branch **or** MultiStore (an array in `[CheckPermission]` passes on any).
- **4.13** becomes a rewrite on `PackNameList.StoreId`; the branch join is not needed.
- **3.20** is every address write (twelve with the storefront packname edit), not four.
- **3.13**: the two methods differ in order (name vs id), and provisioning / registration rely on id order.

**Decisions (2026-11-12, Eduardo):**

- **4.10:** in the by-product view each row shows its packname, shipping method and address as read-only text;
  editing stays in the by-packname view.
- **R2-C** is its own part.
- **Code:** Claude does not edit plugin code this round; each part is a FIND / REPLACE spec, as in Round 1.

**R2-A closed 2026-11-12** (approved): 4.5, 4.7, 4.10, 4.12, 4.13, 4.17 — record `Deprecated/R2A_Guards_Spec.md` (spec, review, test run 1). Found during its test run and fixed the same day: the Manage Store branch tree's selected path (`_Branches.BranchTree.cshtml`) was written with `Html.Raw` into a quoted attribute, so the tree threw on every reload with a branch selected. Recorded there and not scheduled: deleting / setting the default address of a deleted branch, the refused inactive branch kept in the customer picker on create, the unlocalised shipping warnings.

**R2-B closed 2026-11-12** (approved): 3.3, 3.4, 3.9, 3.12, 3.13, 3.15, 3.20, and one method for a manager's store (`IMultiStoreService.GetManagerStoreIdAsync`, refusing a deleted branch) — record `Deprecated/R2B_Code_Health_Spec.md` (spec, review, test run 1; test order 1013 left on the dev site). Found by its review: Open Item 4.19 (deleting a branch leaves its lists live) — needs a decision; a sixth literal admin URL in `_Branches.BranchDetail.cshtml` (script) — goes to R2-C.

**R2-C closed 2026-11-12** (approved): 3.1, 3.2, 3.14 and the literal `DeleteBranch` URL — record `Deprecated/R2C_Sweeps_Record.md` (changes, review, test run 1). Done by Claude directly under a no-logic-change permission for this part. Left on purpose: lines copied from nop in nop-forked views, and the returns confirmation's blocking print reminder. Found: Open Item 3.21 (16 literal URLs in `admin.store.navigation.js`); `CLAUDE.md` §9.14 says Razor HTML-encodes `@T`, but `T()` returns an `HtmlString` written raw (the rule stands, the explanation could be corrected). The returns wizard part of T10 was not run (no returnable order on the dev site).

Round 2 is ready to commit (Eduardo). Next: Round 3, with Open Item 4.19 (deleting a branch leaves its lists live) still waiting for a decision.

### Round 3 and later — the items that need a business answer

The manager has not answered `Manager_Questions_4.1_Gaps.md`. **From 2026-11-12, when an item needs one of those
questions, Claude asks Eduardo that question when the item is taken up**; he answers it himself where he can, or sends
that single question to the manager again. Each answer goes onto the gap list item (and Open Items) before the work.
The ones rated High or blocking: D3 / Q8 (returns on part-shipped orders, OI 2.1.3), D2 / Q10 (cross-store completion,
OI 2.1.2), C1 / Q1 (order per packname / per branch), C3 / Q3 (order authorisation, OI 2.2.1), A1 / Q14 (the store
settings page).

Already answered: the 5.00.1 product-description question (not one of Q1–Q21) — *Allow JavaScript in rich editor*
stays on for now (`Nop_5.00.1_Upgrade_Record.md` §4).

**5.00.1 (2026-11-12 check).** The fix release changes no file behind a gap item, a Round 2 item, a CW override or a
copied method (`Nop_5.00.1_Upgrade_Record.md` §3); nothing in this plan or the gap list moves. The one link found —
the description sanitizer and size charts kept as tables (G3 / Q18) — is covered while the setting above stays on.

## How each part is run (the working agreement of Round 1)

1. Re-validate the items against the code; ask only what decides the design (AskUserQuestion).
2. Write the spec in `CWPlans/` as exact FIND / REPLACE blocks (bottom to top, line numbers, NEW / WHOLE / DELETE FILE
   labels), generated from a checked working copy. Eduardo applies it; Claude does not edit plugin code.
3. Claude writes the unit tests straight into `Tests/CW.Tests` (Claude owns them), and a sub-agent that did not write
   the spec reviews it against the repository before it reaches Eduardo.
4. After Eduardo applies it: diff every changed file against the working copy, then analyse what else it affects.
5. Eduardo builds, runs the unit tests and the plugin update (DATA version bump); Claude runs the site test plan on
   `https://localhost:59579` in the built-in browser (Eduardo signs in; impersonation is fine; never PayPal or real
   credentials; Claude calls `/logout` only to end an impersonation).
6. On approval: update the documents the spec lists, remove the closed items from Open Items, mark the gap list, move
   the spec to `Deprecated/`. Eduardo commits.

## Dev site state left by Round 1 (`testnop50`)

- Orders 1011 (replacement order of return #1; marked **Complete**, with a shipment marked shipped and delivered) and
  1012 (James Pan, plain order with $20 store credit).
- Returns: #1 authorised (pathway A, order 1011); #1002 (Steve Gates, order 1011, Exact exchange) **pending** after an
  undo — a ready-made pending exchange return for tests; RR 5 plain pending (staff note "R1B T3b").
- Packname "R1A Welcome Test" with customer `r1a.welcome@example.com`; a $20 manual credit note for James Pan.
- Reason–action links restored to their state before the tests (reasons 1–3 → Replacement, Store Credit).
- The "Send emails" task is on; the dev site has no working SMTP, so queued e-mails stay unsent.
