# 4.1 gap work — rounds plan and where to pick up

**Date:** 2026-11-09. **Owner:** Eduardo. **Status:** Round 1 closed; **Round 2 not started** (paused for higher-priority
work). This is the one document to open when the work resumes.

**What this is.** The order in which the items of the 4.1 → 5.00 gap list are being taken, what is done, and exactly
where the next session starts. It does not repeat the items: each one is described in its source document.

| Source | What it holds |
|---|---|
| `LegacyVersions/NOP41_To_500_Gap_Analysis.md` | Every 4.1 ability 5.00 lacks (groups A–J), with status; done items say so |
| `Subsidy_PackName_Open_Items.md` | The open items (OI numbers below); closed ones are removed, never struck through |
| `Manager_Questions_4.1_Gaps.md` | Q1–Q21, sent to the manager on 2026-11-05 — **no answers received yet** |

## Rounds

The plan agreed on 2026-11-05: first what is broken or already clear, then guards and code health, while the manager
answers the questions; the items that need his answers come after.

### Round 1 — exchanges and e-mails (closed 2026-11-09)

Two parts, each a spec applied by Eduardo, a unit-test suite, a site test run and an approval:

| Part | Items | Record |
|---|---|---|
| **R1-A** (approved 2026-11-09) | D1 / OI 2.1.1 approval e-mails installed; OI 5.6 (part) queued approval e-mail withdrawn on undo; *Notify completion* re-sends the template; D7 / OI 2.1.6 `%PackName.Name%` on the receipt and approval e-mails; F7 / OI 2.1.7 `%Subsidy.Details%` in the welcome e-mail; OI 5.5 plain-order invoice with store credit / exchange fees | `Deprecated/R1A_Return_Emails_Invoice_Spec.md` |
| **R1-B** (approved 2026-11-09) | D5 / OI 2.2.6 action dropdown follows the reason; D6 / OI 2.1.12 reason / action delete refused while used, rules cleaned otherwise; D4 / OI 2.1.5 dead list override deleted (name filters not ported); OI 5.4 no grouped parents in the replacement picker; OI 5.3 found already closed by the port; OI 5.6 (part) dead `IsColourAttribute` overload; the payment e-mail names the additional fee | `Deprecated/R1B_Admin_Returns_Picker_Spec.md` |

Known limits carried from Round 1 (written in the specs, nothing scheduled): a packname whose name contains `"` prints no
"For:" line (nop's tokenizer); an edited or translated payment template does not get the fee sentence automatically;
the fee panel on the admin return page recalculates only after a save.

### Round 2 — small guards and code health (next; not started)

None of these needs a business decision. Proposed as two parts, the same way as Round 1:

| Part | Items (Open Items numbers) |
|---|---|
| **R2-A — guards** | 4.5 (a branch id that does not exist → 500), 4.7 (address-belongs-to-branch check; `[CheckPermission]` on `SearchBranches`), 4.12 (branch validated for a customer without a packname), 4.13 (deleted lists / branches in `GetManagerStoreIdAsync`), 4.10 (by-product checkout view), 4.17 (`.fail` on the subsidies card loader) |
| **R2-B — code health** | 3.12 and 3.13 (dead code), 3.9 (unused model fields), 3.4 (hard-coded labels), 3.3 (literal admin URLs), 3.20 (missing activity-log rows), 3.15 (decimal precision in the builders — before go-live, while it is only a builder edit) |
| **R2-C — the two sweeps (optional)** | 3.2: `@T` inside a `<script>` (53 sites) and `alert()` (24 sites). Its own part, or "fix when next touched" — **Eduardo to decide at the start of Round 2** |

Start of Round 2: re-read the items against the code first (they were written against the code of 2026-10-31; Round 1
changed exchanges code, not these areas), then write the R2-A spec.

### Round 3 and later — waiting on the manager

Taken once `Manager_Questions_4.1_Gaps.md` is answered; fold each answer into the gap list (and Open Items) first.
The ones rated High or blocking: D3 / Q8 (returns on part-shipped orders, OI 2.1.3), D2 / Q10 (cross-store completion,
OI 2.1.2), C1 / Q1 (order per packname / per branch), C3 / Q3 (order authorisation, OI 2.2.1), A1 / Q14 (the store
settings page).

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
