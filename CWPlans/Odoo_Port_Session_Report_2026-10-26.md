# Integration.Odoo port — session report, 2026-10-26

Repository: `C:\Work\CW_NOP_5_0`. Source: the 4.90 source's `Plugins/Nop.Plugin.Integration.Odoo`.
**Status: ported, built and unit-tested; install and page tests wait for the CW.Core port.** Nothing is committed.

## 1. Time spent

Times are from the session transcript, local time (EDT). "Active" means Claude was working; "waiting" means the
session was idle until your next message.

| From | To | Duration | What |
|---|---|---|---|
| 04:33 | 04:35 | 2 min | Read the request; compared the 4.9 plugin with 5.00; asked three decisions (code edits, port scope, CW.Core coupling) |
| 04:35 | 04:51 | 16 min | Spec, port, CW standards pass, build, unit tests, reference doc, `CLAUDE.md` updates; first report |
| 04:51 | 04:55 | 4 min | *Waiting* — sign-in to the local site |
| 04:55 | 04:56 | 1 min | Checked sign-in (not yet signed in) |
| 04:56 | 04:59 | 3 min | Browser test: install, site failed to start, recovery, dependency fix, verified the fix, recorded it |
| 04:59 | 05:36 | 37 min | *Waiting* — decision on installing CW.Core |
| 05:36 | 05:40 | 4 min | This report; test results and blocker recorded in the spec |

**Active work ≈ 26 minutes** of about 67 minutes elapsed; the rest was waiting on you.

## 2. Tasks

| # | Task | Status |
|---|---|---|
| 1 | Compare the 4.9 plugin with 5.00 and ask the decisions | Done — edits allowed; port + CW standards; CW.Core coupling kept (Pattern C) |
| 2 | Write the port spec | Done — `CWPlans/Odoo_Port_Spec.md` |
| 3 | Port the plugin (`CLAUDE.md` §10.5) | Done — `net10.0` / 5.00, `IWebHelper`, one install migration, uniform sync JSON, solution entry with a CW.Core build dependency |
| 4 | CW standards pass | Done — `[CheckPermission]`, region order, `Plugins.Integration.Odoo.*` locale keys (all installed), scripts out of the views, dead code removed |
| 5 | Unit tests | Done — 14 cases in `Tests/CW.Tests/Integration.Odoo/`; all 472 CW cases pass |
| 6 | Documentation | Done — plugin reference doc; `CLAUDE.md` §2, §3.2, §10.5 (row 11), §11; lesson #21 |
| 7 | Browser tests (spec T3–T8) | **Blocked** — the plugin depends on CW.Core, which is not installed on the local site and is not yet migrated |
| 8 | Test against a real Odoo 19 (T9) | Open — no instance |

## 3. Defects found and fixed

| Defect | Fix |
|---|---|
| Installing the plugin without CW.Core stopped **the whole site** from starting | `plugin.json` declares `DependsOnSystemNames: ["CW.Core"]`; nop now refuses the install with its dependency message (checked in the browser). The queued install was removed from `App_Data/plugins.json` and the site starts again |
| A raw product dump from the client's Odoo (with its tenant URL) sat in the 4.9 plugin | Not brought into this public repository |
| Trace grid rendered log messages unencoded (they carry Odoo product / partner names) | Encoded |
| Test Connection showed the API user's language as "Logged in as" | Shows the user id |
| *Last Sync* showed "now" before any sync, and in UTC | Hidden until a sync; admin's time zone |
| *Enable Odoo Integration* checkbox was never saved and never read | Removed |
| Diagnostics card hidden by an id `nop-card` overwrites | Uses the card's real id |
| Dead code: deprecated API client and its models, unused partial view, never-loaded JS / CSS, unused response record | Deleted |

## 4. Open

| # | Item |
|---|---|
| 1 | Install and page tests (spec T3–T8) once CW.Core is ported and installed |
| 2 | Test against a real Odoo 19 (T9) |
| 3 | `OdooSettings.ApiKey` is stored in plain text (as in 4.9) |
| 4 | The build copies CW.Core's `Content/`, `logo.png` and a `refs/` DLL into the Odoo plugin folder (as in 4.9); the plugin list shows CW.Core's logo |
| 5 | CW.Core does not enforce `Department.Active` (4.9 open item O6) |

## 5. Files

**Changed by this session** — new: `Plugins/Nop.Plugin.Integration.Odoo/`, `Tests/CW.Tests/Integration.Odoo/`,
`CWPlans/Odoo_Port_Spec.md`, this report. Modified: `NopCommerce.sln`, `Tests/CW.Tests/CW.Tests.csproj`,
`CLAUDE.md`, `tasks/lessons.md`. Local only (not tracked): `Presentation/Nop.Web/App_Data/plugins.json` (the queued
install removed again).

**Not from this session:** `CWPlans/Freight_Port_Spec.md` and `Plugins/Nop.Plugin.CW.Freight/` appeared in the working
tree at 05:35–05:37 while this session was idle — another session or person is working in this checkout. Review them
separately before staging.

**Before committing** (`CLAUDE.md` §0.2, §0.9): the new and changed files above were checked for secrets — no tenant
ids, keys, connection strings or customer data; the client's Odoo dump was left out.
