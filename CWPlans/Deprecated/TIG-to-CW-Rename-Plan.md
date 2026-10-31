# TIG → CW Rebrand Plan (Code Phase)

> **4.9 history.** Copied from the 4.90 source (`NOP49/CWPlans/`) on 2026-10-31, when CW.Core moved to nopCommerce 5.00. It is kept as the record of the 4.9 work and is **not updated for 5.00**: file paths, line numbers and versions are 4.9's. For how CW.Core works now, read the references in `CWPlans/` and `CWPlans/CWCore_Port_Reference.md`.

**Status:** Tier 2 (per CLAUDE.md §0.3) — spec for programmer implementation. Claude does not touch source files per §0.1; this document is the instruction set.

**Scope of this document:** Phase 1 only — rename code, files, folders, namespaces. **Database objects (tables, columns, views, stored procedures) stay exactly as they are today.** Phase 2 (DB rename) is sketched at the end as a placeholder, to be planned in detail as its own Tier 2 task once Phase 1 is merged and stable.

---

## 0. The one thing that will bite you if skipped

Not every `TIG` you'll find is "just code." Some `TIG`-prefixed identifiers are also **persisted as data** in the database or in config, keyed by their *string value* — not by the C# type. If you rename the C# side without a matching data migration, you get silent breakage: rows in the DB stop matching, permissions silently stop granting, translations disappear, payment methods stop being recognized on old orders.

Rule of thumb used throughout this plan:

- **Pure code identifier** (namespace, class name, file/folder name, project name) → safe to rename now, zero DB impact.
- **Code identifier whose *string value* is also written into a DB row or config file** (plugin `SystemName`, `LocaleStringResource` keys, `Permission` system names, payment/message-template system names, `Setting` key prefixes derived from an `ISettings` class name, `GenericAttribute` names, cache key prefixes) → the C# symbol can be renamed, but the **string literal must stay pointing at the existing persisted value** until Phase 2 does a proper data migration (insert-new/update-old, not a blind find‑replace). Renaming the string today would orphan existing data on any environment that already has the plugin installed.
- **Literal DB object name** (table/column/view/SP name in `NameCompatibility` dictionaries, migration builders, raw SQL) → **do not touch** in Phase 1 at all. This is explicitly deferred to Phase 2.

Section 4 below lists exactly which items in this codebase fall into each bucket.

---

## 1. Inventory (confirmed by direct inspection of the repo)

### 1.1 Plugin folders to rename

| Current | New | Files touching "TIG" | Notes |
|---|---|---|---|
| `Plugins/Nop.Plugin.TIG.Core` | `Plugins/Nop.Plugin.CW.Core` | 664 / 725 | The big one — see §3 |
| `Plugins/Nop.Plugin.TIG.Exchanges` | `Plugins/Nop.Plugin.CW.Exchanges` | 5 / 12 | Not currently wired into `NopCommerce.sln` |
| `Plugins/Nop.Plugin.TIG.Freight` | `Plugins/Nop.Plugin.CW.Freight` | 38 / 53 | |
| `Plugins/Nop.Plugin.TIG.NAB.XMLPayment` | `Plugins/Nop.Plugin.CW.NAB.XMLPayment` | 5 / 12 | |
| `Plugins/Nop.Plugin.TIG.Subsidy` | `Plugins/Nop.Plugin.CW.Subsidy` | 2 / 6 | Not currently wired into `NopCommerce.sln` |
| `Plugins/Nop.Plugin.TIG.ThemeCore` | `Plugins/Nop.Plugin.CW.ThemeCore` | 36 / 50 | Includes `Views/Components/TigThemeHead` (mixed-case — see §3.3) |

