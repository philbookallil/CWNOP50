# P9 — Interface

> **4.9 history.** Copied from the 4.90 source (`NOP49/CWPlans/`) on 2026-10-31, when CW.Core moved to nopCommerce 5.00. It is kept as the record of the 4.9 work and is **not updated for 5.00**: file paths, line numbers and versions are 4.9's. For how CW.Core works now, read the references in `CWPlans/` and `CWPlans/CWCore_Port_Reference.md`.

> **The pager half of P9 is DONE (2026-09-02) and its spec has been deleted.** Windowed page numbers with
> first/previous/next/last, nop's own arithmetic ported from `Pager.cs:456-481`, one delegated click listener.
> The two things worth remembering are already comments in the code: `BuildPageNumbers`'s XML doc says why the
> arithmetic is *ported* rather than nop's `Pager` reused (its markup emits `<a href>` and navigates, which
> would discard every typed quantity on the page), and `BulkOrderDefaults.PAGER_WINDOW` says why the window is
> 5 (nop's own `IndividualPagesDisplayedCount`, so the control behaves the way the catalogue taught the
> customer it would). What remains of P9 is everything below.


**Status: the structural half (I1–I5) is DONE.** Applied 2026-09-01: the selectors bar, `_Items.cshtml`,
`bulk-order.css` and `init()` in `bulk-order.js`. Its working document is consumed and deleted
(`CLAUDE.md §0.4`); what was durable in it is now design **§4.2**, "The card's shape, and three rules that are
not obvious" — including the one rule that was specified there and never applied, the narrow-viewport
breakpoint, which was found missing on 2026-09-05 and is now in the CSS. Everything below is the reasoning
that produced the phase.

**Status of this file: proposed, not scoped.** This file exists because you asked which phase owns look and feel, and
the honest answer is **none of them**. P4 is conditions, P5 pricing and availability, P6 add to cart,
P7 caching, P8 the product-page variant. Nothing after P3 improves how the screen reads, so the CSS that
shipped with P3 — deliberately minimal scaffolding, enough to prove the markup — is what a user gets
unless a phase is created for it.

That was a reasonable call while the grid was being built and it stops being reasonable now: the screen
works and is unpleasant to read, which is the point at which the layout stops being scaffolding and starts
being the product.

**One thing in here is a defect, not a preference**, and it is fixed separately rather than waiting for
this phase — see "Already fixed" below.

## When this runs — decided 2026-09-01

The phase is **split**, because its two halves have opposite dependencies.

**The pager runs next, straight after P3 acceptance.** It depends on nothing in P4–P6: page numbers do not
change when conditions, prices or an add-to-cart button arrive. Waiting would only mean staring at it for
three more phases.

~~**The layout, density and hierarchy work runs after P6.**~~ **Revised the same day, and the reason is
item I4.** The original argument was that a product row is meant to carry a price (P5), stock (P5), a
per-line error (P6) and an add-to-cart control (P6), so designing its hierarchy before those exist means
designing it twice.

That argument dies once the row becomes a full-width **header plus a full-width body**. In the current
three-column row there is nowhere to put a price, a stock badge or an add button — which is precisely why
building P5 and P6 into it and restyling afterwards would be the expensive order. In the header structure
each of them has an obvious home. So the structure should be built **before** P4, and the later phases fill
it in rather than fight it.

What still waits: the fine detail — exactly where the price sits relative to the stock badge, how a per-line
error is shown. That genuinely cannot be settled until the elements exist.

The intake table stays open the whole time. Anything in it that is a **defect** is pulled forward
immediately — that is what happened to the wrap — and anything that is taste waits for its half.

---

## Already fixed — the product list wrapping under the category tree

Not taste; a flex line-breaking bug.

`.bulk-order-layout` is `display: flex; flex-wrap: wrap`, and `.bulk-order-main` was
`flex: 1 1 auto`. With `flex-basis: auto` a flex item's hypothetical main size is its **content** width,
and a wide grid table makes that enormous. `220px + enormous` exceeds the row, so the browser wraps the
main column onto a second line — below the whole category tree. `min-width: 0` does not prevent this: it
lowers the shrink floor, but line-breaking reads the basis, not the floor.

Two rules, and both are needed — the first stops the wrap, the second stops a genuinely wide grid from
overflowing the card instead:

```css
.bulk-order-main {
    flex: 1 1 0;
    min-width: 0;
}

.bulk-order-product .product-grid {
    flex: 1 1 100%;
    margin-top: .75rem;
    overflow-x: auto;
}
```

This gets worse, not better, once the acceptance data is loaded: `ZZT-19` renders twelve columns and
`ZZT-13` twelve more. Without `overflow-x` those push the card wider than the page.

---

## 1 — The pager

You are right that it is the worst thing on the screen, and right that nop already solved it. nop's
catalogue pager renders first / previous / **individual page numbers** / next / last, windowed around the
current page, and the windowing algorithm is worth copying exactly rather than reinventing —
`Presentation/Nop.Web.Framework/UI/Paging/Pager.cs:456–481`:

```
window = IndividualPagesDisplayedCount        // nop's default is 5

first = 0                                     if TotalPages < window, or PageIndex - window/2 < 0
      = TotalPages - window                   if PageIndex + window/2 >= TotalPages
      = PageIndex - window/2                  otherwise

last  = TotalPages - 1                        if TotalPages < window, or PageIndex + n >= TotalPages
      = window - 1                            if PageIndex - window/2 < 0
      = PageIndex + n                         otherwise
        where n = window/2, minus one more when window is even
```

**What we cannot copy is the markup.** nop's pager emits `<a href>` links and navigates; ours must not —
paging is AJAX (`NOP46` reference §12.2), and a link would reload the page and lose every typed quantity
on it. So the numbers become buttons carrying `data-page`, and `bindPager` swaps its two listeners for one
delegated handler:

