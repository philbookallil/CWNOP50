# Questions for the manager — features of the old system (4.1) not in the new one (5.00)

**Date:** 2026-11-05. **From:** Eduardo. **Status:** open — waiting for answers.

We have compared the old TIG system (nopCommerce 4.1) with the new CW system (nopCommerce 5.00) feature by feature,
reading the old code itself. The features below were in 4.1 and are not in 5.00, or work differently there. Before we
build anything we need you to tell us what the business needs **today**.

**How to read each question.**

- **What 4.1 did** is taken from the 4.1 code, not from memory. 4.1 is old and had changes over the years, so if you
  remember it working differently, please say so; your memory may be of a version or a store that the code no longer
  shows, and that is useful to know too.
- **What 5.00 does today** is how the new system behaves right now.
- **The question** asks for a decision. Where there are options, pick one or describe something else.
- **If you have no strong view** says what we would do, so "no strong view" is a valid answer.

Short answers are fine ("Q4: (b), 30 days"). If a question is about a feature nobody uses any more, "not needed" is a
complete answer.

| # | Topic | Section |
|---|---|---|
| Q1 | One order per packname | Ordering |
| Q2 | Whose subsidy pays when a manager orders for staff | Ordering |
| Q3 | Approval of Purchase Order orders by a branch leader | Ordering |
| Q4 | Welcome packs for new staff | Ordering |
| Q5 | Staff seeing what their manager ordered for them | Ordering |
| Q6 | Staff badges with one name per badge | Ordering |
| Q7 | Yearly limit per product type (Goodstart, Stride, TEEG) | Ordering |
| Q8 | Returns on orders that are only partly shipped | Returns |
| Q9 | Delivery charge on an exchange | Returns |
| Q10 | Finishing an exchange for a customer of another store | Returns |
| Q11 | Signing in to another store | Customers |
| Q12 | Staff changing their own branch | Customers |
| Q13 | Which address an order is billed to | Customers |
| Q14 | One page with all the important settings of a store | Store settings |
| Q15 | Reports | Reports |
| Q16 | ABN, banking details and payment terms on the invoice | Invoices and e-mails |
| Q17 | Copy of the order e-mail to the branch | Invoices and e-mails |
| Q18 | Size charts and embroidery codes | Catalogue |
| Q19 | Different category content per store | Catalogue |
| Q20 | Sending orders to Odoo | Integration |
| Q21 | Who may manage packnames and subsidies | Admin |

---

## Ordering

### Q1. One order per packname

You mentioned a setting so that, when a manager orders for many people, the system creates **one order per packname**.

**What 4.1 did.** No version of the old system created one order per packname. What 4.1 had is close to it but not the
same: a store setting that created **one order per branch**. It was built for Harvey Norman (stores 173 and 184).

- When a manager's cart held items for staff of several branches, checkout created one order for each branch.
- Each order was billed **and** shipped to its branch's address, had its own delivery charge, its own invoice (headed
  "Tax Invoice") and its own confirmation e-mail.
- Several staff of the same branch went into the same order.
- Payment was taken once for everything, and the customer could only pay by **Purchase Order**, because a card or
  PayPal payment cannot be split across several orders.

**What 5.00 does today.** One order per checkout. Inside it, each packname has its own delivery address, delivery
method and delivery charge, its own shipments, and every line says who it is for. The invoice lists every delivery
address and shows who each item is for.

**The question.**

1. What should be split: one order per **packname** (person), one per **branch**, or something else (department, cost
   centre)?
2. **Why** is it needed? For example: the customer's accounts team needs one invoice per branch or per person; the
   warehouse packs per order; a branch has to approve or pay for its own order. If the reason is only the paperwork,
   one order with **a separate invoice or packing slip per person or branch** may be enough, and is much simpler.
3. Which stores need it?
4. Is it acceptable that, when the order is split, the customer can only pay by Purchase Order (or another payment that
   does not charge a card at checkout)?

**If you have no strong view:** we keep one order and add a separate invoice and packing slip per branch, printed from
that order.

### Q2. Whose subsidy pays when a manager orders for staff

