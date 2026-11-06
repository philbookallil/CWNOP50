# Live Preview — Phase 3 Spec: live Cart & Checkout via a per-store preview customer

**Status:** **Tasks 1–5 built and committed (176d965, 2026-09-23). Code-review fixes (2026-10-05) tested 2026-10-08 —
all §9.1 checks pass — and committed (5790df8, 2026-10-08).** **Owner:** CW.ThemeCore only (no CW.Core change — see §6). **Prereq:** Phase 1 live preview working (iframe + token + framing middleware + bridge), Phase 2 click-to-edit built.

> **Build log (2026-09-18) — compiles clean (0 errors):**
> - ✅ **T1/T2 Preview-customer service** — `Services/Preview/{ICwPreviewCustomerService, CwPreviewCustomerService}` (registered in `NopStartup`). `GetOrCreatePreviewCustomerAsync(storeId)`: resolves by `SystemName "CwThemePreview-{storeId}"` or creates a hidden `IsSystemAccount` customer (Registered role, dummy email), seeds a dummy address (first published country → complete checkout address) and an **auto-picked dummy cart** (first 3 `visibleIndividuallyOnly` products; re-seeds only if empty).
> - ✅ **T3 Impersonation + GET-only guard** — `CwPreviewFramingMiddleware`: a validated preview request that is **not GET → 405** (short-circuit, no mutation/orders); on cart/checkout paths (`IsCartOrCheckout`, allows `/{lang}`) → `IWorkContext.SetCurrentCustomerAsync(previewCustomer)` + stash id in `Items[PreviewCustomerItemKey]` (filter fallback).
> - ✅ **T4 Admin wiring** — `PrepareLivePreviewAsync` adds `urls["cart"]=/cart` and `urls["checkout"]=/onepagecheckout` (with token) → both views flip mock→live automatically. Region map gained `.estimate-shipping-button, .checkout-button` for click-to-edit.
> - ✅ **T5 Hide from list (revised 2026-09-21 — no CW.Core change).** First attempt filtered `!c.IsSystemAccount` inside CW.Core's shared `GetAllCustomersDataAsync` — **rejected** (silently changes a general-purpose data method for every caller). **Final approach:** give the preview customer the **Guests** role (not Registered) — exactly like nop's own SearchEngine/BackgroundTask system accounts, so it falls outside the admin customer list's **default Registered-role filter** with **zero customer-list code**. CW.Core edit reverted. Existing preview customer (Id 100) role swapped Registered→Guests in the DB. *(Originally this relied on `OrderSettings.AnonymousCheckoutAllowed = true` to get past checkout's guest gate; superseded by T5b, which makes the preview independent of that setting.)*
> - ✅ **T5b Checkout-settings independence — `CwPreviewCheckoutFilter` (2026-09-21).** So the checkout preview never depends on store settings, a global `IAsyncActionFilter` (ThemeCore, registered in `NopStartup`) runs **only** for a validated preview request on `CheckoutController.OnePageCheckout`. If the real action produced a `ViewResult` it does nothing; otherwise it **bounced** — for **any** reason (`CheckoutDisabled` → cart, `!OnePageCheckoutEnabled` → standard checkout, `IsGuest && !AnonymousCheckoutAllowed` → login) — and the filter swaps in the checkout view built from the preview customer's cart (`PrepareOnePageCheckoutModelAsync` → `ViewResult("OnePageCheckout")`), guarded by a non-empty cart. **No redirect** (token preserved), no core edit, no customer-service override, no CW.Core. Inert for all real traffic. So the checkout preview is robust to `AnonymousCheckoutAllowed`, `OnePageCheckoutEnabled`, and `CheckoutDisabled` all being toggled off. Copies the small OPC render body → **re-diff on nop upgrade** (`Plugins/Nop.Plugin.CW.ThemeCore/CW_ThemeCore_Nop_Upgrade_Checklist.md`). **Note:** the preview always shows the *one-page* checkout view even if the store is configured for multi-page checkout — a deliberate simplification (style preview; multi-page step-page fidelity would be a much larger effort).
>   - ⚠️ **Remaining caveats.** (1) A Guests-role account **still appears if an admin explicitly filters the list by the Guests role** (same as nop's own system accounts); "fully hidden even under a guest filter" would need a list-level change, ruled out. (2) Fidelity: a guest render may differ from a logged-in B2B customer for ACL-restricted catalog/pricing.
> - 🐛 **Fix (2026-09-21): empty preview cart cascaded into a broken checkout.** First test: Cart showed **empty** and Checkout flashed a cart then fell back to the mock. **Root cause = seeding failed:** `AddToCartAsync` with no `attributesXml` silently no-ops for products that require attributes (colour/size sample tees), are call-for-price, buy-button-disabled, or out of stock — so the cart ended up empty. Empty cart then triggered `OnePageCheckout`'s `if (!cart.Any()) return RedirectToRoute(CART)` → a **302 to `/cart` without the token** → that page rendered as the logged-in **admin** (shared-origin dev cookie → the "Administration" bar + admin's own cart) with no bridge → 6 s timeout → mock fallback. **Impersonation itself works** (the empty Cart had *no* admin bar → it was the preview customer). **Fix (v1, insufficient):** keep only products that add with zero warnings — but **the DB showed 0 products addable with a plain add** (all require colour/size). **Fix (v2, 2026-09-21):** `EnsureDummyCartAsync` now builds a **default attributesXml** per product (`BuildDefaultAttributesXmlAsync` — pre-selected/first value for each value-based attribute via `IProductAttributeService` + `IProductAttributeParser`) before `AddToCartAsync`, so attribute-required products (colour/size) add cleanly; still skips anything that warns (free-text/date required, non-buyable). This store has **2** such products (315, 316) → the preview cart shows 2 items. The existing empty preview customer self-heals (re-seeds while empty). **Dev note:** the shared-origin admin cookie leaking into the frame is a dev-only artifact (separate prod domains won't send it); it only surfaced here via the token-less redirect.
> - ⏳ **T6 Deploy + test** — rebuild CW.ThemeCore, restart (clears manifest cache); run §9. **Verify first:** (a) a system-account customer actually renders Cart/Checkout — if any path blocks it, switch to the normal-customer + flag fallback; (b) `SetCurrentCustomerAsync` at middleware Order 550 holds through to the controller — if the cart shows empty/guest, move the impersonation into a resource filter reading `Items[PreviewCustomerItemKey]`.

> **Goal:** the live preview shows the **real Cart and Checkout pages, populated**, for Home/Category/Product's two remaining siblings — so the brand tokens, layout and click-to-edit work there too. We do it by pointing the framed request at a **dedicated dummy "preview customer" per store** that already holds a dummy cart, and letting nop render Cart/Checkout through its **normal pipeline** — no fabricated cart/checkout models, no faked services.

---

## 1. Why this approach (vs. fabricating cart data)

Cart and Checkout are **session-bound**: an anonymous preview request has an empty cart (Cart renders empty; Checkout redirects out). The rejected alternative — building fake cart/checkout view-models per request — means re-implementing a long chain of nop services (shipping, payment, totals, addresses) and is fragile. Instead we reuse the **real render path** with a **real (dummy) customer** that has a **real (dummy) cart + address**. Higher fidelity, less code, and the checkout-dependency risk largely disappears.

**Decisions taken (2026-09-17):** one preview customer **per store**; it must **not appear in the admin customer list**.

---

## 2. The preview customer

- **One per store**, resolved-or-created lazily and idempotently by a new `ICwPreviewCustomerService` (cached). New stores get one on first preview with no migration step.
- **Classified as a system account** — `Customer.IsSystemAccount = true` with a per-store `SystemName` (`CwThemePreview-{storeId}`). This is the correct classification (a non-human, system-managed account) and the basis for hiding it (see §6). No usable password / no login path — it is only ever entered via the preview token, never by signing in.
- **Sandbox only:** **Guests** role (see §6 — this is what keeps it out of the customer list), **no payment method**, a **dummy saved address** (fake name/address — no real PII) set as billing + shipping so Checkout's address steps populate.
- **Dummy cart:** a few active, individually-visible products from *that store's* catalog (same query `PrepareLivePreviewAsync` already uses), added as `ShoppingCartType.ShoppingCart` items for the customer+store. **Re-seeded (idempotently) at the start of each preview session** so it always shows the intended items even if something changed.
- Store-scoped: the cart and address are per (customer, store), so each store's preview shows its own products.

> **De-risk in step 1:** confirm a system-account customer actually renders Cart/Checkout (some nop paths may guard on `IsSystemAccount`). If any path blocks it, fall back to a **normal** customer flagged with a generic attribute `CwPreviewCustomer` — still Guests-only, so §6's hiding holds unchanged. *(Not needed: the system account renders Cart/Checkout fine.)*

---

## 3. Rendering Cart/Checkout as the preview customer

Reuses the Phase-1 token + framing middleware (which already validates the token per request):

- Admin side: `PrepareLivePreviewAsync` gains `urls["cart"]` and `urls["checkout"]` = `{storeOrigin}{cartPath|checkoutPath}?{token}`; the admin adds Cart/Checkout to the **live** views (they leave the mock).
- Framing middleware: for a **validated preview request whose path is Cart or Checkout**, resolve the store's preview customer and call **`IWorkContext.SetCurrentCustomerAsync(previewCustomer)`** — the *same mechanism nop's own admin impersonation uses*. The Cart/Checkout controllers then read that customer's cart and render normally.
  - **Scope:** impersonation applies to **Cart/Checkout only**; Home/Category/Product stay anonymous guest (unchanged). Path-gated (nop routes: `/cart`, `/onepagecheckout` / `/checkout/...`).
  - **Ordering/timing:** the framing middleware runs at Order 550 (after auth 500, before endpoints), so the customer is set before the controller reads it. **Verify** nop's `WorkContext` cache holds through the request; if it re-resolves, move the `SetCurrentCustomerAsync` into a lightweight **resource/action filter** that reads a `HttpContext.Items["CwPreview.CustomerId"]` the middleware stashes (mirrors how it already stashes the admin origin).

---

## 4. Read-only safety (the client's data-safety requirement)

Two layers:

1. **Server — token-bearing requests are GET-only.** A request whose URL carries a valid `?cwpreview=` token and
   is **not GET is rejected** (405, short-circuited in the framing middleware), and on Cart/Checkout it runs as
   the throwaway preview customer. The impersonation lasts for that request only: the middleware discards the
   `.Nop.Customer` cookie `SetCurrentCustomerAsync` writes, so the browser's own identity is never replaced.
2. **Client — the bridge makes the page display-only.** Clicks on links/buttons and `submit` events are
   cancelled in the capture phase, so nothing the viewer clicks navigates, posts or places an order.

Consequence: Checkout previews render the **page as styled** (first step expanded, others collapsed) — the AJAX
step-through is intentionally disabled, because the goal is previewing the *look*, not transacting.
(Interactive step-through would be a separate, larger scope.)

### 4.1 Accepted limitation — sub-requests are guarded by the bridge only (decision 2026-10-05)

The server guard applies only to the token-bearing page load. Anything the previewed page does **afterwards**
— AJAX, form posts, navigation inside the frame — carries no token, so the server treats it as an ordinary
request from whoever the browser is on the store's domain:

- **Store on a different site from the admin:** the browser sends no store cookies from inside the iframe, so
  such requests run as a fresh guest. Harmless.
- **Store on the same site as the admin** (the default store with its own `/admin`, localhost in dev): they run
  as the **admin's own logged-in account**.

What the bridge does not stop, and so can reach the admin's account in the same-site case:
- `change` / `input` events — e.g. nop's header currency / language selector navigates on `change` to a GET
  (`/changecurrency/{id}`) that saves the admin's currency and takes the frame off the preview;
- a store script calling `form.submit()` (no `submit` event fires) or posting over AJAX / `fetch`.

**Why accepted:** it needs the same-site setup *and* an unusual interaction or a self-posting store script; the
effect is limited to the admin's own account (never customer data, never the preview customer); and the
preview is an admin-only tool. Closing it was costed at ~½ day (iframe `sandbox`, server recognising the token
from the `Referer` header with an explicit `/logout` block, bridge blocking `change` events) and deferred.

**Revisit if:** the preview is exposed to non-admin users, a store theme adds scripts that post on load, or a
real incident traces back to it. The deferred design is the one above — start from there.

---

## 5. Click-to-edit on Cart/Checkout (Phase 2 integration)

Cart/Checkout are **core views** (can't be tagged, §2), so their regions come from the theme's **selector→region map** (`regionMap` in `cw-theme.json`) — largely already covered (`.button-1/.button-2` → button, `a` → link, and the already-branded estimate-shipping button / active-step bar). Add any missing cart/checkout selectors (e.g. totals, discount box) as map entries. No core edits.

---

## 6. Hiding the preview customer from the admin customer list

**Mechanism: the Guests role — no customer-list code, no CW.Core change** (decided 2026-09-21).

- The preview customer is a system account (`IsSystemAccount = true`) in the **Guests** role only — exactly like
  nop's own system accounts (SearchEngine, BackgroundTask).
- Admin → Customers opens with nop's default **Registered**-role filter, so a Guests-only account is not listed.
  Nothing in nop or CW.Core is changed to achieve this.
- **Rejected:** filtering `!c.IsSystemAccount` inside CW.Core's `CustomerOverrideService.GetAllCustomersDataAsync`.
  It is a shared, general-purpose data method; the filter would silently change its results for every caller.
  Reverted.
- **Survives nop's guest clean-up.** The "Delete guests" task skips system accounts
  (`CustomerService.DeleteGuestCustomersAsync` filters `!guest.IsSystemAccount`), so the account and its seeded
  cart are not purged.

**Knock-on: checkout's guest gate.** A guest is bounced by `OnePageCheckout` when
`OrderSettings.AnonymousCheckoutAllowed` is off. `CwPreviewCheckoutFilter` (build log T5b) renders the checkout
anyway for preview requests, so the preview does not depend on that setting — nor on `OnePageCheckoutEnabled` or
`CheckoutDisabled`.

**Where it can still appear** (accepted — it is not a person and holds no real data):
- the customer list when an admin filters by the **Guests** role, or clears the role filter — the same as nop's own
  system accounts;
- any other admin screen that lists customers without a role filter.

It places **no orders** (token-bearing preview requests are GET-only, §4), so order and report exposure is nil by
construction.

---

## 7. Out of scope → later
- Interactive, transacting checkout preview (step-through, place a test order). Deliberately excluded — preview is display/style only.
- Non-CW themes (they get the mock as today).

---

## 8. Build steps (in order)
1. **`ICwPreviewCustomerService`** — resolve-or-create the per-store system-account preview customer; seed the dummy address; **verify Cart/Checkout render** as it (else switch to the normal-customer + flag fallback).
2. **Dummy-cart seeding** — pick N visible products for the store; add as cart items; idempotent re-seed at preview-session start.
3. **Impersonation + GET-only guard** in the framing middleware (path-gated to Cart/Checkout; `SetCurrentCustomerAsync`; reject non-GET). Filter fallback if WorkContext timing needs it.
4. **Admin wiring** — Cart/Checkout live URLs in `PrepareLivePreviewAsync`; move those two views from mock → live; regionMap entries for cart/checkout.
5. **Hide from the customer list** — Guests role only (§6); no list code.
6. **Safety + test pass** (below).

---

## 9. Test plan
- Open the live preview → select **Shopping cart**: shows the store's dummy products, brand-tinted, with correct totals; select **Checkout**: renders the styled one-page checkout (first step populated from the dummy address).
- **Brand tokens + layout** apply live on both; **Edit elements** lets you click cart/checkout buttons/totals and restyle them.
- **Safety:** attempt add/remove/checkout-submit in the preview → **blocked** (GET-only); confirm **no order is created**, the dummy cart is unchanged after a session, and **nothing persists** to real data.
- **Per store:** each store's preview shows *its own* products (per-store customer + cart).
- **Customer list:** the preview customer(s) do **not** appear in Admin → Customers.
- **Isolation:** a leaked token can only ever surface the sandbox customer; it can never place an order or mutate a cart.
- Dev at **All-stores / test store** scope (per the Phase-1 shared-`Store.Url` note).

### 9.1 Review-fix checks (code review 2026-10-05) — all passed 2026-10-08

Run at the test store's scope with DevTools open on Console and Network. The Network filter
`/RefreshPreviewToken|cwpreview=/` hides the Browser Link and analytics calls made from inside the frame (they carry
the frame URL encoded, so the literal `=` does not match them).

```sql
-- preview customers, and every cart holding a preview-cart product
SELECT c.Id, c.SystemName, c.CustomerGuid FROM Customer c WHERE c.SystemName LIKE 'CwThemePreview-%';
SELECT s.CustomerId, s.StoreId, s.ProductId, s.CreatedOnUtc FROM ShoppingCartItem s
WHERE s.ProductId IN (SELECT ProductId FROM ShoppingCartItem x JOIN Customer c ON c.Id = x.CustomerId
                      WHERE c.SystemName LIKE 'CwThemePreview-%');
```

| # | Fix | Do | Pass if | 2026-10-08 |
|---|---|---|---|---|
| R1 | Customer-cookie leak (middleware discards the `.Nop.Customer` cookie `SetCurrentCustomerAsync` writes) | Preview **Shopping cart**, then **Checkout**; inspect the framed `/cart?cwpreview=…` and `/onepagecheckout?cwpreview=…` responses | No `.Nop.Customer` cookie carrying the **preview customer's** GUID. One carrying the signed-in admin's own GUID is normal — nop re-issues it on every request, before the middleware runs | ✅ both carried the admin's GUID only |
| R1b | | Look at your storefront cart signed in, signed out, and signed in again | Never holds the dummy products; empty as a guest | ✅ admin cart empty in the DB. (A 316 line dated 2026-09-21, likely the pre-fix leak, was removed first) |
| R2 | Dummy cart re-checked and re-seeded (`IsCartStillValidAsync`) | Unpublish one seeded product, preview **Shopping cart**; republish it, preview again | No cart warnings; the cart is cleared and re-seeded with what adds cleanly. After republishing it stays as re-seeded — a valid cart is never topped up | ✅ 315 unpublished → re-seeded with 316 only (the sole sellable product left); unchanged after republishing |
| R3 | Per-store lock on first use (`ILocker`) | Switch quickly between Cart and Checkout | No errors; exactly one `CwThemePreview-{storeId}` customer | ✅ one customer, no new accounts, no log errors. Proves only that the lock does not get in the way: the race is on **first creation**, and store 4's customer already existed |
| R4 | Checkout filter keyed on the impersonation flag (`PreviewCustomerItemKey`) | Preview **Checkout**; separately, go through your real checkout as yourself (stop before placing) | Preview: populated one-page checkout. Real checkout: your own cart and the store's gates | ✅ admin cart held only its own test product; preview cart untouched; no orders. (Optional anonymous-checkout-off guest check not run) |
| R5 | Token renewal (`RefreshPreviewToken`) | Temporarily set `PreviewTokenLifetime` to 6 min (refresh due after 1 min) and rebuild; switch views before and after the 1-minute mark, then again at once. **Restore to 60** | Before: no refresh. After: one `RefreshPreviewToken` POST returning a token, and the view loads live. Straight after: no second POST | ✅ all three |
| R6 | Config div (CLAUDE.md §9.14), theme/manifest loaded once | Generate, a vibe chip, Surprise me, Reset, a layout card, Edit elements + popover AI on a live view | Everything works; no Console errors | ✅ |

Fixes with nothing to test: stale comments, and `CW_ThemeCore_Nop_Upgrade_Checklist.md`. Finding #3 (token-less
sub-requests) was accepted, not fixed — see §4.1.

---

## 10. Effort
≈ **4–5.5 dev-days**: preview-customer service + seeding (~1–1.5 d), impersonation + GET-only guard (~1.5–2 d), admin wiring + regionMap (~0.5–1 d), hide-from-list + safety/testing (~1 d). Lower-risk than fabricating cart/checkout data, and it removes the checkout-dependency uncertainty.

## 11. Decisions (resolved 2026-09-18)
- **Customer role:** **Guests** (revised 2026-09-21) — matches nop's own system accounts and hides the customer from the list's default Registered view with no CW.Core change; checkout's guest gate is handled by `CwPreviewCheckoutFilter` (T5b), so no store setting is relied on. *(Originally Registered; changed to keep CW.Core untouched per the customer-list-hiding discussion.)*
- **Dummy-cart contents:** **auto-pick** the first N individually-visible products from the store's catalog (self-maintaining; each store shows its own).
- **Exclusion scope:** **customer list only** for now — it places no orders (GET-only) and has no real PII, so reports/GDPR need no special handling yet.
