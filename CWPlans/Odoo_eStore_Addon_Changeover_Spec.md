# Integration.Odoo: changeover to the `cityweb_estore` addon

2026-11-23. **Draft: decisions in §4 are open.** Source: the client's changeover document
(`Plugins/Nop.Plugin.Integration.Odoo/NopCommerce_eStore_Odoo_Changeover.md`), checked against the plugin code
(version `2026_11_18_DATA_1.4`).

The client replaced the Odoo Studio eStore app (`x_store`, `x_department`, `x_studio_*` fields) with a real addon
(`estore.store`, `estore.department`, `estore_*` fields). The plugin is to work against the new addon only.

## 1. Answers to the client's open items (their §10)

| # | Their question | What the plugin does today |
|---|---|---|
| 1 | Transport | JSON-2 only (`POST /json/2/<model>/<method>`, bearer key, `X-Odoo-Database`), plus the unauthenticated `/web/version`. No change of transport is needed. |
| 2 | Revision counters | No high-water number. Every run fetches **all** stores, departments and active saleable products; a store is skipped when its `x_studio_sync_revision` **and** `write_date` match those kept on `Odoo_StoreMapping`, a product when they match its `Odoo.ProductSyncMarker` attribute (`revision\|write_date`). Departments are upserted every run. |
| 3 | Contract Version | Not used. The unused request for it was removed on 2026-10-28. |
| 4 | Deleted records | Archived product templates are fetched separately (`active = false`) and unpublished in nop. Archived departments are deactivated, archived delivery addresses deactivate their branch, an archived store is only logged. Hard deletes are not detected. |
| 5 | Price and stock | Product price = template `list_price`; old price = `compare_list_price`; **product cost = `standard_price` (stored in nop)**; a combination's overridden price = variant `lst_price`; an attribute value's `price_extra` = nop price adjustment. No pricelist. Stock = variant `qty_available` (on hand, every warehouse), set on each combination. |
| 6 | Customers | Yes. The branch sync reads each store's companies (`x_store.x_studio_companies` → `res.partner`) as head-office branches and their `delivery` addresses as branches; `CardCode` = Odoo partner id. The order send reads and **creates** `res.partner` delivery contacts and **creates and confirms** `sale.order`. It reads no `x_studio_estore` field. |

## 2. Where the old names are

| File | What changes |
|---|---|
| `Services/OdooApiService.cs` | `FetchStoresAsync` (model, fields, domain), `FetchDepartmentsAsync` (same), `FetchProductsAsync` (fields `x_studio_stores`, `x_studio_departments`, `x_studio_sync_revision`; website fields; `standard_price`), `GetProductVariantsAsync` (`qty_available`), `FetchArchivedProductsAsync` |
| `Models/Responses/OdooStoreResponse.cs` | `x_name`, `x_active`, `x_studio_sequence`, `x_studio_sync_revision`, `x_studio_store_url`, `x_studio_departments`, `x_studio_companies` |
| `Models/Responses/OdooDepartmentResponse.cs` | `x_name`, `x_active`, `x_studio_sequence`, `x_studio_store`, `x_studio_sync_revision` |
| `Models/Responses/OdooProductResponse.cs` | `x_studio_stores`, `x_studio_departments`, `x_studio_sync_revision` |
| `Services/StoreSyncService.cs`, `DepartmentSyncService.cs`, `ProductSyncService.cs`, `BranchSyncService.cs` | the `X*` property names, comments and log text |
| `Domain/OdooStoreMapping.cs`, `I*SyncService.cs`, `IOdooApiService.cs` | comments |
| `References/Odoo_Plugin_Reference.md` | §1, D5, D10, D11, §4 "Client contract" |

The renames themselves are mechanical (their §3). The rest of this document is what is not.

## 3. Changes beyond the renames