`Presentation/Nop.Web/Plugins/TIG.*` are **build output only** (git-ignored via `.gitignore:182`) — nothing to rename there, they regenerate under the new plugin folder names on next build. Just confirm the `.gitignore` pattern (`Presentation/Nop.Web/Plugins/*`) still matches after rename (it will, it's a wildcard).

### 1.2 Shared library

| Current | New |
|---|---|
| `TIGLibraries/` (solution folder) | `CWLibraries/` |
| `TIGLibraries/TIG.Common/` | `CWLibraries/CW.Common/` |
| `TIGLibraries/TIG.Common/TIG.Common.csproj` | `CWLibraries/CW.Common/CW.Common.csproj` |

### 1.3 Theme

| Current | New |
|---|---|
| `Presentation/Nop.Web/Themes/TIG.Standard/` (58 git-tracked files, includes `Content/css/tig-brand.css`) | `Presentation/Nop.Web/Themes/CW.Standard/` |

### 1.4 Planning docs folder

| Current | New |
|---|---|
| `TIGPlans/` (solution folder + 5 markdown files, one named `TIG_Theme_Architecture_Foundation.md`) | `CWPlans/` (rename folder and that file to `CW_Theme_Architecture_Foundation.md`) |

### 1.5 Solution file (`NopCommerce.sln`)

Update every `Project(...)` line and nested solution-folder file mapping that references any of the paths/names above (9 lines currently contain `TIG`).

### 1.6 Flagged, not in scope

`NopCommerce.sln` also references a project `Nop.Plugin.TIG.Theme.Standard` at `Plugins\Nop.Plugin.TIG.Theme.Standard\Nop.Plugin.TIG.Theme.Standard.csproj` — **this folder does not exist on disk.** It's a pre-existing stale/broken reference, unrelated to this rename. Recorded here per §0.5 (scope discipline) — recommend a separate follow-up ticket to either restore or remove that `.sln` entry. Do not fix it as part of this rename (removing it would need its own review, since it's not clear whether it's meant to point at `ThemeCore` or a deleted project).

---

## 2. Execution order

Work in a dedicated branch (e.g. `rename/tig-to-cw-code`). Do not run this alongside other active feature branches touching `TIG.Core` — a 664-file mechanical diff will conflict with almost anything in flight. Get a green baseline first:

1. `dotnet build NopCommerce.sln` — confirm it's currently green.
2. Run `Tests/Nop.Tests` — confirm current pass/fail baseline (per CLAUDE.md §22 Note A, TIG plugins have no dedicated tests today, so this mainly guards against breaking core).
3. Create the branch.

Then, per plugin/folder, in this order (folders first, then identifiers inside them — doing it in this order means every subsequent `grep` for `TIG` is scoped to a shrinking set of already-relocated files):

### Step A — Folder & file renames (`git mv`, preserves history)

For each plugin folder in §1.1:

```bash
git mv Plugins/Nop.Plugin.TIG.Core Plugins/Nop.Plugin.CW.Core
git mv Plugins/Nop.Plugin.CW.Core/Nop.Plugin.TIG.Core.csproj Plugins/Nop.Plugin.CW.Core/Nop.Plugin.CW.Core.csproj
```

Then rename every file whose *name* contains `TIG`, e.g. inside `Nop.Plugin.CW.Core`:

```
TIGNameCompatibility.cs          → CWNameCompatibility.cs
TIGCoreDefaults.cs               → CWCoreDefaults.cs
TIGCorePlugin.cs                 → CWCorePlugin.cs
TIGCustomerAttributeNames.cs     → CWCustomerAttributeNames.cs
TIGCustomerRoleNames.cs          → CWCustomerRoleNames.cs
TIGOrderCustomValuesKeys.cs      → CWOrderCustomValuesKeys.cs
TIGPaymentMethodSystemNames.cs   → CWPaymentMethodSystemNames.cs
TIGWidgetsZones.cs               → CWWidgetsZones.cs
TIGMessageTemplateSystemNames.cs → CWMessageTemplateSystemNames.cs
TIGCreditNote*.cs                → CWCreditNote*.cs
TIGFeeRule.cs                    → CWFeeRule.cs
TIGImpersonationToken.cs         → CWImpersonationToken.cs
TIGReasonActionRelationship.cs   → CWReasonActionRelationship.cs
TIGReturn*.cs                    → CWReturn*.cs
TIGMapperConfigurationProfile.cs → CWMapperConfigurationProfile.cs
TIGAddressService.cs             → CWAddressService.cs
TIGPdfService.cs                 → CWPdfService.cs
TIGCustomerServiceDefaults.cs    → CWCustomerServiceDefaults.cs
TIGDatabaseService.cs            → CWDatabaseService.cs
TIGTokenGroupNames.cs            → CWTokenGroupNames.cs
TIGOrderService.cs               → CWOrderService.cs
TIGReturnRequestService.cs       → CWReturnRequestService.cs
TIGPermissionConfigManager.cs    → CWPermissionConfigManager.cs
TIGCustomerController.cs         → CWCustomerController.cs
TIGAddressModelFactory.cs        → CWAddressModelFactory.cs
TIGSettingModelFactory.cs        → CWSettingModelFactory.cs
TIGAddressValidator.cs           → CWAddressValidator.cs
```

(this is the confirmed list from a repo scan — treat it as the checklist, not exhaustive on faith; re-run `grep -rl "TIG" <folder>` after the folder move to confirm nothing was missed before moving to Step B).

Use a small script rather than doing 700+ individual `git mv` calls by hand:

```bash
# run from repo root, one plugin folder at a time
find Plugins/Nop.Plugin.CW.Core -depth -iname '*TIG*' | while read -r f; do
  newname=$(echo "$f" | sed -e 's/TIG/CW/g' -e 's/Tig/Cw/g')
  git mv "$f" "$newname"
done
```

(`-depth` walks children before parents so renaming a directory doesn't invalidate paths still queued below it.) Do this for each plugin, `TIGLibraries`, and `TIGPlans`.

Then handle the theme folder and `TIGLibraries`/`TIGPlans` themselves the same way.

### Step B — `NopCommerce.sln` updates

Manually edit the 9 `TIG`-referencing lines identified in §1.5 to point at the new paths/names/GUIDs stay the same, only names and paths change.

### Step C — Namespace & identifier rename (content, not filenames)

Now that files/folders are moved, do a scoped, reviewable find-replace **inside each renamed folder only** (never touch `Libraries/`, `Presentation/Nop.*`, `Tests/Nop.Tests`, or other OOTB plugins — cardinal rule).

Two case-sensitive passes, in this order (order matters — do the longer/more specific token first so a later pass doesn't corrupt something the first pass already produced):

1. `TIG` → `CW` (namespaces `Nop.Plugin.TIG.*` → `Nop.Plugin.CW.*`, class/type names, constants, comments, XML doc, `plugin.json` `SystemName`/`Group`/`FileName` — **except the persisted-data exceptions in §4**)
2. `Tig` → `Cw` (mixed-case occurrences — confirmed to exist, e.g. `TigThemeHead` component folder/class in ThemeCore → `CwThemeHead`)

Suggested per-file command, restricted to text files and excluding build output:

```bash
grep -rlZ --include='*.cs' --include='*.cshtml' --include='*.json' --include='*.csproj' \
  -E 'TIG|Tig' Plugins/Nop.Plugin.CW.Core \
  | xargs -0 sed -i 's/TIG/CW/g; s/Tig/Cw/g'
```

**Do not run this blind and commit.** Run it, then:

- `git diff` the whole branch and read it — 664 files is a lot, but this is a pure mechanical rename; skim every hunk for anything that doesn't look like an identifier/namespace/comment swap.
- Specifically search the diff for the string `TIG_` (table-name prefix) — if any diff hunk shows `TIG_` being changed, **revert that hunk**. Table names must not change in Phase 1 (see §4).
- Specifically check `plugin.json` for each plugin — `SystemName` needs special handling, see §4.1.

### Step D — Build & test

1. `dotnet build NopCommerce.sln` — must be green with zero new errors/warnings beyond baseline.
2. Re-run `Tests/Nop.Tests`.
3. Fresh-install smoke test against a copy of the DB (not production): install the renamed plugins in a scratch environment and confirm plugin registration, branch/PackName selector widget, subsidy calculation, exchange/return flow, and freight shipping calc still work end to end. This exercises exactly the persisted-data risk areas called out in §0 and §4.

---

## 3. Special cases inside the identifier rename

### 3.1 `plugin.json` metadata

Each plugin's `plugin.json` has `Group`, `FriendlyName`, `SystemName`, `Author`, `Description` fields containing `TIG`/`Total Image Group`/`TIG Team`. `FriendlyName`, `Description`, and `Author` are safe to reword freely — but you'll need the actual new company name (not just the "CW" initials) for `Author`/`Description` text; that's a business decision, not a technical one, so get that string from whoever owns the rebrand before editing those fields. `SystemName` is **not** safe to blind-rename — see §4.1.

### 3.2 Cache key prefixes

`TIG.packname.*`, `TIG.subsidy.*`, `TIG.departments.*` (see CLAUDE.md §16) are pure in-memory `IStaticCacheManager` keys, rebuilt fresh on every app start. Safe to rename freely — no persisted data depends on them.

### 3.3 `TigThemeHead` component

`Plugins/Nop.Plugin.TIG.ThemeCore/Views/Components/TigThemeHead` — mixed-case folder and (presumably) a `TigThemeHeadViewComponent` class. Confirm the exact class name when you get there; rename folder, class, and the `<vc:tig-theme-head>` (or similar) tag-helper invocations in any `.cshtml` that references it by kebab-case name (ASP.NET Core view components are invoked by a kebab-cased version of the class name — e.g. `tig-theme-head` — so `.cshtml` files elsewhere in the theme or other plugins may reference it by that string and need updating too).

### 3.4 CSS/JS/asset file contents

`Content/css/tig-brand.css` (theme) and any `lib/` JS with `TIG` in variable names or comments — sweep these too; the same regex passes in Step C should be pointed at `.css`/`.js`/`.scss` files inside the theme folder, not just `.cs`/`.cshtml`.

---

## 4. Items that are code-renamed now but need a Phase 2 data migration later

Do **not** skip renaming these C# symbols in Phase 1 — but the underlying string **value** must stay pointed at the old, still-existing DB/config data until Phase 2 ships a proper migration. Concretely: rename the class, keep the string literal it holds unchanged, and leave a `// TODO(CW-rename-phase2):` comment on each so they're easy to find later.

### 4.1 Plugin `SystemName` (`plugin.json`)

NopCommerce tracks installed plugins by `SystemName` string (`TIG.Core`, `TIG.Freight`, `TIG.ThemeCore`, etc.) in its plugin registry/config. Changing this string looks to nopCommerce exactly like uninstalling the old plugin and installing a brand-new one — permission assignments, widget-zone registrations, and payment/shipping method activation that reference the old `SystemName` would need re-linking. **Recommendation:** leave `SystemName` as `TIG.Core` / `TIG.Freight` / etc. in Phase 1, even though the folder, namespace, and `FriendlyName` are now `CW.*`. Flip `SystemName` only as part of a deliberate Phase 2 cutover migration that also updates whatever config/DB rows reference it.

### 4.2 `TIGNameCompatibility` table/view name values

The **class** `TIGNameCompatibility` → `CWNameCompatibility` (safe, code-only). The **dictionary values** inside it (`"TIG_CO_Branch"`, `"TIG_SUB_Subsidy"`, `"vw_TIG_SUB_SubsidyPackNameCredit"`, etc.) must stay exactly as-is — they map to real, unrenamed tables/views. Same for every `NopEntityBuilder<T>` and FluentMigrator migration file that names a table.

### 4.3 `TIGPaymentMethodSystemNames`, `TIGMessageTemplateSystemNames`

These almost certainly hold string constants persisted on existing `Order` rows (`PaymentMethodSystemName`) and `MessageTemplate.Name` rows. Rename the class, **do not** change the string values it contains.

### 4.4 Locale resource keys (`Plugins.TIG.*.Fields.*`)

These are inserted into the `LocaleStringResource` table on plugin install (`AddOrUpdateLocaleResourceAsync`). If you rename the key strings in code without a migration, environments that already have the plugin installed keep the *old* keys in the DB while new code looks up the *new* keys — translations silently disappear. Leave these resource-key strings as `Plugins.TIG.*` in Phase 1; convert them in Phase 2 alongside the DB work, via an update migration that re-keys existing rows (not a fresh insert).

### 4.5 `TIGPermissionConfigManager` permission system-name strings (`TIG.CORE.Admin.*`)

Same shape of risk as 4.4 — these are persisted in the `Permission`/`PermissionRecordCustomerRoleMapping` tables. Renaming the string without migrating existing rows would silently revoke access for every role currently granted the old permission key. Rename the class; keep the key strings as `TIG.CORE.Admin.*` until Phase 2.

### 4.6 `GenericAttribute` names (`TIGCustomerAttributeNames`)

`StoreId`, `BranchId`, `EmployeeId`, etc. as documented in CLAUDE.md §15.1 are stored as rows in the `GenericAttribute` table keyed by these exact string names. Looking at the actual constant values, they may not literally contain the substring `TIG` (worth double-checking when you're in the file) — if any of them do, treat them the same as 4.4/4.5.

---

## 5. Phase 2 (DB rename) — placeholder only

Per your instruction, this is not detailed today. It becomes its own Tier 2 plan once Phase 1 is merged and stable in an environment. It will need to cover, at minimum:

- Renaming `TIG_CO_*`, `TIG_SUB_*`, `TIG_EX_*` tables and `vw_TIG_*` views (via `Rename.Table`/`Rename.Column` FluentMigrator steps, not drop/recreate, to preserve data).
- Renaming the two stored procedures (`packname_get_listids_by_manager`, `subsidy_get_history`) if they're to lose any `TIG`/`tig` references, and updating `ITIGDatabaseService` call sites.
- The deferred string-literal renames from §4 (plugin `SystemName`, locale resource keys, permission system names), each via an explicit "rename in place" migration rather than uninstall/reinstall.
- A rollback plan, since this phase touches live data rather than pure code.
- A maintenance-window / backup requirement, unlike Phase 1 which can ship as a normal code change.

---

## 6. Checklist (per CLAUDE.md §0.4)

- [ ] Branch created off a green baseline build + test run
- [ ] Step A: all 6 plugin folders + `TIGLibraries` + `TIGPlans` + theme folder moved via `git mv`
- [ ] Step B: `NopCommerce.sln` updated (9 lines)
- [ ] Step C: namespace/identifier rename done per plugin, `git diff` reviewed hunk-by-hunk
- [ ] §4 exceptions verified untouched (grep the final diff for `TIG_`, `Plugins.TIG.`, `TIG.CORE.Admin.`, `SystemName`)
- [ ] Step D: build green, tests green, smoke test in scratch environment
- [ ] `Presentation/Nop.Web/Themes/TIG.Standard` → `CW.Standard` (58 files) confirmed renamed and theme still selectable/renders
- [ ] Flagged stale `Nop.Plugin.TIG.Theme.Standard` `.sln` entry logged as a separate follow-up, not touched here
- [ ] `Author`/`Description` text in each `plugin.json` updated once the actual new company name is confirmed
- [ ] No file anywhere under `Plugins/Nop.Plugin.CW.*`, `CWLibraries/`, `CWPlans/`, or the renamed theme still contains the substring `TIG` or `Tig` **except** the explicitly-deferred items in §4
- [ ] Human review/approval obtained before merge (per §0.2 — Claude does not mark this done unilaterally)