**What 4.1 did.** When a manager added items for a staff member, the manager could choose for each item whether it
was paid from **the staff member's subsidy** or from **the manager's own subsidy**.

**What 5.00 does today.** Each item is always paid from the subsidy of the person it is for. The manager's own subsidy
pays only for the manager's own items.

**The question.** Is there any store where a manager's own subsidy should pay for items ordered for other people?

**If you have no strong view:** we keep 5.00 as it is (each person's items use that person's subsidy).

### Q3. Approval of Purchase Order orders by a branch leader

**What 4.1 did.** A branch could be set to need approval. When someone at that branch placed an order paid by
**Purchase Order** above an amount set for the branch, the order was held and the branch leaders received an e-mail
with a link to **approve or decline** it. Unanswered requests were re-sent, for up to 90 days.

**What 5.00 does today.** No approval. Orders are placed straight away.

**The question.**

1. Does any customer still approve orders this way?
2. If yes: which stores; is the approval per branch, by amount, and for Purchase Order payments only (as before)?

**If you have no strong view:** we do not build it.

### Q4. Welcome packs for new staff

**What 4.1 did.** A manager uploaded an Excel file of new staff with, for each person, the products, sizes, colours and
quantities of their uniform pack. The system created the staff members (or packnames) and **filled the manager's cart
with all the items, already assigned to each person**. Rows with errors came back in an Excel file to correct.

**What 5.00 does today.** The Excel import creates packnames (and their accounts and subsidies) but **puts nothing in
the cart**. The manager then orders for each person by hand, or on the bulk ordering page.

**The question.** Do customers still use welcome packs? If yes, which stores, and roughly how many people per upload?

**If you have no strong view:** we do not build it.

### Q5. Staff seeing what their manager ordered for them

**What 4.1 did.** A staff member who signed in could see a list of **the items a manager had ordered for them**.

**What 5.00 does today.** On a staff member's *My Subsidies* page, the orders placed for them are listed as text, but
the orders cannot be opened. Neither the staff member nor the manager can open an order the other one placed.

**The question.** Should a staff member be able to see the items ordered for them (and their delivery status)? Should
a manager be able to open the orders of the staff they order for?

**If you have no strong view:** we add the staff member's list of items ordered for them; the manager side stays as it
is.

### Q6. Staff badges with one name per badge

**What 4.1 did.** For a "staff badge" product, the customer typed a list of names, and the system added one badge per
name to the cart, one each.

**What 5.00 does today.** Nothing like it.

**The question.** Are staff badges still sold this way? If yes, in which stores?

**If you have no strong view:** we do not build it.

### Q7. Yearly limit per product type (Goodstart, Stride, TEEG)

**What 4.1 did.** For these three customers only (their names were written into the code), a customer could order
only a limited quantity of some products **per year**, and a free product had to be removed before another could be
added.

**What 5.00 does today.** A subsidy can limit quantities, but only within that subsidy, not per product type per year.

**The question.** Are Goodstart, Stride or TEEG coming to the new system? If yes, are these limits still their rules?

**If you have no strong view:** we do nothing until one of them moves to the new system.

---

## Returns and exchanges

### Q8. Returns on orders that are only partly shipped

**What 4.1 did.** A customer could start a return on an order **still in progress** as long as some of its items had
already been shipped (common when an order for many people is sent in several parcels). Only shipped items could be
returned, and the days allowed for a return counted from **the last shipment**.

**What 5.00 does today.** It uses the standard nopCommerce rule: a return is allowed only on a **completed** order (or on
any order, if that switch is turned off), and the days are counted from the **date the order was placed**. CW already
lets the customer return only items that were shipped.

**The question.**

1. Should a customer be able to return items of an order that is not complete yet?
2. Should the return period count from the **order date**, the **shipment of that item**, or the **last shipment**?
3. How many days?

**If you have no strong view:** returns allowed on any order with shipped items, counted from the shipment of the item
being returned, with the number of days each store has today.

### Q9. Delivery charge on an exchange

*This is question 15 of the exchanges questions of September; it is still open.*