```js
pager.addEventListener('click', function (e) {
    var btn = e.target.closest('[data-page]');
    if (btn && !btn.disabled) load(parseInt(btn.dataset.page, 10));
});
```

That is smaller than what is there now, and it survives the markup being re-rendered because it binds to
the container rather than to the two buttons.

Open questions for whoever scopes this:

- Does the window size become a setting, or stay at nop's 5? A setting is one more thing to explain; 5
  matches the rest of the site, which is an argument on its own.
- Keep the "Page 4 of 5 (64 products)" summary alongside the numbers? It is what the screen-reader live
  region currently announces after a page change (`bulk-order.js:212`), so removing it means moving that
  announcement somewhere else, not just deleting a span.

---

## 2 — Density and hierarchy

From the screenshot, in the order they hurt:

- **The category tree is the tallest thing on the page** and pushes the first product below the fold. It
  is a control, not the content (design §4.3), and it is currently sized as though it were the point of
  the screen. Options: collapse to the current branch, cap its height with its own scroll, or move it into
  the filter row as a dropdown.
- **The product image is 100px and the name, SKU and price are stacked in a 100px-wide column** beside it,
  so "ZZ Test 02 — Single axis" wraps across three lines next to a picture that has room to spare. The
  three columns of the product row (image / info / grid) do not have proportions; they have defaults.
- **The grid table has no visual weight relative to the product info.** The thing you are meant to type
  into reads as lighter than the label next to it.
- **Nothing separates one product from the next** except a 1px rule, at the same weight as the rules
  inside the grid.

## 3 — Intake

Raised 2026-09-01 from the first working screens. Triage column: **defect** is pulled forward immediately,
**structure** belongs to the layout change described in item 4, **pager** goes with the pager work.

| # | What | Triage |
|---|---|---|
| I1 | A product with a single quantity box leaves a large dead area to its right; the box floats mid-row | structure |
| I2 | Product images are left-aligned and top-aligned against the text; nop's list view reads far better | structure |
| I3 | The grid gets a horizontal scrollbar at **12 boxes** — and most real products have more | **defect (mine)** |
| I4 | Product data should be a full-width header, with the attribute grid below it using the whole row | structure |
| I5 | The filter row is all flush left; nop splits it and uses two icons for view mode, not a dropdown | structure |
| I6 | The single quantity box should be **right aligned**, alongside the add-to-cart button, not centred | **post-P6** |
| I7 | The pinned strip does not read as separate attributes — three controls run together as one row | **done 2026-09-05** |
| I10 | The pager's five-page window hides the fact that more pages exist — six pages read as five | **interface** |
| I11 | A failed cell's warning is rendered inside the cell and widens its whole column | **done 2026-09-05** |
| I12 | The category tree is the tallest thing on the page and pushes the first product below the fold | **done 2026-09-05** |
| I13 | After a page-wide add, nothing says WHICH cards failed — the pill is at the bottom, the lists are scattered above | **done 2026-09-05** |
| I14 | The screen has never been designed for a phone, and the quantity boxes and Add button are unreachable there | **done 2026-09-09** |
| I15 | Desktop density and alignment: things not aligned, sizes to revisit, and more data wanted inside each grid | **done 2026-09-08** |
| I17 | The theme's own notification bar is 456px wide and gives EVERY page of the store a horizontal scroll on a phone | **theme, not ours** |

---

### I3 — the scrollbar is mine, and it is not good enough

I introduced it. The fix for the list wrapping under the category tree was two rules, and the second was
`overflow-x: auto` on `.product-grid`. That traded a broken layout for an ugly one, and at twelve boxes —
Size × Logo on the Nike shirt — it is already scrolling. You are right that this is not acceptable: the
overwhelming majority of real products carry more than twelve, and a screen where every product has its own
little scrollbar is not a screen anyone wants to place an order on.

The scrollbar is a symptom. The cause is that the grid is competing for horizontal space with the image and
the product info in the same flex row, so it gets roughly half the page to render a table that wants all of
it. **Item I4 is the actual fix** — a full-width grid has something like three times the room, which takes
the common cases below the scroll threshold entirely.

Keep `overflow-x: auto` as the last resort for the genuinely enormous grids (`ZZT-19` at twelve columns,
`ZZT-13` at twelve): a scrollbar on a handful of extreme products is defensible, one on a two-attribute
shirt is not. What must not happen is going back to the wrap.

Wrapping cells instead of scrolling was your other suggestion, and it is worth stating why it does not work
for this control specifically: the grid is a **matrix**, and a cell only means anything at the intersection
of its column header and its row label. Wrap the columns onto a second line and a quantity box is no longer
under a size, so the customer cannot tell what they are typing into. Long axes want a different answer —
freezing the row-label column so it stays visible while the rest scrolls, or splitting a very long axis into
banded sections with the header repeated. Both are real options for the layout work; wrapping is not.

### I4 — product data as a header, grid below

This is the right structure, and it is worth spelling out why beyond the space saving:

- A single-box product (**I1**) stops having a mid-row void, because there is no mid-row: the header spans
  the width and the one box sits under it.
- The grid gets the full content width (**I3**).
- The image and the product text get a proper relationship to each other instead of both being squeezed by
  a table (**I2**).
- **It is the shape the later phases need.** A row is going to gain a price, a stock indicator, a per-line
  error and an add-to-cart control. In the current three-column row there is nowhere to put them. In a
  header with a full-width body there is an obvious place for each.

**Resolved 2026-09-01.** You said the single box should be *right* aligned; I read that as two possible
things and asked. Agreed reading: the **grid area stretches to the right edge so there is no leftover
space**, and the box itself sits at the **left** of that full-width area, under the header, lined up with
where every other product's first column starts. The first quantity box is then in the same place on every
card whether the product has one box or eighty. **Superseded by I6 (done 2026-09-05):** once the add-to-cart
button existed, the box and the button became one cluster and moved right together, which is what the original
note anticipated.

