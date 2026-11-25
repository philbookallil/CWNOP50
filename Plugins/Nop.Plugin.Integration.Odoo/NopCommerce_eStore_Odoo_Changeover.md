# NopCommerce changeover: Odoo Studio "eStore" app to the new `cityweb_estore` addon

Audience: the NopCommerce developer (and any AI assistant they feed this to).
Odoo version: 20 (Odoo.sh). The new code is a normal addon, not Odoo Studio.

## 0. How to use this document with an AI assistant

Give the assistant this whole file plus the NopCommerce plugin source. Ask it to:

1. Search the plugin for every old name in section 3 (`x_store`, `x_department`, any `x_studio_*` field, `contract_version`).
2. Replace each with the new model/field, using the mapping in section 3.
3. Remove everything to do with Contract Version (section 4).
4. Change the connection and authentication as in section 2.
5. Adjust the sync loop as in section 6 and the business rules in section 7.
6. Work through the checklist in section 10 and report anything it could not confirm from the plugin code.

Facts marked **(verified)** were checked against a running Odoo 20 test database. Anything marked **(confirm)** could not be seen from here, because only the Studio export was available and not the plugin code. Do not guess those: check the plugin.

## 1. What changed in one paragraph

The Studio app stored its data in Studio-generated models (`x_store`, `x_department`) with `x_studio_*` fields on products, customers and tickets. The new addon replaces them with real models (`estore.store`, `estore.department`) and plain fields (`estore_*`). NopCommerce still pulls from Odoo. Nothing is pushed to NopCommerce, nothing comes back from NopCommerce, and customers are not part of the feed. The Odoo ID is the only link key: there is no external code field on stores, departments or products. Contract Version is gone. There is no data migration: the new Odoo starts clean, so **IDs will be different from the Studio database** (section 8).

## 2. Connection and authentication

Use the standard Odoo 20 external API (JSON-2). Nothing custom was added.

* Base URL: `https://<odoo-host>/json/2/<model>/<method>`, HTTP `POST`, JSON body.
* Headers: `Authorization: bearer <API key>`, `X-Odoo-Database: <database>`, `Content-Type: application/json`.
* One API key per NopCommerce instance (not per store). It belongs to a dedicated Odoo user.
* That user must be an internal user in the group **eStore / Integration (read only)** (or eStore / Manager). Create an API key for it with scope `rpc`. **(verified)**
* Responses are plain JSON (a list or object), with no `jsonrpc` envelope. Errors come back with an HTTP error code and a JSON body with `name` and `message`.
* The integration user is read only. A write attempt returns an `AccessError` ("You are not allowed to modify 'eStore' (estore.store) records"). **(verified)**

If the plugin currently uses the older `/jsonrpc`, `/xmlrpc/2/object` or `/web/dataset/call_kw` endpoints **(confirm which)**, move it to `/json/2/` as above. The old endpoints are legacy; `/json/2/` is what was tested. If the plugin already uses `/json/2/` against the Studio database, only the model names, field names and the key/user change.

Request parameters for the methods used here:

| Method | Body |
|---|---|
| `search_read` | `{"domain": [...], "fields": [...], "limit": n, "offset": n, "order": "...", "context": {...}}` |
| `read` | `{"ids": [...], "fields": [...], "context": {...}}` |
| `search` | `{"domain": [...]}` returns ids |

## 3. Name mapping: old (Studio) to new