**What 4.1 did.** When the customer's cart held only exchange replacements, 4.1 charged **no delivery** and skipped the
delivery step.

**What 5.00 does today.** The customer pays the **delivery charge and the redelivery fee**. For example, exchanging a
$24 shirt on a store with $20 delivery: $20 delivery + $5 redelivery fee + $3 repackaging fee = $28 to pay, when the
shirt itself is covered by their subsidy. On stores with free delivery nothing changes.

**The question.** Pick one:

- **(a)** The redelivery fee **replaces** the delivery charge (no delivery charge on an exchange, as in 4.1).
- **(b)** Both are charged: the fee pays for handling and the delivery charge pays the courier.
- **(c)** **Each store chooses** (a) or (b).

**If you have no strong view:** (c), with (a) as the default because it is what customers had in 4.1.

### Q10. Finishing an exchange for a customer of another store

Sometimes an approved exchange still needs the customer to pay (fees or a price difference). Sales Support can finish
it for the customer by signing in as them ("impersonation").

**What 4.1 did.** If the customer belonged to **another store** (each store has its own web address), Sales Support
could still sign in as them on that store from the return page.

**What 5.00 does today.** It works within the same store only. For a customer of another store the button does not
work.

**The question.** Does Sales Support handle returns for customers of a store other than the one they are working in?
How often?

**If you have no strong view:** we remove the button for other stores (Sales Support opens the customer's own store
and signs in as them from there).

---

## Customers and sign-in

### Q11. Signing in to another store

**What 4.1 did.** A customer could sign in **only to their own store**. If they tried to sign in to another CW store
with the same e-mail and password, they were refused (with exceptions written into the code for stores 164 and 166,
"TIG Direct" and "Capsule Collection").

**What 5.00 does today.** No such check. A customer can sign in to any store.

**The question.**

1. Should customers be blocked from signing in to a store that is not theirs?
2. Should any store be an exception (for example a store open to everyone, like TIG Direct)?

**If you have no strong view:** we block it, with an exception list kept per store in the settings.

### Q12. Staff changing their own branch

**What 4.1 did.** A store could allow staff to change their own branch, from *My Account* or at checkout.

**What 5.00 does today.** Only an administrator can change a person's branch. (In 5.00 the branch belongs to the
person's packname, so changing it also moves them in the store's staff lists.)

**The question.** Should staff be able to change their own branch? In which stores?

**If you have no strong view:** only administrators, as today.

### Q13. Which address an order is billed to

*This is question 16 of the exchanges questions of September; it is still open.*

**What 5.00 does today.** The customer cannot choose the billing address. The order is billed to the customer's own
address, and the checkout totals use the store's default address for tax. Where tax depends on the address, the total
shown and the total charged can differ. A customer with no address on their account cannot check out.

**The question.** What should an order be billed to: the **customer's** address, the **branch's** address, or should
the customer **choose**?

**If you have no strong view:** the branch's address when the person belongs to a branch, otherwise their own.

---

## Store settings

### Q14. One page with all the important settings of a store

**What 4.1 did.** The *Manage Store* page had tabs for the store's details (name, web address, company details), TIG's
own settings, and five pages of nopCommerce settings (general, payment, delivery, catalogue, shopping cart), all for the
store being managed.

**What 5.00 does today.** *Manage Store* has CW's own settings, exchange rules and fees. The nopCommerce settings are
on nopCommerce's own *Configuration › Settings* pages, where you choose the store at the top. The store's details are
on *Configuration › Stores*.

**The question.**

1. Which settings do staff **check or change** for a store, most often? A short list is enough, for example "logo,
   payment methods, free delivery over $X, theme".
2. What is most useful to them? Pick one:
   - **(a)** **Links** on *Manage Store* that open each nopCommerce settings page already set to that store.
   - **(b)** A **summary tab** on *Manage Store* showing the current value of the important settings of that store,
     each with a link to change it.
   - **(c)** **Edit everything on *Manage Store*,** as in 4.1. This costs the most to build and to keep working
     through nopCommerce upgrades.

**If you have no strong view:** (b).

---

## Reports

### Q15. Reports

**What 4.1 had, and 5.00 does not:**

