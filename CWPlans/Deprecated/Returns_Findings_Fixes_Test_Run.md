# Returns findings fixes — dev-site test run (2026-11-16)

Spec: `CWPlans/Returns_Findings_Fixes_Spec.md` (changes 1–34). Site: https://localhost:59579, plugin `2026_11_16_DATA_0.2`.

| Test | Result | Evidence |
|---|---|---|
| Plugin update | ✅ | Templates `ReturnAuthorisedCredit.CustomerNotification` (73) and `ReturnRejected.CustomerNotification` (74) installed, active; no raw resource keys on the return page or the store page |
| T1 Spent credit note (D1) | ✅ | James: no Delete on his three spent notes; server refuses #1002 ("already paid for an order"); a fresh $5 note (#2003) shows Delete and deletes |
| T2 Basic Data (D2/D3) | ✅ | 1003/1004/1007/1009: Store, Branch, Ordered for (packname), Replacement product (link), options, price; credit returns 1005/1006/1008 show no Exchange Type. Labels from resources; "Customer name" vs nop's "Customer" |
| T3 Required products (D4) | ✅ after change 29 | 1007 (hat, requires the shirt) authorised: hat alone in James's cart, no shirt added. Before change 29 the approval failed ("Please select Size") and rolled back cleanly |
| T4 Rejected fees panel (D5) | ✅ | 1005 and 1007 (while rejected): "Nothing Credited or Charged (Return Rejected) USD 0.00"; stays 0 when a fee is typed |
| T5 Back in stock (B1) | ✅ | Two of three ticks (2003, reason 3 / action 3, status off): stock unchanged (shirt 9994). All three (1007, reason 1 / action 2 / Return authorized): "1 item(s) have been successfully returned to the stock", shirt 9994 → 9995, quantity returned 1 |
| T6 E-mails and note (B2) | ✅ | 1002 Pending → Rejected: e-mail 3065 "Update on your return #1002" with the note and its line break; → Cancelled: no second e-mail. 2003 authorised (allowance given back, no note): e-mail 3064 "Your return #2003 is approved" with only the allowance line. My returns shows "Note from the store" on decided returns |
| T7 Rejected units (B3) | ✅ | 1005 (rejected) shirt returnable again on order 2013 → return 2003 submitted; re-opening 1005 then refused: "This return cannot be re-opened…" |
| T8 Delivery (B4) | ✅ | James, hat replacement only: Shipping $0.00; with a laptop added to the same packname: $9.95; laptop removed: $0.00 again |
| T9 Credit Notes off (B5) | ✅ | Steve, order 2014: reason 1 + Store Credit → "no return options"; reason 1 + Replacement → Exchange only. James's existing $9.44 still applied at checkout. Switched back on |
| T10 Re-open (Q1) | ✅ | 1007 and 1002 Rejected → Pending: decision note cleared, line Pending; 1007 then authorised and executed |
| T11 Exchanges off (Q2) | ✅ | With Exchanges off James checked out the hat replacement: no warnings, shipping $0.00, store credit $24.44 applied, order 3013 placed. Switched back on |

Not covered: a plain (non-packname) cart with only replacements (no plain-cart customer in the test data); Undo approval
on a credit return (the button is offered only while an exchange waits for checkout — a credit return cannot be undone).

Left on the dev site: returns 2003 (authorised, allowance restored) and 1007 (authorised, order 3013 placed for the hat,
credit note $15 raised and spent with the old $9.44); 1002 back to Pending; queued e-mails 3064–3065 and the ones of order
3013; shirt stock 9995. Rule ticks and the Exchanges / Credit Notes switches are back as they were.