| What | Old (Studio) | New |
|---|---|---|
| Store model | `x_store` | `estore.store` |
| Store name | `x_name` | `name` |
| Store active | `x_active` | `active` |
| Store sequence | `x_studio_sequence` | `sequence` |
| Store URL | `x_studio_store_url` | `url` |
| Store logo | `x_studio_store_logo` | `logo` (image, max 512 px) |
| Store sync revision | `x_studio_sync_revision` | `estore_sync_revision` |
| Store contract version | `x_studio_contract_version` | **removed** |
| Store departments | `x_studio_departments` | `department_ids` |
| Store customers | `x_studio_companies` (res.partner) | `partner_ids` (all linked customers); `partner_link_ids` (customers linked directly) |
| Store pricelist | (none) | `pricelist_id` (new) |
| Store stock warehouses | (none) | `warehouse_ids` (new) |
| Department model | `x_department` | `estore.department` |
| Department name | `x_name` | `name` |
| Department active | `x_active` | `active` |
| Department sequence | `x_studio_sequence` | `sequence` |
| Department store | `x_studio_store` | `store_id` (required) |
| Department sync revision | `x_studio_sync_revision` | `estore_sync_revision` |
| Product stores | `product.template.x_studio_stores` | `product.template.estore_ids` |
| Product departments | `product.template.x_studio_departments` | `product.template.estore_department_ids` |
| Product sync revision | `product.template.x_studio_sync_revision` | `product.template.estore_sync_revision` |
| Product contract version | `product.template.x_studio_contract_version` | **removed** |
| Product style code | `product.template.x_studio_style_code` | `product.template.estore_style_code` |
| Customer store | `res.partner.x_studio_estore` | `res.partner.estore_id` |
| Customer inherited store | `res.partner.x_studio_inherited_estore` | `res.partner.estore_inherited_id` |
| Helpdesk ticket store | `helpdesk.ticket.x_studio_related_x_studio_estore_1` | `helpdesk.ticket.estore_id` (only if the `cityweb_estore_helpdesk` addon is installed) |

## 4. Things that no longer exist

* **Contract Version** (`6.0.0` on stores and products). Do not send, read, compare or branch on it. If the plugin validates a contract version, remove that check.
* **External Code.** The Odoo ID is the key for stores, departments and products. Store it in NopCommerce as the mapping key.
* Customers are not in the feed. (Only the store and department structure and the products are.)

## 5. Endpoints to call

All examples are `POST` with the headers from section 2. The result shapes below come from a real test database. **(verified)**

### 5.1 Stores

```
POST /json/2/estore.store/search_read
{"domain": [["estore_sync_revision", ">", N]],
 "fields": ["name","active","sequence","url","logo","pricelist_id","warehouse_ids","department_ids","partner_ids","estore_sync_revision"],
 "context": {"active_test": false}}
```
```
[{"id": 4, "name": "Pizza Hut", "active": true, "url": false, "pricelist_id": false,
  "warehouse_ids": [1], "department_ids": [5], "partner_ids": [49], "estore_sync_revision": 25}]
```

### 5.2 Departments (NopCommerce customer roles, specific to one store)

```
POST /json/2/estore.department/search_read
{"domain": [["estore_sync_revision", ">", N]],
 "fields": ["name","sequence","store_id","active","estore_sync_revision"],
 "context": {"active_test": false}}
```
```
[{"id": 5, "name": "Kitchen", "store_id": [4, "Pizza Hut"], "active": true, "estore_sync_revision": 24}]
```
The same department name can exist on different stores. Treat a department as `(store_id, id)`, not as a global name.

### 5.3 Products

```
POST /json/2/product.template/search_read
{"domain": [["estore_sync_revision", ">", N]],
 "fields": ["name","default_code","estore_style_code","estore_ids","estore_department_ids","list_price",
            "categ_id","description_sale","image_1920","weight","volume","barcode","active","sale_ok",
            "attribute_line_ids","product_variant_ids","estore_sync_revision"],
 "context": {"active_test": false}}
```
```
[{"id": 3, "name": "Uniform shirt", "default_code": false, "estore_style_code": "UF-100",
  "estore_ids": [4], "estore_department_ids": [5], "list_price": 1.0, "active": true,
  "estore_sync_revision": 27, "product_variant_ids": [3]}]
```
Only products that are on at least one store carry a revision and belong in the feed (see section 6).

### 5.4 Variants (sizes, colours, barcodes)

```
POST /json/2/product.product/search_read
{"domain": [["product_tmpl_id", "in", [<template ids>]]],
 "fields": ["product_tmpl_id","default_code","barcode","weight","volume","product_template_attribute_value_ids","image_variant_1920","active"],
 "context": {"active_test": false}}
```
Attribute names and values: read `product.template.attribute.line` (`product_tmpl_id`, `attribute_id`, `value_ids`), `product.attribute`, `product.attribute.value` and `product.template.attribute.value` (the per-product value used by a variant, with `price_extra`). The integration group can read all of these. **(verified for the reads that returned data; the test database had no variants, so check on real data.)**

