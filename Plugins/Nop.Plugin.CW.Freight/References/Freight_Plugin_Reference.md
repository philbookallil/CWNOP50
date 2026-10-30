# CW.Freight — plugin reference

Shipping rate computation plugin (`IShippingRateComputationMethod`), SystemName `CW.Freight`, table prefix `CW_FR_`.
Ported from 4.90 on 2026-10-26 (`CWPlans/Freight_Port_Spec.md`, version `2026_10_26_DATA_1.2`).

## 1. What it does

An administrator keeps a list of shipping methods (name, description, display order, flat rate). At checkout the plugin
offers every method of the current store that is not restricted in the shipping address's country, at its stored rate,
in display order.

| Table | Entity | Meaning |
|---|---|---|
| `CW_FR_ShippingMethod` | `CWShippingMethod` | The method and its rate |
| `CW_FR_ShippingMethodStore` | `CWShippingMethodStoreMapping` | Stores offering it; **none = every store** (a `StoreId 0` row also means every store) |
| `CW_FR_ShippingMethodCountry` | `CWShippingMethodCountryMapping` | Countries where it is **not** offered; none = no restriction |
| `CW_FR_ShippingMethodCustomerRole` | `CWShippingMethodCustomerRoleMapping` | Stored, not applied (§4) |

The store and country filter is `Services/FreightMethodFilter` (internal, unit-tested in
`Tests/CW.Tests/CW.Freight`).

## 2. Who depends on it

**CW.Core** (`IPackNameShippingMethodService`) loads this plugin by SystemName through `IShippingPluginManager` — no
assembly reference either way — and calls `GetShippingOptionsAsync` with a store id, **one empty item, no address and no
customer** to list the names of a store's methods. That request must keep answering every method of the store: a
filter that reads the subtotal, the customer or the address would empty the store and branch shipping pickers and
the packname checkout. The packname checkout offers only this plugin's options (4.90 `PackName_Order_Shipments.md`).
The names are matched exactly, so renaming a method breaks every store and branch setting that lists it.

## 3. Admin

*Configuration → Shipping → Shipping providers → CW Freight → Configure* (`Admin/CWFreight/Configure`), permission
`Configuration.ManageShippingSettings`: the method grid (filter by store), Add new / Edit / Delete, and the
`LimitMethodsToCreated` setting. Strings: `Infrastructure/FreightLocaleResources.cs`, installed on install and by an
update whose version contains `DATA`.

## 4. Open items

Stored and shown on the admin forms but read by nothing at checkout (unchanged from 4.9):

- `CWFreightSettings.LimitMethodsToCreated`
- the customer-role mappings
- `LowestOrderTotal` / `HighestOrderTotal` — applying them must leave CW.Core's empty-item request (§2) untouched
- `ApplyPerBranch`
- the validator's messages are fixed English

## 5. What the 5.00 port changed

`net10.0`, `SupportedVersions 5.00`; `IWebHelper` from `Nop.Services.Helpers`; one installation migration with
`CreateTableIfNotExists` (the 4.9 rename and add-column migrations dropped — no 5.00 database has the old tables);
`UpdateAsync` installs the strings on a `DATA` version; empty `RouteProvider` removed. Admin fixes: no "All" (value 0)
entry in the store and restricted-country lists, which saved a row that hid the method everywhere; stores, countries
and roles are optional; a refused save redisplays what was posted; the missing `Admin.Common.FormErrors` notification
removed; Bootstrap 4.6 float classes; the duplicate grid-reload script removed; Edit's Delete confirmation fixed
(`<nop-delete-confirmation asp-action>` takes an action name — a URL there breaks the modal id); `ViewBag.PageTitle`
for the page titles. `ShippingOption.DisplayOrder` is now
set. Detail: `CWPlans/Freight_Port_Spec.md` §3.
