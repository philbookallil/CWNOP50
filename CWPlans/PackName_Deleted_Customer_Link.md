# Packnames of deleted customers — the link, the key and the data

**Date:** 2026-11-25. **Plugin version:** `2026_11_25_DATA_0.5`. **State:** coded, unit tests pass; the test plan (§6) ran on `testnop50` on 2026-11-25: T0–T7 and T9 pass, T8 not run (§7).

## 1. The defect

nop has two customer deletes:

| Delete | What it does | Event |
|---|---|---|
| Ordinary (`CustomerService.DeleteCustomerAsync`) | Soft delete through the repository | `EntityDeletedEvent<Customer>` |
| Permanent (`GdprService.PermanentDeleteCustomerAsync`: *GDPR — permanently delete*, and nop's task *Delete inactive customers (GDPR)*) | Anonymises the customer, moves it from Registered to **Guests**, saves it as deleted | `EntityUpdatedEvent<Customer>`, then `CustomerPermanentlyDeleted` |

CW.Core cleared `PackName.CustomerId` and `Manager` on the first event only. After a permanent delete the packname kept
pointing to a guest. nop's *Delete guests* task (`CustomerService.DeleteGuestCustomersAsync`) deletes every guest in one
statement and publishes no event; the key `FK_CORE_PN_PackName_CustomerId_Customer_Id` (no action) refused that guest, so
no guest was deleted. On `testnop50`: packname 24 → customer 11, 288 errors from 2026-11-09 to 2026-11-25.

A second gap on the same path: the handler looked the packname up among packnames that are **not deleted**. A deleted
packname keeps its row and its `CustomerId`, so it would have blocked the task too.

## 2. What changed

- `Services/PackNames/PackNameCustomerDeletedEventConsumer` (new) answers both events. It clears `CustomerId` and `Manager`
  on every packname of the customer, deleted ones included. The handler in `Services/EventConsumer.cs` moved here.
- The key is now **SET NULL** on delete (`PackNameBuilder`; `PackNameCustomerForeignKeyMigration` for installed databases).
  nop's bulk delete publishes no event, so only the key can guarantee that the task never fails. Links to a guest can also
  come from an admin who moves a linked account to Guests, a guest created with a branch (provisioning), or a packname
  save that posts a guest's id.

## 3. Decisions (2026-11-25)

- **A GDPR delete leaves the packname's own data as it is**: name, print name, employee number, gender, PO entry, comments,
  addresses, and the name copied onto past orders (`PackNameOrderItem`). A packname is the employer's record of an employee
  and exists without an account. The inactive-customers task uses the same delete, and would blank an employee a manager
  still orders for. An erasure request for this data is answered by an administrator, who edits or deletes the packname.
- **The key is SET NULL**, with one known limit: a guest removed by the bulk delete leaves `Manager` as it was (nobody can
  use it: every manager screen starts from the customer). The next save of the packname clears it. Until then a subsidy
  without *Include managers* still exempts that packname from its limits. After both delete events the flag is already off.
  The key publishes no event either, so the packname caches (lists, the customer's packname, the manager sets) keep the old
  customer until they expire, at most the cache time (60 minutes by default).

## 4. Every key to `Customer`

Read from the database on 2026-11-25, with every chain of cascades from `Customer` and from `Address` (the bulk delete removes
the guests' addresses too).

| Table | Rule | Verdict |
|---|---|---|
| `CORE_PN_PackName.CustomerId` | was no action, now SET NULL | Fixed |
| `CORE_CustomerBranch.Id` | cascade | Never blocks; the row goes with the guest |
| `CORE_PN_PackNameCartItem` (through `ShoppingCartItem`) | cascade | Never blocks |
| `CORE_PN_OrderShipping.ShippingAddressId`, `.PickupAddressId` → `Address` | no action | Does not block: placement inserts its own copy of each address (`InsertCheckoutAddressAsync`); no shared row on `testnop50` |
| `CORE_BranchAddresses`, `CORE_PN_PackNameAddresses`, `CORE_PN_CostCenterAddresses` → `Address` | cascade | Never block |
| Customer ids without a key (`CORE_EX_CreditNote`, `CORE_EX_CreditNoteHistory`, `CORE_RET_*`, `CORE_APR_OrderApproval.DecidedByCustomerId`, `CORE_PN_PackNameOrderItem.PackNameCustomerId`, the subsidy `CreatorId` columns, `SSO_AuthEvent`, `NABXMLTransaction`) | none | Cannot block; they keep the id as history |

`CustomerForeignKeyTests` builds every CW.Core table and fails when a key to `Customer` refuses the delete or a new one appears.

## 5. Rows already in this state

The script below lists them, then clears the customer and the manager flag, and lists links to guests who are not
deleted. Clear the cache after running it. On `testnop50` it changes packname 24 (tried and rolled back 2026-11-25). Run it
on every database; the key alone does not do the same: it clears the link when the guest is removed, and a deleted customer
with orders never is.

```sql
/*
    One-off: packnames still linked to a deleted customer.

    Clears what CW.Core now clears when the customer is deleted, for packnames left behind before the fix: the customer
    and the manager flag. The packname's own data, its orders and their packname rows are not touched.

    Run it once per database, before or after the plugin update, then clear the cache (or restart the site): the packname
    caches hold the old link. Safe to run again.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;

-- 1. The packnames that will be changed
SELECT pn.Id AS PackNameId, pn.Name, pn.Deleted AS PackNameDeleted, pn.Manager, pn.CustomerId,
    c.Email AS CustomerEmail -- empty after a GDPR delete (and for a guest who never had one)
FROM CORE_PN_PackName pn
JOIN Customer c ON c.Id = pn.CustomerId
WHERE c.Deleted = 1
ORDER BY pn.Id;

-- 2. The change
BEGIN TRANSACTION;

UPDATE pn
SET pn.CustomerId = NULL, pn.Manager = 0
FROM CORE_PN_PackName pn
JOIN Customer c ON c.Id = pn.CustomerId
WHERE c.Deleted = 1;

PRINT CONCAT(@@ROWCOUNT, ' packname(s) unlinked');

COMMIT TRANSACTION;

-- 3. Left to look at by hand, never changed here: packnames linked to a guest who is not deleted (an account moved to the
--    Guests role). "Delete guests" removes such an account; the key then clears the customer, not the manager flag
SELECT pn.Id AS PackNameId, pn.Name, pn.Manager, pn.CustomerId, c.Email
FROM CORE_PN_PackName pn
JOIN Customer c ON c.Id = pn.CustomerId
JOIN Customer_CustomerRole_Mapping m ON m.Customer_Id = c.Id
JOIN CustomerRole r ON r.Id = m.CustomerRole_Id AND r.SystemName = 'Guests'
WHERE c.Deleted = 0;
```

## 6. Test plan

Side effects: three test customers and packnames are created and stay (the packnames without a customer); GDPR and the *Delete
guests* age are changed and put back; *Delete guests* removes every old guest without cart and orders, which it has not
done since 2026-10-31.

| # | Step | Expected |
|---|---|---|
| T0 | Run the SQL of §5; build, start the site (the plugin updates to `2026_11_25_DATA_0.5`) | One row (packname 24); the key reads `SET_NULL` in `sys.foreign_keys`; `MigrationVersionInfo` has *CW.Core packname customer key set null on delete* |
| T1 | *Schedule tasks → Delete guests → Run now* | Succeeds (`LastSuccessUtc` moves); no new *Delete guests* error in the log; customer 11 is gone |
| T2 | Create customers A, B, C with a branch, each with a packname in its list; A's packname is a manager. Delete B's packname. Enable GDPR | Three linked packnames, B's deleted |
| T3 | A: *GDPR — permanently delete*. Read the packname **before** the task runs | `CustomerId` null, `Manager` 0; name, print name, employee number, list and branch unchanged; the packname shows without a customer in the store's grid and no longer in *Department managers* pickers |
| T4 | B: *GDPR — permanently delete* | B's deleted packname has `CustomerId` null and is still deleted |
| T5 | C: change the role from Registered to Guests and save | The packname is still linked |
| T6 | *All settings*: `customersettings.deleteguesttaskolderthanminutes` = 1; wait two minutes; run *Delete guests* | Succeeds; A, B and C are gone from `Customer`; C's packname has `CustomerId` null; no error in the log |
| T7 | Create customer D with a packname and delete it the ordinary way | `CustomerId` null, `Manager` 0 (as before the change) |
| T8 | Delete the customer of a placed packname order with GDPR | The order opens in admin with its packname; `PackNameOrderItem.PackNameCustomerId` is unchanged; *Delete guests* leaves the customer (it has an order) and succeeds |
| T9 | Put both settings back (GDPR off, 1440) | — |

## 7. Test results (2026-11-25, `testnop50`)

| # | Result |
|---|---|
| T0 | Pass. Migration applied 22:59 UTC; key `SET_NULL`. The SQL of §5 had nothing left: *Delete guests* had already run at 23:09 and removed customer 11, and the key cleared packname 24 |
| T1 | Pass. 23:09 and 23:13 runs succeeded, the first since 2026-10-31; no error logged since start-up |
| T2 | Customers 2032–2035 (*GDPR Test A–D*, Test Branch 2) created in admin; provisioning gave them packnames 1035–1038. Manager flag, print name, employee number and the deleted flag on B's packname were set in SQL |
| T3 | Pass. Packname 1035: customer null, manager 0, name / print name / employee number / list unchanged, while customer 2032 still existed (the event, not the key) |
| T4 | Pass. Packname 1036: customer null, still deleted |
| T5 | Pass. Customer 2034 in Guests only; packname 1037 still linked |
| T6 | Pass. *Run now* succeeded; 2032–2034 and six older guests removed (10 guests → 2); packname 1037's customer cleared by the key, `Manager` stayed 1 (the limit in §3; reset to 0 by hand afterwards) |
| T7 | Pass. Packname 1038: customer null, manager 0 after the ordinary delete of 2035 |
| T8 | **Not run.** The only packname customer with orders on `testnop50` is the shared test customer Steve Gates; a GDPR delete would anonymise him. Covered by nop's code: *Delete guests* skips any customer with an order, and orders keep `CustomerId` |
| T9 | Done. GDPR off, guest age 1440, cache cleared |

Left on `testnop50`: packnames 1035–1038 without a customer (1036 deleted); customer 2035 (deleted).