| # | Change | Their § | Notes |
|---|---|---|---|
| C1 | **One high-water revision.** A new setting `OdooSettings.LastSyncRevision`; each run reads stores, departments and products with `estore_sync_revision > N`, `active_test: false`, paged by `order: "estore_sync_revision asc"`, and saves the highest revision after each page. The per-record markers (D5) go. | 6, 9 | Changes D5. The run order stays Stores → Departments → Products → Branches; N is saved only for what was applied, so a failed record must hold N back (see Q8) |
| C2 | **Archived = `active: false` in the same pull.** The separate archived-product fetch goes. Archived product → unpublished; department → deactivated; store → logged (D11 unchanged). | 6 | |
| C3 | **Products on no store.** Only products on a store are in the feed. A product whose `estore_ids` becomes empty (untagged from its last store) is unpublished, instead of D10's "no store = visible in every store". | 5.3, 6, 7 | Changes D10 |
| C4 | **No department on a product = the whole store sees it**; departments restrict it. Already how `SaveAclDepartmentAsync` behaves; to be checked, not changed. | 7 | |
| C5 | **Fields that may not exist.** The fetch asks for `is_published`, `website_published`, `description_ecommerce`, `website_description`, `compare_list_price` (Website / eCommerce). Their field list has none of them, and Odoo refuses the whole `search_read` on one unknown field. Publishing would then follow C3 (on a store = published). | 5.3, 7 | Q5 |
| C6 | **Stop storing cost.** `standard_price` is copied to `Product.ProductCost` today; their §5.6 says read cost only to compute prices, never store or log it. | 5.6 | Q3 |
| C7 | **Price from the store's pricelist** (`estore.store.pricelist_id`); `list_price` only when the store has none. Read every run (rule changes stamp no revision). | 5.6, 7 | Q3: how |
| C8 | **Stock from the store's warehouses** (`free_qty` with `context.warehouse_id = store.warehouse_ids`), read every run. | 5.5, 7 | Q4: nop keeps one stock figure per product / combination |
| C9 | **Branch sync source.** `x_studio_companies` → `partner_link_ids` (customers linked directly; head offices) rather than `partner_ids` (every linked customer, people under a company too). | 3, 5.7 | Q6 |
| C10 | **Hard-delete check.** An occasional full id comparison (`search`, domain `[]`, `active_test: false`) per model; a store, department or product no longer in Odoo is handled as archived. | 6 | A button on the Sync Log page, or part of a full run (Q8) |
| C11 | **New ids: rebuild the mapping** (their §8). Reset N to 0; then stores matched by name or URL, departments by (store, name) (CW.Core's key already), products by the `ODOO_` link first, then `default_code` (the SKU); branches by `CardCode` = old partner id, which will not match. | 8 | Q7 |
| C12 | **Errors.** 401/403 and `AccessError` messages name the user's group and the model. | 9 | Small |
| C13 | **Not used:** `estore_style_code`, store `logo` / `sequence` / `pricelist_id` / `warehouse_ids` beyond C7–C8, `helpdesk.ticket.estore_id`, `product.image` gallery. | 3, 7 | Q9 |

## 4. Decisions needed

| # | Question | Why it matters | Suggested answer |
|---|---|---|---|
| **Q1** | **The integration user is read-only** (their §2: a write returns `AccessError`). The order send creates `res.partner` delivery contacts and creates, confirms and cancels `sale.order`. Will the new Odoo let the plugin write those, through a second user or group? | Blocks the whole order send (`CWPlans/Odoo_Order_Sync_Spec.md`), which their document does not mention ("nothing comes back from NopCommerce"). | Ask the client for a group with create / write on `sale.order`, `sale.order.line` and `res.partner` (delivery contacts), used for the order send only. |
| Q2 | Do the order send's Odoo prerequisites still exist on the new database: the delivery product (`Delivery_007`), the exchange fee product, company ids as `CardCode`? | The order send reads them by code and id. | Recreate them on the new database; the codes go on Configure again. |
| Q3 | **Pricelist prices: compute in nop, or take the client's offer** of a read-only "price for these products on this store" method? | Computing means reimplementing Odoo's rule engine (variant / product / category / global rules, min quantity, dates, fixed / discount / markup, other-pricelist bases recursively, cost bases, rounding, margins) and reading cost. Any drift from Odoo is a wrong price. | **Take the method.** Store the per-store price in nop as a store-scoped tier price (quantity 1, no role), `Product.Price` = `list_price`. |
| Q4 | **Stock per store.** nop keeps one stock quantity per product and combination. Do stores that sell the same product use different warehouses? | If they do, one nop figure cannot show each store its own stock; nop's multiple-warehouse stock is summed, not chosen per store. | Find out first. If every store shares the warehouses, take `free_qty` over the union. If not, decide between per-store products and stock shown without enforcement. |
| Q5 | Does the new database have Website / eCommerce installed (`is_published`, `website_description`, `description_ecommerce`, `compare_list_price`)? | If not, the product fetch fails outright. | Ask; drop the fields if not, and publish by C3. |
| Q6 | Which customer field feeds the branches: `partner_link_ids` (directly linked) or `partner_ids` (all)? Their §1 says customers are not in the feed, yet §5.7 says `res.partner` is readable. | Branch sync and the order's company id. | `partner_link_ids` for head offices; delivery addresses read as today. Confirm `res.partner` stays readable. |
| Q7 | **Re-link or recreate** nop's records against the new ids? | Their §8 asks us to decide with them. 5.00 is pre-production with test data only. | Recreate: clear the local test data from the old Odoo (stores and departments from Odoo, Odoo-linked products, branches with Odoo card codes, `Odoo_*` mappings and order mappings), reset N, full pull. Matching (C11) is for a database that has real data. |
| Q8 | How do failed records and the full run work with one high-water number? | A record that fails (unmapped store, unreadable image) must be retried, but N moves past it. | Keep N at the lowest failed revision of the run, or keep a short retry list. Add a "full sync" button that reads from 0, also used for the hard-delete check (C10). |
| Q9 | Any use for `estore_style_code`, the store logo, or the product gallery? | Their document offers them; the plugin has nowhere to put them yet. | Not in this change. |
| Q10 | Department roles "should not appear in the normal ACL list" (their §7). | That is CW.Core's role type, not this plugin's. | Leave to CW.Core. |

## 5. Test plan

Against the new Odoo test database, once Q1–Q8 are answered. Each case is checked in nop and in the Sync Log.

| # | Case | Expected |
|---|---|---|
| T1 | Test Connection with the new integration user | Connected; database, user and product count shown |
| T2 | First full run (N = 0) | Stores, departments and products created; counts per store equal Odoo's; N = highest revision received |
| T3 | Second run, nothing changed | Nothing fetched apart from price and stock |
| T4 | Rename a store, a department, a product | Only those three come back; each updated in nop |
| T5 | Same department name on two stores | Two nop departments, each in its own store |
| T6 | Archive a store, a department, a product | Store logged, department inactive, product unpublished |
| T7 | Untag a product from one of two stores | Mapped to the other store only |
| T8 | Untag a product from its last store | Unpublished |
| T9 | Product with no departments; then with one | Whole store sees it; then only that department's role |
| T10 | Pricelist rule changed (no revision) | New store price after the next run |
| T11 | Stock changed (no revision) | New quantity after the next run |
| T12 | A product whose store is not mapped yet | Logged, retried next run (Q8) |
| T13 | Hard-delete a department in Odoo (an admin user), run the full check | Handled as archived |
| T14 | Wrong key; user outside the group | Clear 401/403 or AccessError message naming the model |
| T15 | Branch sync | Head offices and branches from the new partner field (Q6) |
| T16 | Order send, if Q1 allows it | As `Odoo_Order_Sync_Spec.md` §8 |
| T17 | Unit tests | Response classes read the new field names; the high-water logic; the existing 41 cases |

## 6. Housekeeping

The client's document is in the plugin folder; move it to `Plugins/Nop.Plugin.Integration.Odoo/References/` as the
contract it is. It holds no secrets.

## 7. Access check on the new instance (2026-11-25)

*Odoo › Settings › Run Diagnostics* now lists what the API user may do and which fields exist (read-only, plugin
`2026_11_25_DATA_1.5`). On the new staging instance (Odoo 20.0+e, 54 products), every line is OK but one:

| Decision | Result |
|---|---|
| Q1 | `sale.order`, `sale.order.line`, `res.partner`: read, create and write allowed. Model rights only; a real create, confirm and cancel is still to be tried. The key's user is not the read-only one of their document: production must keep these rights |
| Q2 | `Delivery_007` exists. Exchange fee product code not set. Company ids are new |
| Q3 | `product.pricelist.item` readable; how prices are computed is still open |
| Q5 | Every Website / eCommerce field the plugin requests exists on this instance |
| Q6 | `partner_ids` and `partner_link_ids` exist and `res.partner` is readable; which one to use is still open |
| Renames | `estore.store`, `estore.department` and the product `estore_*` fields exist and are readable |

Until the old links are cleared (Q7), no sync, order send or cancel may run against this instance: nop still holds ids of
the old database.

## 8. First sync from the new addon (2026-11-25, plugin `2026_11_25_DATA_1.6`)

Q7 done: the old Odoo data was deleted from the local database. The renames (§2), C3, C6 and C9 (`partner_link_ids`) are
built; products are fetched by `estore_sync_revision > 0` in a fixed order. C1, C2 (one revision), C7, C8 and C10 are not.

| Sync | First run | Second run, no change |
|---|---|---|
| Stores | 2 created | 2 skipped |
| Departments | 6 created (same three names on both stores, kept apart) | 6 updated (upserted every run) |
| Products | 6 created, 37 variants | 6 skipped |
| Branches | 7 created (2 head offices, 5 delivery addresses) | 7 updated (upserted every run) |

No errors or warnings. Not yet checked: the figures against Odoo's own screens, change runs, archive / untag, order send.
Found: all six products are unpublished, because the Website flags are off in Odoo (C5); cost is 0 on all.

**C5 decided 2026-11-25:** a product is published while it is on at least one active Odoo store, unpublished on none; the
Website flags are no longer read for it (plugin `2026_11_25_DATA_1.7`). A specification is also replaced, not added again,
when its product is updated.
An unchanged product that is unpublished in nop is published again by the next sync.

**Print Name as a text box:** left to the other developer (2026-11-25); it is in the open decisions of
`CWPlans/Handover/Odoo_Order_Sync_Handover.md`. Until then it stays a read-only specification.

## 9. Live tests on the new Odoo (2026-11-25)

Each record was changed in Odoo, synced, put back and synced again. All pass, with no warning in the Sync Log.

| Case | Result |
|---|---|
| Price change (T4) | Product and its variants updated, the other five skipped |
| Untag from its only store, then re-tag (T8) | Unpublished (revision stamped, so it stays in the feed); then published, store and department back |
| Archive / restore a product (T6) | Unpublished; published |
| Archive / restore a department (T6) | Only that store's department inactive, the same name on the other store untouched; active |
| Archive / restore a store (T6) | Store logged and left; its three products unpublished; published |
| Send an order (T16) | ESTORE-01 → S00126: company, matched delivery address, variant, nop price, delivery line, note; untaxed 47.30 as in nop |
| Cancel in Odoo | Confirmation shown; S00126 reads Cancelled in Odoo and nop |

**Found:** Odoo put 0% tax on S00126 (products carry 15%) and nop 10%, so the totals with tax differ; the totals check compares
before tax and does not see it. The order is in USD and Acme's pricelist is "Trade (GP 40%) (USD)", so it is cost-based (C7).
Not tested: untagging from one of two stores (T7), packname orders, stock and price cases, hard deletes.