### 5.5 Stock (view only, available quantity from the store's warehouses)

```
POST /json/2/product.product/read
{"ids": [<variant ids>], "fields": ["free_qty"], "context": {"warehouse_id": <store.warehouse_ids>}}
```
`warehouse_id` takes the list from the store's `warehouse_ids`. `free_qty` is on hand minus reserved. Do the same on `product.template` for a template total. **(verified that the call works; the test database had no stock, so the quantity returned was 0.)** Read stock fresh each cycle: stock changes do not change the revision.

### 5.6 Price (from the store's pricelist)

The store's price is whatever its `pricelist_id` gives for the product. Odoo 20 has no public price method on the external API, so the plugin must read the rules and apply them itself:

```
POST /json/2/product.pricelist.item/search_read
{"domain": [["pricelist_id", "=", <store.pricelist_id>]],
 "fields": ["applied_on","product_tmpl_id","product_id","categ_id","min_quantity","date_start","date_end",
            "compute_price","base","base_pricelist_id","fixed_price","price_discount","price_markup",
            "price_surcharge","price_round","price_min_margin","price_max_margin","uom_id"]}
```
Rule facts for Odoo 20:

* `applied_on`: `0_product_variant`, `1_product`, `2_product_category`, `3_global`. Odoo's order is `applied_on, min_quantity desc, categ_id desc, id desc`, with the first matching rule winning, so the most specific rule applies. Check `min_quantity`, `date_start` and `date_end`.
* `compute_price`: `fixed` (use `fixed_price`), `discount` or `markup` (Surcharge).
* `base`: `list_price`, `standard_price` (cost) or `pricelist` (another pricelist, `base_pricelist_id`; resolve it recursively).
* `price_discount` is in whole percentage points. `price_markup` is simply `-price_discount`. So a 42.86 % surcharge is stored as `price_discount = -42.86`.
* Formula for the percentage types: `price = base * (100 - price_discount) / 100`, rounded to `price_round` if set, plus `price_surcharge`, then limited by `price_min_margin` / `price_max_margin`. **(confirm against `product/models/product_pricelist_item.py` in Odoo 20, method `_compute_price`.)**
* Variant extra prices (`price_extra` on the attribute value) add to fixed-price and list-price calculations.
* Cost (`standard_price` on `product.product`) is readable by the integration user **(verified)**, so rules with `base = standard_price` can be computed. These are the rules the new `cityweb_pricing_gp` addon creates from a gross profit %. Cost is sensitive: read it only to compute prices and never store or log it.

If computing prices in NopCommerce is not workable, tell us: a small read-only "price for these products on this store" method can be added to the addon.

### 5.7 Customers and which store they belong to (only if the plugin needs it)

```
POST /json/2/res.partner/search_read
{"domain": [["estore_inherited_id", "=", <store id>]], "fields": ["name","email","parent_id","estore_id","estore_inherited_id"]}
```
`estore_id` is set on the top-level customer. People under a company follow their company: `estore_inherited_id` gives the store for any contact. Customers are not part of the product feed, so you may not need this. **(verified)**

## 6. Sync loop and revision

One shared counter (an Odoo sequence) numbers every change across stores, departments and products. A higher number is a newer change.

Plugin logic:

1. Keep one number: the highest `estore_sync_revision` seen so far. If the plugin kept a separate number per model or a "contract version" **(confirm)**, collapse it to this one.
2. Each cycle ask each of the three models for `estore_sync_revision > N` with `active_test: false`.
3. Apply the results (create, update, or unpublish if `active` is false).
4. Set N to the highest revision received across all three.

What stamps a new revision:

* A store, department or product changes one of its synced fields.
  * Store: name, active, url, logo, pricelist, warehouses.
  * Department: name, active, store.
  * Product template: name, reference, description, list price, category, active, sale flag, image, weight, volume, barcode, attributes, stores, departments, style code.
* A variant change (barcode, reference, images, attributes) stamps its template.
* **Tagging or untagging a product to a store stamps it.** An untagged product therefore appears in the next pull with its new `estore_ids` (without the store) so it can be removed from that store.
* Only products on at least one store carry a revision.