### I6 — the single box, deferred to P6 on purpose

Revision 3 of the layout change made a single quantity box a full-width one-cell strip with the input
**centred**, on the argument that a product with no attributes is a grid with exactly one cell and should
look like one. That argument is about the grid. It is not about what a customer does on that card.

What they actually do there is type a quantity and add it to the cart, and the add-to-cart control does not
exist until P6. Once it does, the box and the button are one cluster and the cluster belongs on the right,
where the eye ends up and where nop puts its own buy controls. Placing the box now, alone, is deciding half
of a two-part control — which is the same mistake as designing the row's hierarchy before it carries a
price and a stock badge.

**So: the centred version stands until P6, then the box and the button move right together.** Not a
compromise — the position is genuinely undecidable until the thing it sits beside exists.

### I7 — the pinned strip reads as one control, not three

**Added 2026-09-05, from the required-text fix.** Two things now sit on top of I7's original complaint, and
both are about the strip saying too much rather than too little:

- **Measured: no live product renders more than one pin**, and there are **no Checkboxes or
  ReadonlyCheckboxes attributes anywhere in the catalogue**. 103 of the 104 live pins are the free-text
  *"Not available on this screen"* line. The three-controls-in-a-row problem I7 describes is `ZZT-11` and
  nothing else — so this item's **priority** drops sharply. Its **scope** does not: the next client may
  configure checkbox pins everywhere, and the strip has to hold N pins of any control type.
- **A card with a partially-blocked grid now states one fact four times**: the strip's label, its
  *"Not available on this screen"*, its *"Applies to Embroidered only"*, the notice above the grid, and
  *"Product page only"* in every affected cell. The strip entry is the redundant one — when a pin is the
  blocking one, the notice says it better. Reducing or suppressing that entry belongs here.

Screenshot 2026-09-01, `ZZT-11`: **Fit \*** (a select), **Embroidery options** (three checkboxes) and
**Name to print** (an unsupported-attribute message) sit in one row separated only by a gap, every label at
the same weight and colour. Nothing says where one attribute ends and the next begins, and the checkbox
group is the worst case — *Name*, *Department* and *Initials* read as three peers of *Fit* rather than as
three values of one attribute.

It is a grouping problem, not a spacing one: more gap would push them apart without saying they are
different things. What the strip needs is a visible boundary per attribute, a label that is clearly a label
(the required asterisk is currently the only thing distinguishing one), and the checkbox set visually
contained so it reads as one control.

**Triage: the pager batch, not post-P6.** The strip gains nothing from P5 or P6. It does gain something
from **P4** — conditional pins that appear and disappear as axis values change — but a conditional pin is
just a pin that is sometimes absent, so grouping designed now still holds. Worth doing before P4 rather
than after, so P4 builds into a strip that already has a shape.

Markup lives in `Views/BulkOrder/_PinnedStrip.cshtml`, styles under `.bulk-order-pins` in
`bulk-order.css`. The three branches (text/date/file unsupported, checkboxes with `role="group"`, select)
already exist and are correct — this is presentation only, no view logic changes.

### I2 / I5 — what nop actually does, for reference

**List view** (`Themes/DefaultClean/Content/css/styles.css:7457`): the picture is a fixed **220px** column
floated left, and the details block is offset by a matching `margin-left: 240px`. That is the whole trick —
every product's text starts at the same x position regardless of how tall or wide its image is, so the
column edge reads as a line down the page. Ours sizes the image column from a setting
(`--imageSize`, default 100px) and lets the text start wherever the image ends.

**Selectors** (`Views/Catalog/_CatalogSelectors.cshtml`): one bordered bar, top and bottom rule, and view
mode is **two icon toggles** — `<a class="viewmode-icon grid">` and `<a class="viewmode-icon list">` with a
`selected` class — not a dropdown. You are right that customers already know this control from the rest of
the site; ours makes them learn a second way to do the same thing on one page. Sorting and page size stay as
labelled selects there, which is worth copying too: `Show` / `Sort by` / `Display` currently have their
labels stacked above the controls, which is what makes the row look twice as tall as nop's.

Note this is `DefaultClean`, and the store runs `CW.Standard`. The structure to copy is the markup and the
class names, not the pixel values — matching nop's *classes* is what makes a theme change improve our screen
instead of breaking it.

---

## I8 — a pin that vanishes should say why — DONE 2026-09-05

Built as **option 1**, and it absorbed case 2 as well: dropping an axis puts the attribute back in the pin
list, where case 1 renders it disabled. One resource, `Grid.ChooseFirst`. **F19 closed with it.** The rules,
the two properties that hold them together and the deliberate divergence from nop are all in design **§9.1**,
"An unmet condition shows itself" — this section is the reasoning that produced them.