- For a customer's managers, on the website: downloads (CSV) of **sales by style**, **sales by branch**, **stock on
  hand** and **subsidy use** by store or branch.
- For administrators: a **subsidy report to Excel** (per subsidy and per person), a **product group usage report**
  (% used), and a **list of all stores' subsidies** with totals.
- Statistics on the admin dashboard **per store**. 5.00's dashboard shows all stores together; its *Reports* pages
  (bestsellers, sales summary) can be filtered by store.

**What 5.00 has:** the credit note reports, each person's subsidy history, each store's subsidy list with credit
and % used, and the packname export to Excel.

**The question.** Which of these reports do customers or staff still use, and who uses each one? Mark each one
"needed", "nice to have" or "not used".

**If you have no strong view:** we build the admin subsidy report to Excel only.

---

## Invoices and e-mails

### Q16. ABN, banking details and payment terms on the invoice

**What 4.1 did.** Invoices showed the company **ABN** and a **banking details** block, and for orders placed by a
branch manager, the branch's **payment terms**.

**What 5.00 does today.** None of these. The branch payment terms can be typed in the branch screen but are printed
nowhere. nopCommerce can already print a VAT/ABN number and two footer text columns on the invoice, which may be
enough for the ABN and banking details without new work.

**The question.** Are these still needed on invoices? Is the same ABN and bank account used for every store?

**If you have no strong view:** ABN and banking details through nopCommerce's own invoice fields; payment terms printed
under "Invoice to" for branch orders.

### Q17. Copy of the order e-mail to the branch

**What 4.1 did.** A store could send a copy of the order confirmation to the **billing address's e-mail** when a
branch manager ordered, so the branch's shared mailbox received it.

**What 5.00 does today.** The confirmation goes to the customer only.

**The question.** Do branches still want a copy? To which e-mail: the billing address's, or one e-mail set per branch?

**If you have no strong view:** we do not build it.

---

## Catalogue

### Q18. Size charts and embroidery codes

**What 4.1 did.** Administrators built **size charts** (sizes against measurements, with a picture) and assigned them
to products; the chart showed on the product page. Separately, products could carry **embroidery codes** for each
colour/size combination.

**What 5.00 does today.** Neither exists, and nopCommerce has nothing similar.

**The question.**

1. Are size charts still needed on the product page? If yes, would they come from **Odoo** with the products, or be
   maintained on the website?
2. Are embroidery codes still used? Where should they come from?

**If you have no strong view:** we do not build them.

### Q19. Different category content per store

**What 4.1 did.** One category could show a **different description and picture per store**, and be shown on the home
page or the top menu of some stores and not others.

**What 5.00 does today.** A category can be shown or hidden per store, but its description and picture are the same in
every store that shows it. (The same need for pages such as "About us" is already covered: each store can have its own
copy.)

**The question.** Do stores need a different category description or picture?

**If you have no strong view:** we do not build it.

---

## Integration

### Q20. Sending orders to Odoo

**What 4.1 did.** Every order was sent to **Jim2** (the old accounting system), and shipment and invoice information
came back from Jim2.

**What 5.00 does today.** The new system **receives** stores, departments, products (with stock) and branches **from
Odoo**. Nothing sends orders to Odoo, and nothing brings shipment status back.

**The question.** Will orders need to go into Odoo, and shipments come back from it? If yes, who is planning that, and
when?

**If you have no strong view:** nothing to decide now; we note it as a future project.

---

## Admin

### Q21. Who may manage packnames and subsidies

**What 4.1 did.** Separate permissions for subsidies and for product groups, and a set of roles (Inventory, Accounts,
Sales, Branch Manager, and others).

**What 5.00 does today.** One permission gives access to everything in CW's store management: stores, branches,
departments, packnames **and** subsidies. Someone who should only manage packnames also gets to change stores and
subsidies.

**The question.** Which kinds of staff use the admin area, and what should each be allowed to do? For example: "Sales
Support: packnames and returns, not subsidies".

**If you have no strong view:** separate permissions for stores and branches, packnames, and subsidies; staff roles
set up with you before going live.