What does **not** stamp: stock quantity changes, and pricelist rule changes. Read stock and price each cycle (or on your own schedule).

Archived records: `active_test: false` returns them with `active: false`. Treat that as "unpublish".

Deletes: a record deleted for good simply stops appearing and leaves no trace. A store that still has departments cannot be deleted, and the integration user cannot delete anything. Users are expected to archive rather than delete. For safety, run a full id comparison now and then (`search` with the `domain` `[]` and `active_test: false`) to catch hard deletes.

## 7. Business rules NopCommerce must follow

* **Product visibility.** A product on a store with no departments is visible to everyone in that store. If it has departments, only customers holding a matching department role see it.
* **Departments belong to stores.** A department must be one of the product's own stores (Odoo enforces it). The same department name can exist on several stores, so key roles by `(store, department id)`.
* **Departments are NopCommerce customer roles that are special**: they are per store and should not appear in the normal ACL list. Create them as the special store-scoped role type you already planned. Odoo only supplies the structure.
* **Multiple companies per store.** Several Odoo companies (for example separate franchises) can belong to the same store.
* **Pricelist.** Price comes from the store's pricelist, not `list_price`. Use `list_price` only if the store has no pricelist.
* **Stock.** The available quantity of the store's warehouses only.
* **Images.** The main product image is `image_1920` (base64) on the template and `image_variant_1920` on a variant. Extra gallery images live in the model `product.image`, which exists only when the website sales module is installed, and the integration group currently has **no access** to it. Tell us if you need the gallery and we will add access.
* **Style code** is `estore_style_code` on the template and is not unique.

## 8. One-time changeover

1. We create the integration user and API key on the new Odoo. Never put keys in documents or source control.
2. **IDs are new.** The new database starts clean, so store, department and product IDs differ from the Studio database. NopCommerce must rebuild its mapping (Odoo ID to NopCommerce record). Suggested approach: match stores by name or URL, departments by (store, name), products by `estore_style_code` or `default_code`, then store the new Odoo ID against each record. Decide with us whether existing NopCommerce records are re-linked or recreated.
3. Reset the stored high-water revision to 0 and run one full pull.
4. Confirm counts match the NopCommerce side for stores, departments and products per store.
5. Switch the schedule to the new endpoints and retire the old connection.

## 9. Error handling

* HTTP 401/403: bad key, wrong database header, or the user is not in the Integration group.
* AccessError on `/json/2/...`: the user lacks access to that model. Report the model named in the message.
* Large pulls: page with `limit` and `offset` and `order: "estore_sync_revision asc"`, and save the highest revision after each page so a stopped run can resume.
* Images are large base64 strings. Fetch them in smaller batches.

## 10. Checklist for the developer and AI

- [ ] Searched the plugin for `x_store`, `x_department`, `x_studio_`, `contract_version` and replaced or removed each.
- [ ] Connection moved to `/json/2/` with bearer key and `X-Odoo-Database` header (section 2).
- [ ] Stores, departments, products read with `estore_sync_revision > N` and `active_test: false`.
- [ ] One revision counter, highest seen across all three models.
- [ ] Tagging or untagging handled (a product losing a store is removed from it).
- [ ] Departments keyed by (store, id); same name on two stores stays separate.
- [ ] No department on a product means visible to the whole store.
- [ ] Price from the store's pricelist; stock from the store's warehouses; both read each cycle.
- [ ] Variants, attributes, barcode, weight, dimensions and images mapped.
- [ ] Contract Version and External Code logic deleted.
- [ ] Id mapping rebuilt against the new Odoo (section 8).
- [ ] Open items below confirmed and reported back.

### Open items the plugin code must answer (confirm)

1. Which transport does the plugin use today (`/jsonrpc`, XML-RPC, `/json/2/`)?
2. Did it use one revision counter or one per model?
3. Where does it use Contract Version, and what does it do with it?
4. How does it detect deleted records today?
5. Does it read price from `list_price`, a pricelist, or something else, and how does it get stock?
6. Does it read customers, and if so which store field?