Two things found while building it, both recorded in `tasks/lessons.md`: a condition **chain** would have let a
blocked pin's remembered value unlock the attribute below it (#29's sibling, fixed by restarting the pass),
and the fix reached the view only after the **factory's** own §12.5 early return was made to carry the pins —
the builder had them all along (#30).

---

### The original write-up, kept for the reasoning

## I8 — a pin that vanishes should say why (added 2026-09-02, after P4)

P4 gives §9.1 **case 3** a note beside the control — *"Applies to Navy only"* — because the customer is
entering one value that will land on some lines and not others. **Cases 1 and 2 get nothing.** On `ZZT-23`,
unticking *Embroidery* makes *Print style* disappear from the strip with no explanation at all; on `ZZT-24`
an entire axis appears and disappears as *Print style* is toggled.

This is the common shape, not the rare one: on the measured catalogue the largest conditional group is
`Print Text ← Print Name` at 202 products, and that is **case 1**. So the one case with no explanation is the
one almost every conditional product uses.

What it needs is a sentence, not a mechanism — the model already carries everything required
(`Conditions` knows the depended-on mapping, and `data.Names` its label). Two options, in preference order:

1. **Leave the control in place, disabled, with a reason** — *"Choose Embroidery first"*. Closest to nop's
   own product page, which renders the field and hides it with JavaScript rather than omitting it, and it
   removes the "did that control just disappear?" moment entirely. It also makes **F19** moot, because the
   control still exists in the DOM to read a value from.
2. **A note where the control was** — cheaper, but a line of text appearing where a control used to be reads
   as an error message.

**A third instance, found 2026-09-05.** When a required free-text attribute blocks the whole product, the
§12.5 card replaces the grid **and the pinned strip with it**, because that early return carries no `Pins`. On
`ZZT-51` the customer ticks a checkbox, the card appears, and the checkbox they just ticked is gone — only a
page reload brings it back. Same shape as I8's own complaint, same fix: the control that caused the state has
to survive the state. Fixing it needs the pin projection lifted out of `BuildAsync`'s return into a local
function so both returns can use it.

Option 1 changes what `hidden` means to the *view* only — the pin still contributes nothing to any cell key,
which is the part that must not change. Worth deciding together with F19 rather than separately: they have
the same fix.

## I9 — a quantity box accepts more than it can sell — DONE 2026-09-05

**Built as the clamp only, and the reason matters.** The original write-up wanted the client and the server to
use the same number. They cannot: `AddToCartAsync:1627` validates the MERGED quantity — the existing cart
line's plus the submitted one — so on a max-10 cell with 6 already in the cart the true ceiling is 4, and no
number on the page is 4. That needs the per-cell cart quantity, which is **F21** and is not built. So the
clamp snaps to the rendered `max`, names the cell and says what it changed, and the server stays the
authority; I11's summary is where its refusals now read.

Deliberately **not** clamped up to `OrderMinimumQuantity`: typing `2` on the way to `25` would snap under the
customer's fingers, and the grid already renders nop's own minimum-quantity notice.

Measured before building: **131 cells in the whole catalogue** render a cap at all, 86 of them at stock 1–10.
Everywhere else `MaxAvailable` is 10000 and the clamp is inert — which is why it is a dozen lines rather than
a mechanism.

---

### The original write-up, kept for the reasoning

## I9 — a quantity box accepts more than it can sell (found in P5 acceptance, 2026-09-03)

`ZZT-36`'s S/Navy cell has ten in stock and renders `max="10"`, and you can still type **15** into it. That is
not a bug in the markup: HTML's `max` constrains the spinner arrows and form validation, and constrains
nothing about typing. The number sits there looking accepted.

Two things are needed and they are **not** the same change:

- **Here (interface):** clamp on `change`/`blur` and say what happened — the value snaps to 10 with a brief
  note beside the cell, rather than silently rewriting what the customer typed. Silent correction is worse
  than none: a customer who typed 15 and looks away has ordered 10 without being told.
- **P6 (add to cart):** refuse the line server-side regardless. The client clamp is a courtesy, never the
  check — a typed value reaches the server through a request the page does not control, and
  `GetStandardWarningsAsync` is what actually decides. The clamp exists so the refusal is rare, not so the
  refusal can be skipped.

Worth doing together with the **"n in cart"** interaction, which is the same question asked differently: the
`max` we render is the cell's stock and takes no account of what is already in that customer's cart for the
same cell. P5 recorded that deliberately — nop compares the *submitted* quantity against stock
(`ShoppingCartService.cs:502`), and how a bulk line merges with an existing cart line is P6's decision. Once
P6 has made it, the clamp should use the same number, or the two will disagree in exactly the case a customer
notices.

---

## I10 — the pager hides the pages it is not showing (reported 2026-09-04)

> **Scale corrected 2026-09-05.** While measuring I12 this document's justification for I10 turned out to be
> wrong by an order of magnitude. It read "4,198 visible products, 280 pages at the default page size, 34 in
> the largest category". Those are ALL-STORES figures: products here are almost entirely store-exclusive, so a
> customer sees **27 to 222 products — a median of about 3 pages, and 15 at the very most**. Only about
> **12 of 94 stores** exceed the five-page window at all.
> The item stands: a 15-page store showing five numbers hides ten pages, and the fix was a dozen lines. But it
> is a small-store screen, not a 280-page one, and the record should not claim otherwise.

**Reported from a live page 3 of 6.** The control renders `« ‹ 1 2 3 4 5 › »` and, beside it,
*"Page 3 of 6 (89 products)"*. The number six appears only in that sentence. Everything the eye actually
reads — the row of boxes — says five, and the customer stops at five.

**This is working as specified, which is the problem.** `BulkOrderDefaults.PAGER_WINDOW` is 5 because nop's
own `IndividualPagesDisplayedCount` is 5, and `BuildPageNumbers` is nop's arithmetic ported verbatim
(`Pager.cs:456-481`). On page 3 of 6 that window centres to 1-5 and page 6 is simply absent. **nop's own
catalogue pager behaves identically** — so this is not a defect against nop and not a porting mistake. It is
a place where copying nop faithfully produced something worse than the sentence sitting next to it, and the
sentence is the only thing telling the truth.

**What it should be.** The window keeps its five numbers; the control gains an ellipsis on whichever side is
truncated, and the first and last page numbers stay visible so the range is legible at a glance:

```
«  ‹  1  2  3  4  5  …  6  ›  »          page 3 of 6, more to the right
«  ‹  1  …  4  5  6  7  8  ›  »          a longer run, truncated on the left
«  ‹  1  2  3  4  5  ›  »                exactly five pages - unchanged, no ellipsis
```

Two rules decide it, and both are cheap because `PageNumbers` already knows the window: show `1 …` when the
window does not start at 1, and `… n` when it does not end at `TotalPages`. The ellipsis is a `<span>`, not a
button — it is a gap marker, not a destination — and it must be `aria-hidden`, because the live region
already announces *"Page 3 of 6"* and a screen reader reading "ellipsis" adds nothing.

**Where.** `_Items.cshtml`'s `@foreach (var number in Model.PageNumbers)` block, `bulk-order.css`'s
`.bulk-order-pager` rules for the new span, and — the decision that shapes it — whether the first/last numbers
are added to `BulkOrderingModel.PageNumbers` by the factory or rendered as a special case in the view. **The
factory**: the view should not be doing arithmetic about which pages exist, and `bindPager`'s delegated
listener already reads `data-page` off whatever buttons are there, so adding two more needs no JS at all.

**Do not lose what the current control gets right.** `«` and `»` already jump to first and last, and are
disabled at the ends. The ellipsis is about *reading* the range, not about reaching it — first and last are
already reachable in one click, which is why this is an interface item and not a defect.

## I11 — a cell's warning must not live inside the cell (reported 2026-09-04, after P6)

**Reported from `ZZT-36`.** One refused line renders nop's sentence — *"Your quantity exceeds stock on hand.
The maximum quantity that can be added is 10."* — inside the `S` cell, and the whole **S column** stretches to
fit it. On a two-column test product that is ugly. On a real product with twelve or twenty columns it is
unusable: one refused cell reflows the entire grid, and every other column collapses to make room.

This is P6's `applyLineResults` appending a `.cell-warning` span into `input.parentNode`. It was the right
call for attributing a failure to its cell and the wrong call for where the text goes.

### What it should be

**In the cell: a red border and nothing else.** The cell keeps its typed quantity and its mark; it gains no
width.

**Above the grid: the messages, each naming its cell.** One refused line reads as a sentence; several read as
a list. The cell name comes first so the customer can find it:

```
Size S, Color Navy — Your quantity exceeds stock on hand. The maximum quantity that can be added is 10.
```

**nop's sentence is not rewritten**, only prefixed. §11.2 stands: the customer must not be told two different
things about one rule by two different screens.

### The border is genuinely broken, and here is why

`.bulk-qty.line-failed` sets `border-color` alone. The theme's input rule (`CW.Standard/Content/css/styles.css:106-115`)
covers `input[type="text"|"email"|"tel"|"password"]`, `textarea` and `select` — **not** `input[type="number"]`.
So the quantity box keeps the user agent's default `border: 2px inset`, and recolouring an inset border makes
the browser render its 3D split in red: dark on two sides, light on the other two. That is the "only bottom
and right are red" in the screenshot, and it is also why the boxes look unlike every other input on the site
even when nothing has failed.

Two rules, not one: give `.bulk-qty` an explicit `border: 1px solid #ddd` to match the theme's other inputs,
and make `.line-failed` set the full `border: 1px solid #a94442` rather than only its colour.

### Where the cell's name comes from

`BulkOrderGridCellModel.Label` is already on the element as `aria-label`, so the client needs nothing new on
the wire — but today it is **values only** (`"S, Navy"`, from `BulkOrderModelFactory.cs:427-446`), and the
customer needs the attribute names too.

Qualify it in the **factory**, by pairing `RowAxisNames` with `row.Labels` and `ColumnAxisName` with
`cell.Column.Name` — both are already on `BulkOrderGridModel` for the table header. Extend `Label` itself
rather than adding a second property: `"Size S, Color Navy"` is a better aria-label than `"S, Navy"`, and the
model's own XML comment already describes the qualified form (*"e.g. Navy, Regular, size 12"*), so this brings
the code to what it was documented to do. One change, two surfaces improved, nothing added to the response.

### Three things not to lose

- **Clearing.** `clearLineResults` removes the marks before each submit. The summary block has to be cleared
  in the same place, or a fixed cell keeps its message at the top of the card.
- ~~**The pill's wording.**~~ **Corrected 2026-09-05: no resource change is needed.** This file claimed
  `AddToCart.SomeFailed` ends *"see the notes below"*. It does not — `CWCorePlugin.cs` has *"{0} added to your
  cart. The rest are marked with the reason."* and `AddToCart.Failed` *"Nothing could be added. Each line is
  marked with the reason."* Both stay true after the move: the cell keeps its mark, and the reason is on the
  same card. Leaving them alone also avoids a `plugin.json` version bump. The lesson is the older one — a
  document that quotes code is not the code.
- **Single-cell products.** Design §4.2's path has one box and no columns, so there is nothing to widen and no
  coordinate to name. The message there should stay a plain sentence with no cell prefix — a card that says
  *"— Your quantity exceeds..."* with an empty name reads as a bug.

## I12 — the category tree is the tallest thing on the page (raised 2026-09-01, tabled until now)

The one bullet of §2 that the layout change did not absorb, and it never made it into the intake table, so it
has been living in prose. Still live: `.bulk-order-side` is `flex: 0 0 220px` with no height cap
(`bulk-order.css:121`), so on a deep category tree the first product starts below the fold.

**Two causes, and only one of them was taste.**

**Depth — a divergence from nop, fixed 2026-09-05.** This view rendered every subcategory of every category at
every depth. nop's own `CategoryNavigation` renders a sublist only for the selected category and the branch
leading to it (`Views/Shared/Components/CategoryNavigation/Default.cshtml:44-45`). Ported, including the
`active` / `last` class names so the theme styles our tree as it styles its own. On the measured data that is
**117 of 365 category lines** gone.

**Breadth — measured, then left alone on the desktop.** Across **94 live stores** a customer sees a median of
**5** root categories, an upper quartile of **9**, and at most **30**. A 30-item tree is roughly 700px, and
that is free: the side column is a flex sibling of the product list, which is fifteen products tall and has
nothing else in it. A cap there buys a scrollbar and a truncated label in exchange for empty space, which is
what the first attempt did and what the screenshot of 2026-09-05 killed.

**Capped only below 900px**, where the layout stacks and the tree's height really is paid for out of the
product list's.

**The premise of this item had already expired.** "Pushes the first product below the fold" was written on
2026-09-01, when `.bulk-order-main` still had `flex-basis: auto` and the product column **wrapped below the
tree**. That was the wrap defect, fixed during P3 — and in a two-column layout a tall tree cannot push
products anywhere, because they sit beside it. The item was never re-read after its cause was removed, so it
survived as a complaint about a layout that no longer existed. What was left of it, and was real, is the depth
divergence above.

**The dropdown option is dropped.** It was the last resort for a 248-item tree that turned out not to exist on
any screen.

## I13 — a page-wide add says how many failed, never which (raised 2026-09-05, from I11)

`Add everything on this page` submits every typed box on the page. Its status pill sits at the **bottom**,
beside the button, and reads *"{0} added to your cart. The rest are marked with the reason."* The reasons are
now per-card lists scattered up the page — better than the marks they replaced, and still no way to find them
without scrolling the whole list.

Not a regression: the marks were scattered before too. I11 makes it visible by giving each card something
worth going to.

What it needs is an index, not a fourth copy of the message: the pill naming the products that failed, or a
link that scrolls to the first one. The per-line result set already carries `ProductId` for every refusal, so
the client has everything it needs — this is presentation only.

## I14 — the screen on a phone has never been designed — DONE 2026-09-09

**Four narrow-viewport findings in two days**, each found by looking rather than by planning:

1. `bulk-order.css` had **no `@media` block at all** — the responsive rules were specified in the P9 layout
   change and lost when Revisions 2 and 3 replaced parts of a file Revision 1 had given whole. A 375px
   viewport rendered a 220px category tree beside ~130px of product grid.
2. The theme **hides the category block entirely below 1000px**, assuming its own header menu replaces it.
   Nothing replaced it here, so category navigation vanished.
3. Between 900 and 1000px the tree was hidden while the layout was still two columns — a 220px empty gutter.
4. **Reported 2026-09-05: on a phone the quantity boxes and the Add to cart button cannot be found.**

The first three are fixed. The fourth is not, and it should not be fixed the way the first three were.

**Why this is one item and not a fourth patch.** Three reactive CSS fixes written from three screenshots is
precisely the shape of work this project has said it does not want. And the fourth is not a CSS oversight: a
matrix of quantity boxes on a 375px screen is a genuine design problem. The card spends ~124px on the image
before the grid starts, `.grid-scroll` then scrolls horizontally inside what is left, and a horizontal scroll
inside a vertically scrolling page has no visible affordance on a touch device. That is not a rule to add —
it is a question about what this card should BE at that width.

**The shape of the answer, to be decided rather than assumed:** the image is the first thing that should go
(it is the least useful pixel-per-inch on the card); the grid then gets the full width with its row-label
column already sticky (§4.2); and the add-to-cart cluster has to sit where a thumb reaches it rather than at
the end of a horizontal scroll. Whether the grid becomes something other than a table below some width is the
real question, and §4.2's rule stands either way — a matrix must not wrap, because a box that is not under
its column header means nothing.

**Sequence.** After I7, I8 and I13. Designed as one piece, against a real phone, not from a desktop window
narrowed with devtools — the scroll affordance is precisely what those two do differently.

### What the 2026-09-08 pass settled, and what it did not

Measured at an emulated 375px viewport, page 3 of the SKU Z→A list.

**The card no longer overflows the phone.** Before: the document was **1458px** wide at a 375px viewport with
**321** of our elements off-screen and the Add to cart button's right edge at 1458. After: **375px**, **zero**
of our elements off-screen, the button on screen.

**The cause was not the image, and not a missing rule.** `.bulk-order-layout` is `flex-wrap: wrap` and becomes
`flex-direction: column` below 1000px, and *a wrapping column flex container sizes its items to content rather
than stretching them*. `.bulk-order-main` therefore grew to 1439px, `.product-body` never had to shrink, and
`.grid-scroll`'s `overflow-x: auto` had nothing to clip — `scrollWidth == clientWidth` on every card, so the
scroller that was supposed to absorb a wide grid had never once engaged. The fix is `flex-wrap: nowrap;
align-items: stretch` in the narrow block plus `min-width: 0` on `.bulk-order-main, .bulk-order-grid`
(flex items refuse to shrink below content without it). `.product-image` is then hidden below 600px, which is
the one part of this the item had already predicted.

**The open question about wide grids is answered: a table does not compress here.** `ZZT-20` (400 cells) hands
its overflow to `.grid-scroll` — 576px of table in a 338px scroller — and its cells stay at a readable 100px.
Same for ZZT-19 (1110px), ZZT-16, ZZT-15, ZZT-14, ZZT-13. **No `min-width` on `.grid-table` is needed**, which
was the contingency held open going in.

**The sticky row label was broken, and had been on the desktop too.** §4.2 calls the row-label column sticky;
the CSS pinned **every** label column to `left: 0`, so a product with more than one row axis stacked them all
at the same edge and the last in source order won. On ZZT-20 at 375px, Color and Sleeve showed as slivers
under Fit and the first quantity column slid underneath the pile. Not phone-only: ZZT-20, ZZT-16 and ZZT-15
all scroll at desktop width and all three overlapped. Two smaller faults came with it — the second header
row's spacers are `td.row-axis` while the rule said `th.row-axis`, so they were neither sticky nor opaque and
the size headers showed through them; and `box-shadow` is not painted on a table cell at all while
`border-collapse: collapse` is in effect, so the intended divider was inert.

**What the fix is, and why it is not CSS.** `left` has to be the summed width of the columns to a cell's left,
and those widths come from the attribute values, so only measurement knows them: `pinRowLabels` in
`bulk-order.js` measures the header row once and writes an offset per column. Sticky positioning does not
change an element's in-flow size, so measuring and pinning in one pass cannot feed back into the widths it
measured. Two rules follow from measuring rather than guessing:

- **Not every label column is pinned.** Three label columns are 232px of a 338px card; pinning all three
  leaves 106px to scroll in, which is no longer a table. Columns are pinned from the left while the block
  stays under **half** the visible width, and the first is always pinned however wide it is — a quantity box
  whose row you cannot name is worth nothing. At 375px ZZT-20 pins two of three; at 1440px it pins all three.
- **Nothing is pinned unless the grid actually scrolls.** At 1440px, twelve of the thirteen grids on a page
  fit their card. Pinning them bought a dozen sticky layers and a set of rules drawn over borders already
  there, to hold columns that never move.

The block's two rules — the seam between pinned columns, and its right edge — are pseudo-elements on the cells
themselves. A collapsed border belongs to one cell, so between two sticky cells at fractional offsets there is
a hairline neither of them paints, and the scrolling columns showed through it down every row.

**Verified on all four paths that touch it**, not just first render: initial page, pin change (`reloadGrid`),
paging (`load`), and resize. Resize is the one that matters most and it was checked in both directions —
375 → 1440 re-pinned ZZT-11 from two columns to three, and every card that stopped scrolling dropped to zero
pinned cells.

**Still open in I14.** Three things, all of which need a real device rather than an emulated viewport:

1. **The pinned strip at narrow widths.** It is 338px wide and 99px tall on a phone, above every grid, and it
   has not been designed for that width — only measured at it.
2. **The add-to-cart cluster's placement for thumb reach.** The button is on screen now. Whether it is where a
   thumb reaches is a different question, and the item said so before this pass.
3. **The scroll affordance on a touch device.** This is the finding the item was created for and it is the one
   an emulator cannot answer: overlay scrollbars behave differently under a finger, and the shaded right edge
   of the pinned block is now the only thing on the card that says the grid continues. Whether that reads as
   "there is more" to somebody who has not been told is a question for a person holding a phone.

### Closed 2026-09-09 — all three settled, on a real handset

**The two open design items were built on 09-08 and the run was executed on 09-09 against a physical Android
phone.** Twenty-one checks, all passed, including the two that were deliberately written as judgement calls
rather than pass/fail — B1, whether the shaded edge of the pinned block reads as "there is more" to somebody
who was not told, and B5, whether ZZT-19's 1110px table inside a 338px card feels navigable rather than
endless. Those two were the only ones that could have sent the design back, and they did not. **No affordance
beyond the shaded edge is needed**, which is worth recording as a decision rather than an absence: it was
tested, not skipped.

**What shipped for the two remaining items.**

- *The pinned strip at narrow widths.* Measurement first, and it moved the problem: at 375px the strip has
  ALREADY collapsed to one pin per row, because the pins do not fit side by side — ZZT-11's three measured
  103, 251 and 188px in a 318px box. So it was never a wrapping problem. `flex: 0 1 auto` sizes each pin to
  its content and I15's `align-items: stretch` equalises heights only *within* a row, which is nothing once
  every row holds one item; three stacked boxes at three different widths is what read as messy. Below 600px
  each pin takes the full row (`flex: 1 1 100%` and `max-width: none`, which has to go with it — 24rem is
  narrower than the card here). Measured after: every pin 317px, both edges aligned.
- *The add-to-cart cluster for thumb reach.* 151px inside a 338px card is a target that must be aimed at, and
  the page-wide button sits at the foot of an 18,500px document where aiming is worst. Both go full width
  below 600px. The **height is deliberately unchanged** at 43px: that is the theme's own add-to-cart height,
  chosen on purpose, and one pixel under the 44px guideline is not what made it hard to hit.
- *A defect found on the way, present on the desktop too.* §4.2 calls the row-label column sticky; the CSS
  pinned **every** label column to `left: 0`, so a product with more than one row axis stacked them at the
  same edge and the last in source order won — on ZZT-20 at 375px, Color and Sleeve showed as slivers under
  Fit and the first quantity column slid under the pile. ZZT-20, ZZT-16 and ZZT-15 all scroll at desktop
  width and all three overlapped. `left` has to be the summed width of the columns to a cell's left and those
  widths come from the attribute values, so `pinRowLabels` in `bulk-order.js` measures and writes an offset
  per column. Two rules came out of measuring: columns are pinned only while the block stays under **half**
  the visible width (three label columns are 232px of a 338px card, and pinning all three leaves 106px to
  scroll in, which is no longer a table), and **nothing is pinned unless the grid actually scrolls** — at
  1440px, twelve of thirteen grids on a page fit their card.

**And the picture came back.** The 09-08 pass hid `.product-image` below 600px. That was the lazy answer, and
the client said so: an image stacked above the details costs vertical space only. The reason it could not
merely *shrink* is the one worth keeping — `.product-grid` lives INSIDE `.product-body`, so an image beside
the body narrows the matrix itself, not just the name; measured at 88px, the grid fell from 338 to 235px and
ZZT-20's row labels alone are 232px. But stacking it above *everything* is also not the answer. Measured over
15 products with real 300×300 images:

| layout | image | page height |
|---|---|---|
| hidden | — | 9,299 |
| **beside the name, grid full width below** | **110px** | **10,463** |
| beside the name, grid full width below | 88px | 10,133 |
| stacked above the details | 96px | 11,000 |
| stacked above the details | 160px | 11,960 |
| stacked above the details | natural | 14,060 |

Stacking above everything is **both taller and smaller** — 96px stacked costs more page than 110px beside the
name — because a stacked image adds its full height to every card while a row-mate only costs the difference
against text that was going to be there anyway. So the image shares the top row with the name and price, and
the grid takes its own row at the full 338px. `display: contents` on `.product-body` is what allows it with no
markup change: the body's box disappears, `.product-info` and `.product-grid` become children of the card, and
the card places all three. Its `min-width: 0` disappears with it, which is why both children carry their own.

**A note the code carries and should not**: the comment on `.bulk-order-pins` says the theme sets
`text-align: center` on `.page`. True at phone widths, misleading about the rest — the theme centres `.page`
only on narrow viewports, which is why the desktop never looked wrong and why the fix is correctly scoped to
the phone block. Worth two words next time that file is open.

## I15 — desktop density and alignment (raised 2026-09-05)

Raised after the P9 run, looking at the finished screen: **things are not aligned, some elements look wrong,
and the grids should fit more data than they do.** Deliberately logged as one item rather than a list of
tweaks, and deliberately paired with I14 — the phone and the desktop are the same card at two widths, and the
last four narrow-viewport findings all came from designing one width at a time.

What is known going in, so the pass starts from evidence rather than from scratch:

- **The card's proportions have never been chosen.** `--imageSize` defaults to 100px and the image column is
  `flex: 0 0 var(--imageSize)`; everything else takes what is left. Nobody has asked what share of the row the
  grid should get.
- **The grid's own metrics are inherited, not decided.** `.grid-table th, td` is `.35rem .5rem`, the quantity
  box is `3.5rem` wide and now 36px tall to match the theme's inputs (P9 I11) — which made every row taller.
  That is the first thing to look at for "fit more data".
- **A cell can carry four things** — box, price, stock note and, since I11, a red border — stacked. §10.3's
  price collapse already suppresses a price column that says the same number 400 times; the same question has
  not been asked of the stock note.
- **The measured shape to design against**: a customer sees 27–222 products, a median of about 3 pages, and
  3–12 root categories. This is a small-catalogue screen, not a 4,000-product one.

**Constraint that does not move:** anything touching type, colour or spacing outside `.bulk-order-*` is out of
scope — the next theme change undoes it. And design §4.2's rule stands: a matrix must never wrap, because a
box that is not under its column header means nothing.

## I16 — a cell can say "500 in stock" and refuse 3 (raised 2026-09-07, from P6b's G1)

On `ZZT-56` the *With badge* row carries `max="2"` — the bundled product's stock — while every cell in it
prints **"500 in stock"**, the parent's. Both numbers are correct and they are about different products.

**Not a parity break, which is why it is here and not a bug.** nop's own product page shows the parent's
stock too and surfaces the bundle limit only as a cart warning, so changing what the cell *validates* would
make us stricter than the store. What is ours to fix is the card telling the customer two things at once.

Options when this is picked up: suppress the stock line on a row whose cap comes from a bundle value; or say
whose stock it is ("2 available with badge"); or leave the stock line and lean on the over-max message, which
already names 2 the moment a quantity is typed. Decide with I15, since it is the same pass over cell density.

## I17 — the theme's notification bar makes every page scroll sideways on a phone (found 2026-09-08, during I14)

Found while measuring, not while looking for it. At a 375px viewport the document came out **456px** wide on
page 4 of the bulk list. Exactly **one** element overflows, and it is not ours:

- `#bar-notification`, `Presentation/Nop.Web/Views/Shared/_Notifications.cshtml:79`
- sized by `Themes/CW.Standard/Content/css/styles.css:614` (and identically in `DefaultClean`)
- fixed at 456px, on **every page of the store**, not only this one

So a phone gets a horizontal scroll on the whole site, and on our screen it undoes what I14 just fixed: the
card measures 375px and the page still slides.

**Not ours to fix, and that is the point of writing it down.** It is nop's own file, behind the cardinal rule.
An override from `bulk-order.css` would reach it — the selector is global — but that changes a store-wide
element on every page from a stylesheet loaded only on ours, which is worse than the bug: the bar would be one
width on the bulk screen and another everywhere else, and nothing would say why.

**What to do with it.** It belongs to the theme, so it goes to whoever owns `CW.Standard` — the fix is a
`max-width: 100%` on `.bar-notification-container` in the theme's own stylesheet, not here. Recorded so that
the next person measuring this screen on a phone does not spend an afternoon looking for it in our CSS, and so
that I14's "the document is 375px wide" is not read as a claim that the page does not scroll sideways. It
does, for a reason that is not ours.

## Testing this screen on a real phone (recorded 2026-09-09)

Needed again for any future narrow-width work, and it took two attempts to get right, so it is written down.

The site is bound to `localhost`, so nothing on the network can reach it. The route is Android's own ADB over
a USB cable, which tunnels a port from the phone to the desktop:

```
.\adb reverse tcp:65254 tcp:65254
```

`https://localhost:65254` on the phone is then the desktop's instance. The certificate warning is expected —
the ASP.NET development certificate is trusted on the desktop and nowhere else — and you tap through it with
*Advanced → Proceed*, or by typing `thisisunsafe` on the warning page when Chrome hides the link.

**Use Google's platform-tools, not Chrome's `chrome://inspect` port-forwarding UI.** Chrome ships its own
cut-down ADB; on this hardware it enumerated the phone, reported *Offline — pending authentication*, and never
produced the authorisation prompt, through a revoke, a debugging toggle and several replugs. The real `adb`
authorised on the first try. `adb reverse` is what Chrome calls underneath anyway, so the UI is not needed at
all once platform-tools is on the machine.

One thing to know before judging anything on a phone: the page slides sideways by about 80px and it is not
ours — see **I17**.

---

## What this phase must not become

A restyle of the nop theme. The bulk screen sits inside whatever theme the store runs, and its CSS is
loaded per-page (`NOP46` D13). Anything here that changes type, colour or spacing outside
`.bulk-order-*` is out of scope, or the next theme change silently undoes it.
