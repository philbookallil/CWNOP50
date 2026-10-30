# Lessons — 4.9 history

The lessons learned on nopCommerce 4.9 (`CLAUDE.md` §0.7). This file is history: new lessons go in `tasks/lessons.md`.
Cite these as "4.9 #N".

**Brought across from 4.9 on 2026-10-22.** Lessons #1–#126 come from the 4.90 source's `tasks/lessons.md` (`CLAUDE.md`
§3.1) with their numbers unchanged, because the 4.9 `CWPlans/` documents and the code reviews cite them by number. Their
text is as written for 4.9. Where 5.00 changes a fact the lesson rests on (a path, a line, a type, a section number), a
**5.00:** note at the end says what holds now. A lesson with no note rests on nothing 5.00 changed. Table rows are in
number order (the 4.9 file had #3 and #4 out of place). The ones `tasks/lessons.md` carried over in short form are 4.9
#2, #4, #5, #73, #74, #75, #77, #103 and #104.

**Shared file.** This file is committed: it keeps the lessons that matter to every developer, the traps of nop and the
stack and the discipline of verifying work. Lessons about how one developer wants to work with Claude (how changes are
handed over, their own browser session, their own folders) live in that developer's `CLAUDE.local.md` (`CLAUDE.md`
§0.10), with their numbers kept. A gap in the numbering is one of those. Write a new lesson here only if it would hold
for any developer; otherwise it goes to your local file.

| # | Mistake | Rule |
|---|---|---|
| 1 | Emitted a literal `<link rel="stylesheet">` from a view rendered into the `PublicWidgetZones.HeadHtmlTag` widget zone; it silently vanished from the page. Cause: nop/WebOptimizer's `<link>` tag helper intercepts literal stylesheet links and routes them into `NopHtml`'s CSS collection, which `_Root.Head` already emitted via `GenerateCssFiles()` *before* the `HeadHtmlTag` widget zone runs — so the tag is registered too late and never output. | In widget-zone / late-rendered plugin views, emit `<link>`/`<script>` tags via `@Html.Raw("...")` (or register through `NopHtml.AppendCssFileParts` earlier in the pipeline). Html.Raw output bypasses tag-helper processing and is written literally. Verify head injection by inspecting the rendered HTML, not just the component/model. **5.00:** Still true in 5.00: `Views/Shared/_Root.Head.cshtml` calls `NopHtml.GenerateCssFiles()` (line 68) before the `HeadHtmlTag` widget zone (line 70). |
| 2 | Versioned a core view into `Themes/TIG.Standard` and it threw `CS0103: 'OfficialSite' does not exist` at runtime, taking the page down. Cause: the core view relied on an identifier exposed by the **core** `Views/_ViewImports.cshtml` (`@using static Nop.Services.Common.NopLinksDefaults`), but the theme's `_ViewImports` has a smaller curated using-set and doesn't include it. Theme `_ViewImports` does **not** inherit the core one. | When porting a core view into the theme, the theme `_ViewImports` is the *only* imports in scope — core's are not inherited. Scan the source view for identifiers resolved via core `_ViewImports` `@using` / `@using static` (e.g. `OfficialSite`, model namespaces) and add the missing `@using` to the themed view (or the theme `_ViewImports`). Theme views are runtime-compiled, so such errors only surface on first request — load the page after each port. **5.00:** Still true in 5.00: the core `Views/_ViewImports.cshtml` has `@using static Nop.Services.Common.NopLinksDefaults` (line 16). The theme plugins are not ported yet. |
| 3 | Used the `Edit` tool directly on `CreditNotesService.cs` (a real plugin source file) mid-session, breaking the "give me the code only, I'll apply it" policy that had been the running agreement for all C# source changes this session. Caught by re-reading my own output before responding; reverted via three follow-up `Edit` calls back to the original content. | Before calling `Edit`/`Write` on anything under `Plugins/`, `Libraries/`, `Presentation/`, or `Tests/` (compiled source), pause and check whether this session's standing code-touch answer is "apply directly" or "code only." `.md` files under `CWPlans/`/`tasks/` and `CLAUDE.md` are documentation, not source — direct edits to those don't imply the same permission for `.cs`/`.cshtml` files. **5.00:** In this repository the code folders are `Plugins/`, `CWLibraries/`, `Libraries/`, `Presentation/` and `Tests/`. The documents are `CLAUDE.md`, `tasks/`, `CWPlans/` and each plugin's `References/` folder. The 4.90 source is reference only. `Tests/CW.Tests` is the one code folder Claude may edit directly, and only when the user has allowed it. |
| 4 | Placed `#region Utilities` *after* the public method regions in `ReturnCaseService`. The user corrected it — utilities go before. The existing services already do this (`ExchangeService` has `#region Utilities` immediately after `#region Ctor`), so this was a deviation from actual practice, not a new preference. `CLAUDE.md` §7 was the misleading source: it listed "Fields / Ctor / Methods / Utilities". | Region order in services is **Fields → Ctor → Utilities → public method regions**. Protected/private helpers come first so a reader meets them before the methods that use them. `CLAUDE.md` §7 corrected to match. |
| 5 | Ported `Html.GetQueryStringValue("returnRequests")` straight out of a 4.1 Razor view into a 4.9 one. The helper does not exist in 4.9, so the build passed and the page died with a Razor compilation error on first load. Same class of mistake as `Html.AddTIGSharedCSS` and `_TIG.common.createModal`, which were correctly spotted only because they were obviously TIG-specific. | When porting Razor from 4.1, **verify every framework helper still exists in 4.9 before reusing it** — `Html.*` extensions are the trap, because they look like core API. Razor compiles on first request, so a clean build proves nothing. nop 4.9 reads the query string as `Context.Request.Query["key"]` (see `Views/Customer/ChangePassword.cshtml`). **5.00:** The rule is about the version being ported **to**. 4.1 → 4.9 then, 4.9 → 5.00 now. 5.00 still reads the query string as `Context.Request.Query["key"]` (`Views/Customer/ChangePassword.cshtml:10`). The types 5.00 moved are listed in `CLAUDE.md` §10.5 (e.g. `IWebHelper` → `Nop.Services.Helpers`, `tasks/lessons.md` #3), and they fail a view the same way. |
| 6 | Emitted JSON into an HTML attribute as `data-currentattrs="@Html.Raw(Json.Serialize(map))"`. `Html.Raw` suppresses encoding, so the JSON's first `"` terminated the attribute. jQuery `.data()` then returned the string `"{"` — truthy, so the null-guard passed, and every lookup returned `undefined`, silently disabling the "nothing changed" check. No error anywhere; it just always took the wrong branch. | Never wrap JSON in `Html.Raw` when it lands in an **HTML attribute** — attribute context needs `"` encoded as `&quot;`, which is exactly what Razor does by default. Serialize with `System.Text.Json.JsonSerializer.Serialize(...)` into a `string` local and emit `@local` with no helper. (`Html.Raw` is correct for JSON inside a `<script>` block, which is the opposite context — hence the confusion.) Verify by inspecting the rendered attribute, not the Razor source. |
| 7 | Wrote code against `ProductAttributeValue.PictureId`, obsolete since nop 4.70 — attribute-value pictures moved to the `ProductAttributeValuePicture` mapping entity. It would have compiled (obsolete is a warning, not an error), run without exception, and silently returned null on any store created since 4.70, so the feature would have looked simply broken. Missed because the property is `[EditorBrowsable(Never)]` — invisible in IntelliSense, so there was no prompt to question it. Compounded by a same-named property on the **view model** (`ProductAttributeValueModel.PictureId`) that is current and correct. | When a nop entity property looks like it should carry data but the feature "just doesn't work", check for `[Obsolete]` on the property — nop keeps obsolete fields for the upgrade path and they return null on new installs. **Entity and view-model properties sharing a name may have different lifecycles**; confirm which one the code is holding. Fastest check: find where nop's own factory populates the view model (here `ProductModelFactory` line 1007) and copy that route. **5.00:** In 5.00, `ProductAttributeValue.PictureId` is gone, not just obsolete (only `ImageSquaresPictureId` is left; pictures come through `ProductAttributeValuePicture`). 4.9 code that still read it is a build error now. The `[Obsolete]` rule stands for whatever 5.00 marks obsolete. |
| 8 | Wrote the `CWOrderReturnRequests` view component's markup as two `<div class="card card-default">` blocks with **no wrapping `<div class="card-body">`**, so the `nop-card` on the order edit page had no direct-child `.card-body`. AdminLTE's `CardWidget.expand()` runs `this._parent.addClass('expanding-card').children('.card-body, .card-footer').slideDown(speed, callback)` — `.children()` matched nothing, `slideDown` on an empty set never fires its callback, so `collapsed-card` and `expanding-card` both stuck on the card forever. The CSS rule `.card.collapsed-card .card-body { display: none }` is a **descendant** selector, so it kept hiding the nested grid. The collapse icon still flipped to `fa-minus` (that runs unconditionally), so the panel looked expanded but was permanently empty. No JS error. | Content rendered inside a `nop-card` **must open with `<div class="card-body">` as a direct child** — the tag helper only emits the card shell and header, it does not wrap child content. Every core order partial does this (`_OrderDetails.Info/BillingShipping/Notes/Products.cshtml` all start with `<div class="card-body">`, then nest `card card-default` inside it). This also means a view component that can return `Content(string.Empty)` must not be the sole child of a `nop-card` — gate the card itself instead, or the same stuck-toggle appears for whoever gets the empty result. **5.00:** Still true in 5.00 (admin-lte 3.2.0): `_OrderDetails.Info/BillingShipping/Notes/Products.cshtml` open their content with `<div class="card-body">` (`Info` after its script block). |
| 9 | Added the phase-1 redesign markup (`DispositionReason`, `ReturnCaseNumber`, `CaseLines`) to `_CreateOrUpdateExchange.cshtml` using `Model.X`, but the partial declares `@model ReturnRequestModel` (nop's) and reaches CW members through a defensive `var exchangeRequestModel = Model as CWReturnRequestModel;`. Assumed the surrounding `@if (isExchangeModelData)` guard made it safe. It does not — Razor compiles the **whole file** regardless of which branch runs, so `Model.DispositionReason` is a compile-time CS1061 on the declared model type. 20 errors, all on first request; the build was clean. | A runtime guard (`@if`) never rescues a **compile-time** member lookup. If a partial needs a derived model's members, declare `@model` as the derived type — do not declare the base and cast. Check where the partial is actually invoked first: here `Edit.cshtml` already wrapped the call in `@if (isExchangeModelData)`, so the base-typed `@model` plus in-partial cast was always dead defence. Razor compiles on first request, so `dotnet build` proves nothing — load the page. (Same failure mode as lesson #5.) |
| 10 | Recorded the returned-item subsidy filter (`AllocateGoodsAsync` skipping items with an open return request) as *restored and verified* across two validation passes. It was written correctly — into a branch nothing could reach, because the field it keys on, `ReturnRequestItem.ShoppingCartItemId`, was being written as `0` by the approval code. Exchange fees, `DisableRemoval`, `IsReturnedItem` and the approval rollback were all inert for the same reason, and every one of them looked implemented. | Verifying that a guard **exists** is not verifying that it **fires**. For anything conditional, the check is *"what writes the value this branch reads, and does it write a real one"* — trace the write, not the read. A single query against the column would have caught all five at once. |
| 11 | Found three copies of the subsidy allocation walk by grepping for callers of the methods being deleted, consolidated them, and declared the duplication closed. There were four: the subsidy bar had its own independently-written copy that shared no code, so no caller-grep could ever have reached it. It surfaced only when a tester noticed the bar and the totals disagreed. | Grepping for callers finds copies that **share** code, never copies written independently. To find duplication of a *concept*, sweep by feature — *"what else displays or writes this thing"* — not by symbol. Ask it once per user-visible surface. |
| 12 | Wrote `ShippingStatus.ReadyForPickup` into a spec, along with two rules about when nop sets Delivered and PartiallyShipped. The enum has no such member and both rules were wrong. Cause: that section was written from what I expected nop to do; the rest of the document had been written by reading the 4.9 source, and had no such errors. | When mirroring core behaviour, read the core method — every time, including the parts that feel obvious. A document that is 90% source-derived and 10% recalled is more dangerous than one that is wholly recalled, because the accurate parts make the invented parts look checked. **5.00:** Read the core method in **this** repository, the 5.00 one. A rule mirrored from 4.9 core is a recalled rule here. |
| 13 | Wrote a dedup as a side effect inside a LINQ predicate — `charges.Where(c => c.LineId == line.Id || (c.LineId == null && countedCases.Add(caseId)))` — intending "count this case's case-level charges once across the cart". The predicate runs once **per element**, so it counted *one* case-level charge ever: a case with both a redelivery and a repackaging fee silently lost the second. It shipped into two files, and the test written to catch fee discrepancies (compare the itemised widget against the total) passed, because both readers shared the defect and agreed with each other. | **Never mutate state from inside a `Where`/`Any`/`All` predicate.** Decide first into a local, then filter on the local. Beyond the correctness trap, a lazily-evaluated side effect fires in an order and a count the reader does not control — and re-enumerating the sequence would fire it again. Corollary for tests: two surfaces agreeing proves consistency, not correctness; at least one assertion must check the value against something computed independently. |
| 14 | Built `ReturnCharge` rows out of a summed `Dictionary<FeeType, decimal>`. The dictionary is what the **UI** shows; the charge row is a **record** of one fee, on one line, priced over a quantity, from a rule, with a name. Deriving records from the summed shape threw all four away, and each of the three fix rounds was an attempt to guess one of them back: the ad-hoc fee's name (lost, so it wrote the enum name), the line it belonged to (lost, so it wrote null when there was more than one line), and how to avoid double-counting the nulls (a dedup guard that was itself a bug). | Decide which shape is the **source of truth** before writing either. Records are the source; a display aggregate is a fold of them, never their input. If producing a record needs information the shape in hand has already summed away, that is the signal to move the computation to where the information still exists — not to reconstruct it. Concretely: price per line, fold for display. |
| 15 | Wrote in a code comment that "the redelivery fee is charged once per return, not per item" and stored the charge that way, while computing its amount from the per-item sum. The sentence was true of the **target design** (target design §3, roadmap decision 1) and false of **the code in front of me** — both live fee walks, and 4.1's, price per item. So phase 3a shipped the target's storage shape with the current arithmetic: a charge with no line, which forced a dedup guard into every reader, which was the `countedCases` bug. Three validation rounds went past it because each checked the implementation against that comment. | A comment on a method describes **what this code does**, never what the design intends it to do one day. Where the two differ, say so explicitly and name the phase that closes the gap — an aspirational statement written in the present tense reads as a finding to the next reader, including to me. And when adopting a target design's data shape, adopt its computation in the same change or not at all; half of each is worse than either. |
| 16 | Re-analysing that mistake, I concluded the per-case fee rule was something I had invented, and that freezing it would move the customer's money. Both wrong. The rule was a recorded manager decision sitting in `Exchanges_Redesign_Roadmap.md` and `Exchanges_Target_Design.md`, and the money did not move because the stored case-level amount **was** the per-item sum. I reached both by reasoning from the code shape alone — correcting an error of "I trusted my own comment instead of the code" by trusting only the code, when the decision existed in writing. The proposed fix, pricing strictly per item, would have cemented an overcharge the manager confirmed the next day was wrong. | Source code says what the system does; the decision records say what it is **supposed** to do. A post-mortem needs both, and a proposal that changes behaviour needs the second one specifically — grep `CWPlans/` for the feature before concluding a rule is unfounded. Separately: never assert that an amount changed without computing it both ways on a concrete example. "It would be charged once instead of twice" was a sentence about structure that I let stand as a sentence about arithmetic. |
| 17 | Wrote in phase 3a §16.2 that the customer wizard *"shows the rule, never a computed total"* and marked that surface checked. Two of the three methods on it do show the rule; the third, `PrepareSummaryFeesAsync`, hands unpriced rule rows to two Razor views that price them and quote the customer a total. The row was written after looking at the method whose name matched the claim (`PopulateEstimatedFeesAsync`) and generalising to its neighbour. It survived a post-apply review because the review re-read the same table. | A "checked and correct" row must name the **specific line** that makes it true, not the method that sounds like it should. Where two methods share a prefix, check both and record both — and when the claim is *"this surface computes nothing"*, the evidence is the **view**, because that is where a Razor-side computation lives. Corollary: a validation table is not evidence on a second pass; re-derive it or leave it alone. |
| 18 | Threaded the `pendingItem` overlay through `ExchangeService` so approval pricing saw the administrator's unsaved edits, verified it end to end, and wrote the test (R21) that would catch the overlay being missing. The overlay was present. The **email** was a second reader of the same item, and `MessageTokenProviderOverride` re-read it from the database — so R21's symptom would have appeared with the opposite cause, and the test's own explanation would have sent the next reader to the wrong file. | When fixing "this reader saw stale data", the unit of work is **every reader of that value in that request**, not the one that was reported. Enumerate them by grepping for the *value's loader* (`GetReturnRequestItemAsync`) rather than for the caller you are fixing. And when a test says "if X is wrong, cause Y was not applied", check that Y is the *only* possible cause before writing that sentence — a confident diagnosis in a test plan is a trap when it is wrong. |
| 19 | Enumerated the dispositions a return line can reach from `ReturnLineDisposition` and from the two statuses the controller mapped, and treated the mapping as complete. nop's `ReturnRequestStatus` has seven members; five of them map to nothing, so a line closed as Cancelled stays Pending for ever and its case can never resolve. Nothing logs it, and the phase's own consistency query (R20) cannot see it, because it looks for a resolution row that is never written. | When code maps an external enum into an internal one, enumerate the **source** enum from its own definition and account for every member explicitly — a `_ => null` default is where the missing cases hide. And a consistency check written as *"find rows where A exists but B is wrong"* cannot detect *"A was never created"*; every such query needs its negative twin. |
| 20 | Nearly reported that a fee rule with `UnitsQuantity = 0` would corrupt frozen charge rows, and that the fee-rule validator was missing, on the evidence that `FeeRuleValidator.cs` contains nothing but a commented-out class. The validation exists — in `ExchangesConfigController.ValidateFeeRule`, with `NormalizeUnitsQuantity` storing 1 for fixed-price rules. Caught by grepping for the *model* name rather than for the validator. | An absent validator file is not an absent validation. Before reporting a missing guard, search for the **thing being guarded** (the model, the column, the value) across every layer — validators, controllers, builders, database defaults — not for the file where you expected the guard to live. Same shape as #10: verify the guard *fires*, and verify a missing one is really missing. |
| 21 | Specced moving the approval workflow from the resolver to the controller, to restore 4.1's per-line execution. I carried across 4.1's outer gate — `RequestStatusHasChanged`, in the controller — and quoted it in the spec as *the* execute-once guarantee. 4.1 has a **second** gate inside the logic, `if (newStatus == ReturnRequestStatus.ReturnAuthorized)`, which I read on the same screen and did not carry. The method it guards hardcodes the Return Authorized rule, so at the new call site every status change ran the full approval workflow: rejecting a return raised its credit note and put the replacement in the customer's cart. Its own comment — *"this runs only for a line the administrator accepted"* — had been true of the old call site and was what made the omission invisible on review. | Moving a call site means inheriting **every** precondition the old one satisfied, and those are rarely all in one place: some are guards at the caller, some are invariants the callee documents in a comment and does not enforce. Before moving a call, list what was true at the old site and check each against the new one — a comment beginning *"this only ever runs when…"* is a precondition that just became the caller's job. When the move is a revert to a reference implementation, diff the reference's **whole** control path, not the entry condition. |
| 22 | Validated the revert by diffing the code the spec had changed, twice, and reported the phase clean apart from comments. `UndoReturnRequestApprovalAsync` was never in that diff — it predates the return case entirely — so nobody noticed it resets the nop row and the customer's cart while leaving `ReturnCaseLine.Disposition` on Accepted and the frozen resolution in place. It cannot self-heal either: re-approving is Accepted→Accepted, so the reopen guard skips it, and `TryResolveCaseAsync` skips a case that already has a resolution. An undone approval keeps a record of itself for ever. | When a change introduces a **second store of state** (here: the case, alongside the nop row), the work is not done when every writer of that state is correct — it is done when every path that **resets or reverses** the original state has been made to reset the new one too. Those paths are invisible to a diff-based review because they were written before the new state existed. Enumerate them by asking *"what can undo this?"* — undo actions, cancellations, rollbacks, admin overrides — not by following the change. |
| 23 | Specced P6's add-to-cart against nop's C# and CW's C#, checked both agreed, and shipped two defects that only exist on the wire. nop replaces System.Text.Json with Newtonsoft **and pins `DefaultContractResolver`** (`ServiceCollectionExtensions:327`), so every response is PascalCase while the script read camelCase — a fully successful twelve-line add cleared no box and reported "0 added". And the client refreshed the cart by dispatching `nop.cart.updated`, an event I invented; nop has none, and refreshes by returning the two cart blocks as rendered HTML. Both were invisible to every reading of the code. | A client and a server agreeing in C# proves nothing about **what crosses the wire**. Serializer naming policy, header names, and the framework's own refresh mechanism are wire facts: look them up in the framework's configuration, never infer them from the language or from what the ecosystem usually does. The cheapest check is one real request with the network panel open, before the second feature is built on the first. **5.00:** Unchanged in 5.00: `Nop.Web.Framework/Infrastructure/Extensions/ServiceCollectionExtensions.cs:331`, `AddNewtonsoftJson(... ContractResolver = new DefaultContractResolver())`. More precisely, `DefaultContractResolver` keeps every name **as declared**. A model's C# properties arrive PascalCase, but an anonymous `Json(new { success = false })` arrives lowercase. Read the action to know which one a handler gets. The SSO port hit this (`SSO_Code_Review_2026-10.md` R7), and its admin module reads either casing. |
| 24 | Worked every phase of the bulk-ordering rebuild from `BulkOrdering_NOP46_Reference.md` — an analysis of the 4.6 plugin I had written myself at P0 — instead of from the 4.6 source. Adequate for the grid phases, which it covers in detail. Not for P6: the reference covers the write path in one paragraph, and ten minutes in `lib/public.ajaxbulkcart.js` would have shown that **4.6 refreshed the cart blocks correctly**, so the invented event (#23) was a regression against the system being replaced, not just against nop. The same read found three more things 4.6 did that the port had dropped: the activity-log entry, `DisplayCartAfterAddingProduct`, and the loading spinner. | An analysis document about the old system is an **index**, not a substitute for its source. For each phase, read the old code for the path that phase actually touches — and read it *before* writing the spec, not when validating it. A summary written for one purpose silently omits what mattered for another. |
| 26 | Read four measurements of the client catalogue and used them to decide what to **build**: cancelled the ManageStock test products because no product uses method 1, shrank I7 because no product has two pins, and was ready to close I8 because no product is case 1 or 2. Every one of those is a statement about one client's year-old database, and the user had to point out that a second, larger client is coming whose data nobody has seen. The project's own P0 caveat already said this — "no rule may depend on how a particular client configured their catalogue" — and I had quoted it. | **Measurements set PRIORITY, never SCOPE.** What the data decides is the order the work is done in and which products an acceptance run is aimed at. What gets built is whatever the design specifies, and a shape with zero live instances gets synthetic coverage instead of being dropped — which is what `ZZT-39 … ZZT-43` already did for five unreachable quantity rules. When a measurement returns zero, the sentence to write is "unreachable on this data, covered synthetically, revisit if a client configures it", never "not needed". |
| 27 | Told the user a change was needed to the `AddToCart.SomeFailed` resource because the P9 document said it ends "see the notes below". It does not, and never did; the actual string is "The rest are marked with the reason", which survives the change untouched. I had read `CWCorePlugin.cs` in the same session and still repeated the document. | A working document that **quotes** code is a claim about code, not evidence of it. Before acting on a quoted string, a line number or a signature that appears in a `.md` file, open the file it claims to be quoting — especially when the document is one I wrote. Same failure as #12 and #15, in a new direction: this time the invented part was in the *notes*, and the code was right all along. |
| 28 | Measured the catalogue three times and read the answer as if it described a screen. `BulkOrderAxisAttributeIds` came back NULL — because I queried a 4.1 database that never had it. 248 root categories looked like a 6,000px navigation tree — across 95 stores; one store sees a median of 5. 4,198 products became "280 pages" in two documents and an acceptance sheet — a customer sees 27 to 222. Every one of those numbers was correct SQL and the wrong question, and the third had already been quoted back at the user as a reason one item mattered more than it did. | This is a **multi-store** install: `Store`, `StoreMapping` and per-store `Setting` rows mean a `WHERE Deleted = 0 AND Published = 1` query describes the estate, not the page. Any measurement used to justify an interface decision has to be scoped the way the screen is scoped — per store, and through the same visibility the code applies (here `BulkOrderService.GetVisibleCategoryIdsAsync`, which counts categories holding a *visible product*, not categories). When a count feels large enough to change a design, that is the moment to ask what the number would be for one customer on one store. |
| 29 | Capped the category tree's height because 30 root categories is ~700px and "taller than the first product card". It sits in a flex column beside a product list fifteen items tall, with nothing below it — so the height was free, and the cap bought a scrollbar and a half-cut label in exchange for empty space. Worse, the intake item I was serving said the tree "pushes the first product below the fold", which had been TRUE of the wrapping layout it was written against and impossible since P3 fixed the wrap. I implemented a symptom whose cause had already been removed. | Two rules. Size an element against **what is actually beside and below it on the rendered page**, not against a number in isolation — "700px" means nothing until you know what occupies the other 2,300px. And when an intake item is finally picked up, **re-derive its premise before implementing it**: an item written weeks earlier describes the code as it was, and the fix that closed something else may already have closed this. A backlog entry is a report, not a standing fact. |
| 30 | Fixed "the §12.5 card takes the pinned strip with it" by making the BUILDER carry its pins on that early return, validated it by grepping the builder for `Pins = BuildPins()`, and told the user it was ready to test. The strip still vanished: `BulkOrderModelFactory`'s own §12.5 early return constructs a `BulkOrderGridModel` from three properties and never sets `Pins`, so the value died one layer above the one I fixed. P5 had already recorded this exact shape — "three of the four defects share one shape: a value computed in the service layer and dropped before the view" — and its checks were rewritten to name the RENDERED result for precisely this reason. | Adding a field that has to reach the view means finding every place the **model is CONSTRUCTED**, not every place the value is produced. Grep for `new <Model>` and for the model's type name, not for the property you just added — a construction site that omits it has nothing to match on, which is why it is invisible to a grep of the change. The dual of lesson #18: that one says enumerate every *reader* of a value, this one says enumerate every *writer* of the model. And validate at the surface — the DOM, the response, the cart row — never at the layer you edited. |
| 31 | Wrote a measurement script against the 4.9 schema from memory — `Product_ProductAttribute_Combination`, a table that exists in neither version — and did not say which database it targeted. The client data being measured is a **nopCommerce 4.1** backup; the 4.9 database is the test instance with ZZT data only. Lesson #28 already noted the 4.1 database and I still wrote to the wrong schema. | Every measurement script targets the **4.1 client database** and is written to the 4.1 schema, with the target named in its header; anything needing a 4.9-only table or setting is marked *4.9 test DB only* and separated. Before writing a table name, confirm it in `Libraries/Nop.Data/Mapping/BaseNameCompatibility.cs` (4.9 renames) and in the earlier measurement scripts that already ran on the client (4.1 ground truth) — never from memory. **5.00:** In 5.00 the test database is each developer's local 5.00 database (`CLAUDE.local.md`). The 4.1 client backup is still the ground truth for client data. `Libraries/Nop.Data/Mapping/BaseNameCompatibility.cs` is still the place to confirm a nop table name. |
| 34 | Validated an applied change set by grepping for the text that had to **appear** - every new method, every new attribute - reported it complete, and missed three separate cases where the change had landed as an INSERT beside the old code instead of a REPLACE of it: `data-clamped` still on the container, `Grid.Clamped` still in the resource list, the old line still above the new one. Each of those greps passed. The removed half of the change was never asked about, so nothing could fail. | **Validate a change set by grepping for what must have DISAPPEARED, not only for what must have appeared.** Every replace has two halves and only the first one leaves evidence a positive grep can find; a stale duplicate is invisible to a search for the new text and often still compiles, so it surfaces at runtime rather than at build. Write the negative grep at the same moment as the change itself - the spec should carry the removal as its own line - and run both directions before saying a set is in place. This is lesson #19's negative twin applied to APPLYING changes rather than to testing them. |
| 35 | Wrote ZZT-58 to cover "the optional axis on a RESTRICTED product" and set `ManageInventoryMethodId = 1`, reasoning in the script's own comment that method 2 would make a dash ambiguous between "not a combination" and "out of stock". But nop only consults `AllowAddingOnlyExistingAttributeCombinations` inside `case ManageStockByAttributes` (ShoppingCartService.cs:496-517), so under method 1 the restriction is a dead letter - the product rendered with no dashes at all and proved nothing. I had reasoned carefully about a SECONDARY property of the test data while never checking the PRECONDITION of the mechanism under test. | Test data has a precondition and a payload, and they fail differently: a wrong payload gives a wrong answer, a wrong precondition gives a **confident right-looking answer to a question that was never asked**. Before writing the data, name the exact line of nop that the case must reach and confirm the data satisfies every condition guarding it - the enum case, the flag, the control type. Write that line's file:line into the script's header. And when a script's comment argues AGAINST the setting the feature needs, treat the argument as the warning: it means the design of the data is being driven by a side concern. Answer the side concern with other data (here, stock 100 on every combination), never by disabling the mechanism. |
| 36 | Built ZZT-58 to prove §7.4 renders a dash, and arranged the missing combinations so that the ONLY gap was the whole `White / Slim` row. `BulkOrderGridBuilder.Compose` drops a tuple whose every cell is a dash - "a row of nothing" - so the row vanished and the grid showed no dash anywhere. The data was right, the restriction was live, the code was right, and the observable I was asserting on could not exist in the shape I had built. | A negative test must **leave a survivor**. When the thing under test is how the screen renders an ABSENCE - a dash, an empty state, a disabled control - check what else the code does with a wholly-absent group before choosing the data: pruning, collapsing and "don't render an empty container" rules are common and they erase the very artifact being tested. Make the gap PARTIAL, so the surrounding row or block survives to display it. The tell is an expected result phrased as a complete row or column of the negative state - if every cell in a group is supposed to be the thing you are testing, ask what happens to the group itself. |
| 37 | Wrote an acceptance run whose first setup step was "set the theme to CW.Standard", chosen from the file layout — that theme ships no product template, so it inherits the ordering the run needed. It could not render at all: its header invokes a view component from a second plugin that was deployed but not installed, so following my own instruction took every storefront page down with a 500. I had verified the theme's VIEWS and never that the theme WORKED. | **A prerequisite in a run sheet is a claim about the environment, and needs the same verification as a code anchor.** "Read it, don't recall it" has always applied to code; a setup step is the same kind of assertion and I had been treating it as stage direction. Before telling someone to change their environment, load the state you are asking for and confirm it works — or write the step as the REQUIREMENT ("a theme with no product template of its own") plus known-good examples, so a broken one can be swapped without rewriting the run. |
| 38 | Twice took derived output as direct evidence. A check said "confirm bulk-order.css is in the head"; assets are BUNDLED, so the filename is never in the HTML and the check reported a failure on working code. Then a product's page changed between two probes with only an admin save in between, and I began investigating whether my save had damaged test data — it was a stale cached render catching up, which the test-data file's own header had warned about. | **Before treating rendered output as evidence, know what transforms sit between the source and it — bundling, minification, caching — because each one breaks a different naive assertion.** A check must assert on something those transforms preserve: fetch the bundle and look inside it rather than grepping the page for a filename. And when a before/after comparison changes, suspect the cache before the code — an admin save invalidates caches, so the "change" can be the cache catching up with data that was always that way. |
| 39 | Wrote a rule about WHERE a customer should be sent, then implemented it as a test of HOW the framework had expressed that: `result is RedirectToRouteResult && RouteName == HOMEPAGE`. Correct for the branch I had read, and wrong for the commonest path in the store — logging out lands on the homepage, the login link then carries `returnUrl=/`, and nop returns `RedirectResult("/")` instead. Same destination, different representation, feature silently dead for most customers. The client found it in a debugger in one step. | **When a rule is about an outcome, assert on the resolved outcome, not on which code path produced it.** Reading the source told me there were two branches and I encoded "the default branch" as the condition — but the rule I had actually agreed was "nop had nowhere particular to send them", and two different branches can both mean that. Ask what the value MEANS and test that; a type-and-name check on the producing branch is a proxy, and proxies fail exactly where the framework offers more than one way to say the same thing. Corollary for debugging: when someone is sitting at a breakpoint, one precise request for an observable value beats several rounds of static inference — ask for it. |
| 40 | Told the client to "measure `[ShoppingCartItem]` warm" and to run "STEP 2 -> load -> STEP 3", as though both were common knowledge. They are a procedure from a SQL file I wrote days earlier and had explained once; his reply was "what do you mean, where do I look, console? network?" - he was hunting in the browser for something that only exists in SSMS. | **A procedure I wrote earlier is my context, not the reader's.** Shared history makes a name feel self-explanatory long before it is, and the more fluently I use my own shorthand the more certain I sound about something the reader cannot act on. When re-invoking any earlier artefact - a script, a step name, a bucket, a test id - name the FILE it lives in, say what kind of thing it is and where it runs, in one clause, every time. It costs a line and it is the difference between an instruction and a password. |
| 41 | Read the store's Return Action / Return Status rule tabs with a text/accessibility search, hit the strings "No Return Action rules have been defined yet" and "No Return Status rules have been defined yet", and started writing up a blocking finding — the workflow fails closed with no action rule, so approvals would do nothing. Both grids were in fact fully populated; those strings are **hidden empty-state placeholders** that sit in the DOM permanently and are only revealed if the DataTables AJAX returns nothing. A screenshot of the same tab showed five action rules with their ticks. | **A grid's empty-state text is not evidence the grid is empty.** Any AJAX grid ships its "no data" message in the initial HTML, so a text search finds it whether or not it applies. Before reporting "X is not configured", confirm it on the RENDERED tab — screenshot it, or read the row nodes rather than the container's text. This is lesson #38's rule (derived output is not direct evidence) in its most common concrete form, and the cost is asymmetric: a false "not configured" sends someone to create data that already exists. |
| 42 | Clicked a form's Save/Checkout/Submit button in the Claude browser pane, read the page back, saw the same form, and concluded the submit had been refused — twice (packname checkout, then the return wizard's confirm step). Both had actually succeeded: the order was placed and return #73 was created. The pane serves a **cached render** after a form POST, so `get_page_text` shows the pre-submit page indefinitely. I also lost time on nop's `type="button"` toolbar buttons, whose jQuery handlers do not fire from synthetic clicks. | **In the browser pane, never judge a POST by re-reading the page.** Judge it by the state it should have changed: query the list/grid/endpoint that would now differ (order history, return list, the ledger). For nop admin toolbars, `button[name=save]` is `type="button"` — submit with `form.requestSubmit(btn)` when the button is a real submit, or POST the serialised form; then verify against state, not against the page. **5.00:** In 5.00 every `name="save"` button in nop's own admin views is `type="submit"` (146 of 146 in `Areas/Admin/Views`). A `type="button"` save comes from a CW view, so read the view before choosing how to submit it. Judging a POST by the state it changed still applies. |
| 43 | Building the U2 fixture, a brand-new customer could not check out: the screen showed a billing address, the form posted `BillingAddressId=272`, and the server answered "Billing address is not provided" every time. The UI (`PackNameCheckoutModelFactory.GetCustomerDefaultAddress`) resolved an address through a four-step fallback and posted it; `PlaceOrderAsync` never read the posted value and validated `Customer.BillingAddressId` instead — a column only the stock one-page checkout ever writes, which a packname store never uses. | **A value the UI resolves and posts is not a value the service reads.** When a screen displays a field with no warning and the save still rejects it as missing, do not re-check the screen — find the server-side reader and confirm it consumes the posted value rather than re-deriving its own. The gap hides wherever a customised front end sits on an un-customised base service, and it fails only for records the base path has never touched, so seeded test data masks it completely. |
| 44 | Re-tested gap P-01 — nop's standard checkout still reachable for a packname customer — by hitting five storefront routes, got a redirect from every one, and told the user the gap could probably be closed. The cart was **empty**; nop's checkout redirects an empty cart before it evaluates anything else, so all five redirects were the empty-cart guard and said nothing about packnames. Repeated with a loaded cart, `/onepagecheckout` renders in full. The gap stands. | **A guard that fires for a reason unrelated to the thing under test is not evidence the thing is guarded.** Before reading a redirect, a 403 or an empty result as proof a rule is enforced, satisfy every *earlier* precondition the endpoint checks — a non-empty cart, a signed-in user, a selected shipping method — so the only remaining explanation is the rule being tested. Name those preconditions in the test itself; the cheapest version is to confirm the endpoint DOES work when the rule should not apply, before asserting it does not when it should. Same family as #35: a wrong precondition gives a confident right-looking answer to a question that was never asked. |
| 46 | Specified a lock for a double-write race, validated it by reading the code and by one live test where two requests arrived ~140ms apart, and told the user the invariant was now guaranteed. It was not. nop's `MemoryCacheLocker` acquires through `IMemoryCache.GetOrCreate`, which is `TryGetValue` → run factory → `Set` and is **not atomic**: three requests dispatched in the same tick all acquired, and one return case was resolved three times with three sets of frozen charges. The lock only ever de-duplicated collisions far enough apart to see each other. | **A lock is a claim about concurrency, and only a concurrent test can support it** — reading the implementation is not enough, because the defect is in an `IMemoryCache` extension method two layers below the code being read. Test it by firing genuinely simultaneous requests (`Promise.all` of N fetches is enough) and asserting on the state, not the response; a collision engineered to be milliseconds apart proves the easy half and hides the real one. And when an invariant carries money, put it where it cannot be argued with: a unique index holds under any timing, any locker and any number of app instances, and the lock becomes an optimisation rather than the guarantee. Corollary: before claiming a platform primitive is safe, check whether it is atomic — `GetOrCreate`, `TryGetValue`-then-`Set`, and read-then-write cache pairs are not. **5.00:** Still true in 5.00: `Libraries/Nop.Core/Caching/MemoryCacheLocker.cs:41` acquires through `_memoryCache.GetOrCreate(...)`, which is not atomic. |
| 48 | Wrote phase 1's manual step M1 to expect that a closed-season packname "behaves as cannot order" — catalogue empty, add-to-cart refused. Phase 1 changes what the VIEWS report; enforcement at add-to-cart is S-03, scheduled for phase 4a and not written yet. The user followed the step, was allowed to add to the cart, and had to ask whether he had found a bug or whether the item was simply missing from the plan. | **A phase's test plan may only assert what that phase changes.** Everything else in the same screen is a *characterisation* — write it as "record what happens today; phase 4a changes it", never as a pass/fail. The tell is an expected result phrased in terms of user-visible behaviour when the phase only touched a query, a view or a calculation: ask which change in THIS phase's own list would have to be wrong for the check to fail, and if there isn't one, the check belongs to another phase. |
| 49 | Wrote M5's assertion as "re-open the dialog, it must read 25, not 0 — this is the S-11 assertion", and carried the same claim into the gap analysis ("the admin dialog shows topup 0") and into two test-file comments. The field always reads 0: `PrepareSubsidyTopupModelAsync:904` sets `model.Credit = 0` unconditionally, after mapping the found row, because it is an amount to ADD and `SaveSubsidyTopupForPeriodAsync` does `existing.Credit += creditToAdd`. I had read both methods while specifying the phase-1 fix and still described the screen from imagination. The SQL block beside the step already asserted the real thing — one row, accumulating credit — so the wrong check sat next to the right one. | **Before asserting what a screen displays, read the code that populates the model — the factory, not the service.** A service that returns a row says nothing about whether the view binds it; an unconditional assignment after the mapping is exactly how an input field is made blank on purpose, and it is invisible from the service side. Broader: when a defect's mechanism is a duplicate row, a wrong total or a stale cache, the assertion belongs on the STATE (row count, sum, ledger) and never on a form field — fields are the most likely part of a screen to be intentionally reset. If a check and a query sitting next to each other test the same defect, and only one of them is derived from code, delete the other. |
| 50 | Told the user that 4.1's credit-notes-before-subsidy ordering contradicted decision 15, and **reopened a settled business decision** on the strength of one parenthetical in the gap analysis: "`ISettlementPipeline` (credit notes priority 1, subsidy priority 2 — `SettlementPipeline.cs:34-63`)". The code says the opposite — `SubsidySettlementSource.Priority => 1`, `CreditNoteSettlementSource.Priority => 2`, each with a comment saying why — and has since the exchanges rework. Worse, `subsidy-system.md`, the maintained reference, states the correct order twice; I had **two documents disagreeing** and picked the stale one because it happened to agree with the 4.1 code I had just read. The user had to correct me, and also supplied what I had missed: 4.1 applied subsidy *units* before everything and only the *money* after credit notes, which is why it looks like a contradiction and is not. | Lesson #27 says a document quoting code is a claim, not evidence. Two things to add. **The stakes change the standard:** using a quoted line to implement is one thing, using it to tell someone a settled decision was wrong is another — anything that would reopen a decision, contradict a stakeholder or change a plan's direction gets the file opened first, every time, no exceptions for a line that "looks like code". **And when two documents disagree, neither is the answer.** The instinct to resolve it by picking the one that fits what I have just been reading is confirmation bias with a citation attached; the disagreement is itself the signal to go to the source. Corollary for cross-version comparisons: when an old version looks like it contradicts a decision, ask what ELSE that version did in the same area before reporting the contradiction — here, units-first was the half that mattered and it made the two versions agree in substance. |
| 51 | Changed `ValidateTopupAccessAsync`'s signature and gave the user the four call sites I had read — `ChangeTopupSubsidy` and `ChangeTopupProductGroup`, GET and POST. There were **six**. `ChangeTopup()` and `GetTopups()` also called it, further down a file I had only ever read in slices, and he was left with two methods that would not compile after applying everything I sent. I had grepped for the two action NAMES I was working on and never once grepped for the method whose signature I was changing. | **Changing a signature starts with grepping the signature, not the feature.** One `grep -n "<MethodName>"` over the file (and the solution, for anything not `private`) before writing a single replacement — the cost is one command and it is the only thing that finds a caller in a region you have not read. Two corollaries. **A slice-read file is an unread file**: `sed -n '215,265p'` four times is not the same as having read it, and my confidence should track what I actually enumerated, not how much of it I happened to look at. And **when the callers turn out to span most of a file, the unit is the file** — lesson #47 says the whole enclosing thing, and for a signature change the enclosing thing is every file that calls it; delivering it as a replaceable file removes the paste risk entirely. Same family as #18 (enumerate every reader) and #30 (enumerate every writer), now for the caller graph. |
| 53 | Specified change 8 — retiring a per-pair cache key for a per-manager set — by reading the method being replaced and enumerating *its* callers, per lesson #51. Complete, and still wrong: **four** cache consumers clear that key's prefix, not one. I found and fixed the one consumer that was already broken for another reason (P-04) and never grepped the KEY, so `BranchCacheEventConsumer`, `PackNameCacheEvenConsumer` and `PackNameListCacheEvenConsumer` would have gone on clearing a prefix nothing writes — leaving the new set never invalidated when a branch, packname or list changed. A worse defect than the key explosion it was fixing, and invisible until a manager's scope silently went stale. Only caught because the user asked "am I going to replace a change and lose it?" | **A cache key, a setting, a resource string, a route name and a feature flag each have readers the method does not** — for a cache key those readers are every consumer that invalidates it, and they live in files the method's call graph never touches. So lesson #51's rule generalises: grep the *identifier being retired*, not just the method whose body you are rewriting, and do it for every named thing the change removes or renames. Two corollaries. When retiring a key, the invalidation consumers move **in the same change** — a rename that leaves them behind converts a performance fix into a staleness bug. And prove invalidation by acting then re-loading on a **second** request, never by reading the same page: a cache that is never cleared looks exactly like one that works, right up until it doesn't (#38). |
| 57 | Validated an eight-change security phase three times and each pass found only what the previous pass had introduced: the resource string I had buried in an aside, then the duplicated clause my own fix left behind, then the no-op argument in the consumer I had just rewritten. The user said it "starts to look like we are walking in circles". Every one of those passes reviewed **my diff**. The fourth pass read the whole of every file the phase touched, with independent reviewers who had not seen the changes — and found a reachable cart mutation, an order-creation path neither guard watched, two missing cache consumers, an unmapped column in a guard that runs on every checkout, and four trusted request values. | **Reviewing a change set means reading the files it lands in, not the change set.** A diff review can only find defects in the lines that changed, so it re-finds its own residue one layer at a time and reads to the user as thrash. Budget the expensive pass once, early: read each touched file top to bottom, and where the stakes justify it have someone who has not seen the changes do it. Two tells that a review is circling — every finding is about a line the previous pass wrote, and each pass is smaller than the last. When that happens, stop patching and re-read. |
| 58 | Told the same user twice that a guard was complete. The checkout filter "covers every action on `CheckoutController`" — it did, and nop also fixes `action = "Cart"` in the route where eight methods answer behind form-value constraints, one of which mutates cart quantities; and orders are also created by `ProcessNextRecurringPaymentAsync`, which never calls the method the second guard overrides. Separately I said four cache consumers covered a key, then five, then — after a table — six. Each claim came from grepping the code I had written. | **Prove a guard complete by enumerating its INPUTS in a table, not by grepping your own code.** For a reachability rule the inputs are every route in the solution that can reach the protected operation (including the framework's own routes and every plugin that calls the service); for a cached query they are every entity whose change alters the result; for a controller they are every value the request carries. Write the table into the permanent document, one row per input with the thing that covers it — building it is what finds the missing rows, and keeping it is what stops the next person re-deriving it. A grep answers "did I do what I intended"; a table answers "is there anything I did not intend". |
| 59 | Wrote a guard that runs on **every** order placement and ended its query `select pnci.Id).AnyAsync()` on a mapping entity whose builder declares `ShoppingCartItemId` as the primary key. nop only injects the identity `Id` column when no column is already a PK, so that table has no `Id` and `BaseEntity.Id` is unmapped for the entity — the projection referenced a column that does not exist. Every other query in the plugin against that entity avoids `.Id`, which is the tell I walked past. If it throws, no customer can check out at all; the matrix that would have caught it had not been run yet. | **Before projecting or filtering on a member of a mapping entity, read its `*Builder.cs`** — `BaseEntity.Id` exists on the class whether or not it exists in the database, so the compiler cannot help and the failure is at runtime, inside a guard, on a path every customer takes. Two habits: when an entity is a join table (two foreign keys, no obvious identity), assume `Id` is unmapped until the builder says otherwise; and when a new guard runs on a universal path, its first test is the **control case** — an ordinary customer completing the flow the guard is not meant to stop. |
| 60 | Shipped a phase whose subject was "who may act on which packname", with four guards that all re-check the subject they are handed. The user then found, by using the app, that revoking a manager's department left the revoked packname sitting in the ordering context: the guards refused an action ON it, while the catalogue, the subsidy bar, add-to-cart and the checkout — none of which has a guard, because they take the context as the actor's own — carried on spending that employee's subsidy. The stored selection had only ever been validated against the current STORE. | **Ambient context is an authorisation boundary, and it is the one nobody guards.** A selected account, an impersonated user, a current store, a chosen profile: whatever supplies the *subject* to every rule must answer the same question those rules ask, on every resolution, and must self-heal when the answer changes — clear the stale selection and fall back, rather than trusting it until someone re-picks. Ask of any access phase: what is the subject, where does it come from, and who validates THAT. Corollary on where this came from: it was found by operating the feature, not by reading it — an access model that has only been read has not been tested, because the interesting states (revoke, then return) only exist in sequence. |
| 61 | Delivered phase 2b part 1 as a spec built from five top-to-bottom file reads, then needed **four** correction rounds — C (14), D (4), E (2) and F (4) — of which eleven were defects in the spec itself. Every single one was the same mistake: a claim about a file I had not opened while writing that claim. C1 missed a second cache consumer that my own spec told the reader to grep for. C3 said the mapper does not map `PackNameListId`; it does. C6's change dropped `overridePublished: true` and created the bug it claimed to fix. D1's comment said the form was "parsed exactly as nop parses it" when nop's loop has no `break`, so the last key wins and my first-match version left the bypass open. F2 checked `CORE_PN_OrderShipping` for an address that lives on the `Order` row. | **A sentence naming another file is a citation, and a citation is opened before it ships.** Prose review cannot catch this class, because the prose is exactly where the wrong belief lives — it reads fluently and confidently either way. So put a mechanical pass *before* the prose pass: a script that matches every `RemoveAsync` against its key's placeholder count found C1 and nothing else would have; one that matches every `AddModelError` key against the `asp-validation-for` spans in the view that action actually returns found E1 and E2. Write the checker for the defect *shape*, run it over the whole plugin, and do it before anyone reads a word. Corollary, learned the hard way in the same round: **scope the checker to the exact unit the defect lives in.** My first validation script was folder-wide and reported `BranchId` as fine because a *different* view in that folder had the span; the per-view version found both defects. Coarse automation does not fail loudly — it returns a pass. |
| 62 | Wrote six findings in one phase that were the same defect: a picker whose GET filtered by store and whose POST inserted whatever ids arrived. I filed them as six unrelated items against six actions, found them one at a time by reading each action, and never once searched for the shape. The same phase had a second instance of one-shape-many-sites: three `ModelState` errors keyed to properties no view rendered. | **When two findings share a mechanism, stop finding and start enumerating.** The second occurrence of a shape is the signal that it is a *class*, and a class is searched for mechanically across the whole codebase, not discovered site by site — the sites you reach by reading are the ones you happened to read. Two shapes worth keeping a standing check for, because both are invisible to the compiler and to a diff: **read-scoped / write-unscoped** (any GET that filters and a sibling POST that does not re-run that filter) and **a write path that trusts an id the corresponding read path derived**. The fix for the first is always to re-run the picker's own query in the POST and accept only what it returns — never to write a second, parallel predicate, which is how the two drift apart again. |
| 63 | Matched an order-guard filter on the action name, because six of nop's admin order actions are `[HttpPost, ActionName("Edit")]` dispatched by `FormValueRequired` — so the action name is `"Edit"` for all six *and* for the GET screen, and the project's own `CoreActionFilter.IsActionName` helper cannot tell them apart. Separately, and worse, I guarded the posted order id only: nop loads the order from the route and then takes the order *item* from a form key and the address from `model.Address.Id`, checking neither against that order — so posting a plain order's id with a packname order's item id walked straight past the guard. | **A filter's discriminator must be the thing the framework actually dispatches on, and its subject must be every identifier the action acts on.** For the first: when several methods answer to one action name behind form-value or route constraints, `descriptor.MethodInfo.Name` is the only discriminator — and a project helper named `IsActionName` will happily return true for all of them. For the second: enumerate the identifiers the *target* method reads, not the ones the route carries, and check each in its own right. Then make the guard **fail closed** — a blocked method whose subject cannot be identified is blocked, because that is the branch a framework binding change lands in. Related to #58's input table; the difference is that here the inputs are inside one method rather than spread across routes. |
| 64 | Built a 65-check run sheet, ran 43 of them live, and two of the passes were worthless. D4 posted a packname order's address **with that order's own id**, so the order-id condition fired and the address condition was never reached — the guard passed the check and was wrong; D10, which posted an innocent order's id with that address, found it. A11 asserted "no unpublished product in the picker" against a catalogue in which all 108 products were published. And G3, meant to prove a stored procedure had been updated, invented the procedure's name: `OBJECT_DEFINITION(NULL)` returns `NULL`, `NULL LIKE '…'` is UNKNOWN, my `CASE` fell through to `'OLD'` — so a wrong name reported identically to an un-updated procedure. | **A check is only evidence if it could have failed.** Three habits, one per failure. **For a guard with more than one condition, one test row per condition with the others innocent** — otherwise the first condition to fire masks every one behind it, which is exactly how a half-built guard passes a full matrix. **Before asserting an absence, prove the thing could have been present**: if every row already satisfies the assertion the check is vacuous, so establish the negative case first (I had to unpublish a product to make A11 mean anything) and restore it after. **And write every diagnostic so that "not found" and "found, unchanged" are different answers** — a `CASE` with no `ELSE` for the unknown, a regex whose non-match looks like a mismatch, an exit code that collapses both, all turn a typo in the test into a confident report about the code. |
| 65 | Drove the admin grid's inline edit from the browser, posted `Quantity = -5`, watched the row revert, and reported — twice, the second time as "confirmed conclusively" — that the refusal was silent and the admin saw nothing. I had checked the DOM, run a MutationObserver over the whole body for three seconds, and grepped the rendered text for the message. All three came back empty because nop shows that error with a native `window.alert`, which is not DOM, emits no mutation and appears in no `innerText` — and the automation driving the page dismisses it without telling me. Hooking `window.alert` for one run returned the message immediately: the feature worked the whole time. | **A native browser dialog is invisible to every DOM-based check, so absence of evidence in the DOM is not evidence of absence on screen.** Before concluding that a UI gives no feedback, hook `window.alert`, `window.confirm` and `window.prompt` — and remember that automation silently answers them, so the more reliable your tooling feels, the more confidently it will report nothing happened. Generalise past dialogs: the same blind spot covers anything rendered outside the document — native validation bubbles, `<dialog>` in the top layer, browser download and permission prompts, canvas- and shadow-DOM-rendered text, and `console` output. **Ask what the mechanism would be if the feature DID work, and test for that**, rather than sweeping for its absence: I had already read `display_nop_error` ending in `alert(message)` and still ran three more DOM sweeps instead of the one check that matched the mechanism. And a second time-related trap: "I looked and it was not there" is worth far less than "I made it prove it was there" — the positive control is cheap and it is what distinguishes a working feature from a broken observer. **5.00:** Still true in 5.00: `display_nop_error` in `wwwroot/js/admin.common.js` ends in `alert(...)`. |
| 66 | Three times in one phase I changed what an endpoint RETURNS and never read what CONSUMES it. **D3:** made `SavePackNameList` answer with a partial on failure, when the JS discriminated success by Content-Type — so the editor closed as though the list had saved. **G1:** gave a resource four placeholders and left the storefront caller passing one, turning a working top-up into a 500. **I1:** added `{success:false,message}` to `GeneratePackNames`, whose handler is `success: function () { alert("…in the background"); }` — no parameter, payload ignored — so the refusal would have been invisible and the admin would have been told the opposite of the truth. The user caught the third one by reading the view I had not opened. | **A response body is an interface, and the caller is part of the change.** Whenever an action starts returning a new shape — including returning anything at all where it previously returned `NullJsonResult` or nothing — grep every caller of that URL and read the handler before writing a line of the server change. The specific tell for an AJAX admin screen: `success:` handlers very often take **no parameter**, so ignoring the payload is the default state, not the exception — and the failure mode is the worst kind, a refusal that reports success. Two corollaries. When the success path returns `null`, the new guard must be `if (data && data.success === false)`; a bare `!data.success` throws and breaks the path that used to work, so the **control case is the test that matters**, not the refusal. And prefer alerting the server's own message over a new client-side string: it keeps the text localised in one place and keeps the change rebuild-only instead of needing a plugin update. |

## #67 — "the caller copes" is not the same as "the admin can tell"

Round J, 2026-09-22. Fourth instance of the #66 shape in one phase (D3, G1, round I, this). The new
part: I *did* check the caller this time. I traced change 69's `{success:false}` payload into
`updateNodeInTree`, confirmed `if (!$row.length) return;` makes it a no-op, and called it safe. It was
safe and it was still broken: nothing alerted, and nothing re-enabled the Save button the handler had
disabled, so the refusal was invisible AND the form was dead until reload.

The mechanical form of the check, which "does the caller cope" does not cover:

  when an endpoint gains a `success:false` shape, the handler must (a) surface the message and
  (b) restore EVERY control it disabled or replaced on the way in.

Tell: a handler that opens with `$btn.prop('disabled', true)` has exactly as many re-enable sites as it
has exit paths. Count them. This one had three exits and two re-enables.

Second tell, cheap and general: before believing a screen behaves, hook `window.alert` and read the
button's `disabled`/`textContent` after the click. A DOM-only inspection is blind to a native dialog
(#65) and says nothing at all about control state.

## #68 — a comment is not a comment to Razor

Round J, 2026-09-22. I wrote `//@T, not @(...)` as an explanatory JS comment in
`_Branches.BranchDetail.cshtml`. Razor does not respect `//`: it saw `@(`, tried to compile `...` as
C#, and the view failed with CS8635 at runtime - the whole admin area down until the line was removed.

The site had already been rebuilt and was serving before this; my own validation sweep passed it
because that sweep checks braces, resource keys, `string.Format` arity and callers, and **nothing in
it compiles Razor**. A `.cshtml` can clear every mechanical check I run and still not build.

Two rules going in:

1. Never write a bare `@` inside a `.cshtml`, including in comments and including when the point of the
   comment is to talk about `@`. Write "at-sign", or escape it as `@@`.
2. Add to the post-edit sweep for any touched `.cshtml`:
   `grep -n "^\s*//.*@" <file>` - every hit is a compile error waiting for the first request.

Related to #62's shape: I wrote the check that would have caught this only after it bit.

## #69 — a UI helper must not restore controls it did not disable

Round K, 2026-09-22. `updateNodeInTree` (a tree-rendering helper) carried
`$('#bd-save-btn').prop('disabled', false)` - and carried it *after* its own
`if (!$row.length) return;`. So the save button's restoration depended on whether the branch's node
happened to be rendered. A successful save with the node absent left the form dead on "Saving...",
and a successful save with it present relabelled the button with a hard-coded English 'Save changes'
over the localised one.

This is why #67 was not enough on its own: I wrote "restore every control it disabled" and then applied
it only to the path I was testing, because the other path *appeared* to work - something else was
restoring the button as a side effect. Side-effect restoration passes the happy case and fails the
moment the side effect's precondition does not hold.

Rule: the handler that disables a control re-enables it, on every exit, in the same function. If
another function is doing it, that is the bug, not a shortcut.

Two testing habits that found this and #68's sibling, both worth keeping:

- **Sample state DURING the request**, not only before and after. `Saving&#x2026;` was invisible at
  both ends.
- **Construct the negative condition deliberately.** No ordinary click-through produces "node absent
  from the tree"; I only hit it because I had created the fixture by POST and never refreshed the tree.
  The bug had been reachable in production the whole time.

## #70 — filtering a picker and refusing a value are one change, not two

Round W, 2026-09-23. Part 4 filtered the storefront branch picker (change 109) and, in the same round,
added a refusal for posting an inactive branch (change 135b). Both were right. Together they made a
third path: the **validation redisplay**, which renders the narrowed picker while the model still
carries the rejected value.

`PreparePackNameEditModelAsync` ended with

    model.BranchName = model.AvailableBranches
        .FirstOrDefault(item => item.Value == $"{model.BranchId}")?.Text
        ?? throw new NopException("The Branch couldn't be found for the manager");

On a GET, `model.BranchId` comes from the entity and is always in the list. On a redisplay it is the
**posted** id — the one just refused, and therefore deliberately absent. So the server refused
correctly and the screen 500'd, and the refusal message the round had just added was never seen.

The spec named that line as the crash risk, and the test written for it (C7) passed, because C7
exercises the GET path. **I wrote the test for the failure mode I had already imagined.**

Rule: every `FirstOrDefault(... == model.X) ?? throw` downstream of a list that some change now
filters is a 500 waiting for the first refusal. Resolve such a value from the **entity**, never from
the request (this is phase 2b's rule 3 in a place it is easy to forget it applies), and when a round
both narrows a list and adds a refusal that uses it, test the redisplay as a third case.

## #71 — a value derived only from a procedure's parameters is a scalar, not a join

Round X, 2026-09-23. Change 113 added an `AssignmentRemoved` column to a history SP by joining
`SubsidyPackName` in eight `UNION ALL` branches. But the flag depends only on `@PackNameId` and
`@SubsidyId` — both **procedure parameters** — so it was a single value for the whole result set and
never needed a join at all.

Carrying it on a join made every branch's row count depend on how many rows that join matched. A
packname removed from a subsidy and re-added has two assignment rows, one deleted and one live, so
every history row came back twice: once flagged *Removed*, once not. The fix deleted three joins,
eight expressions and a column of duplication, and replaced them with

    DECLARE @AssignmentRemoved BIT = CASE WHEN EXISTS (...live row...) THEN 0 ELSE 1 END;

which is also the correct semantics — re-adding restores the assignment.

Two things worth carrying:

- **Soft-deleted history means "one matching row" is never guaranteed.** Any join added purely to
  carry a value must be checked against the remove-then-re-add shape, which is exactly what a
  filtered unique index is there to allow.
- **A risk you document but do not test is a risk you accepted on the reader's behalf.** The spec told
  the developer to run `GROUP BY SubsidyId, PackNameId HAVING COUNT(*) > 1` before applying, called the
  duplicates "legacy", and I never ran it. They were not legacy; they are what the normal flow
  produces.

## #72 — when a guard has to be remembered at N call sites, N is the bug

Phase 2c round A, 2026-09-24. The "unsaved edits" gate was first written at the call sites. A grep
found ~40 places across 10 files that can tear an open form down — breadcrumbs, the tree, tabs, row
clicks, add buttons — so every review pass found more holes, and one change produced a prompt that
fired *after* the form had already been destroyed: worse than no prompt at all.

The gate moved inside the six functions that actually do the tearing down (`navigate`,
`goTab`, `selectBranch`, `enterFocusMode`, `exitFocusMode`, `loadNewChildDetail`). A new call site
cannot forget it.

Rule: put the guard where the thing happens, not where it is requested. The exception is a call site
that performs part of the teardown **itself** — the branch-tree row click repaints the selection and
expands the node before delegating, so it must ask before it paints. **A call site that changes
nothing itself needs no guard; one that changes visible state does.**

Corollary on detection: the first version listened for `input`/`change` on `:input`, so any editor
widget firing `change` while initialising would have marked the form dirty before the admin touched
it. Comparing against a baseline taken at bind time cannot do that, and it correctly reports clean
when someone types and undoes.

## #73 — the minifier can move a `let` out from under the function that reads it

Phase 2c round B, 2026-09-24. An inline "add" row did nothing: no request, no error, nothing in the
console. Three rounds of speculative fixes missed it because the source is correct.

`WebOptimizer.EnableJavaScriptBundling` is true, so NUglify minifies the admin bundle. It inverts an
early-return guard into a wrapping `if` block — and moves the `let` declarations that followed the
guard **inside** that block, while `function` declarations stay hoisted at function scope:

    // source
    const $input = $(config.input);
    if (!$input.length) return;
    let busy = false;
    function commit() { ... if (!name || busy) return; ... }

    // minified
    function li(n){ function f(){ ... if(t&&!r){...} }   // f reads r
                    const i=$(n.input);
                    if(i.length){ let r=!1; ... } }      // r lives in the block

`commit`'s `busy` then resolved to an unrelated outer variable that happened to be truthy, so every
commit bailed silently.

Rule: in bundled admin JS, a closure variable read by a **hoisted inner function** must be `var`.
`var` is function-scoped whatever the minifier does with the declaration's position. Reproducible
only in the minified bundle — `node --check` and the unminified page both pass.

**5.00:** Still true in 5.00: `App_Data/appsettings.json` has `EnableJavaScriptBundling: true`, and `AddNopWebOptimizer` (`ServiceCollectionExtensions.cs` ~414–433) turns on minification with it.

## #74 — a disk cache is not cleared by restarting the process

Phase 2c round B, 2026-09-24. After fixing #73 the export still was not there. I told the developer to
restart the app; he did, and nothing changed — because I had read `EnableMemoryCache: true` and
stopped, two lines above `EnableDiskCache: true` and a `CacheDirectory` pointing at
`Presentation/Nop.Web/wwwroot/bundles`. The stale bundle was a file on disk, base64 inside a `.cache`
JSON wrapper, dated before the edit.

Rule: **a `.js` or `.css` edit needs `wwwroot/bundles` emptied.** Not a rebuild, not a restart.
`.cshtml` and `.cs` are fine on hot reload, which is what makes this so confusing: view changes land
instantly in the same session where JS changes appear inert.

Two wider points. **Read the whole configuration block before explaining a caching symptom** — the
answer was two lines below where I stopped. And when the browser and the source disagree, prove
*which artefact* the browser is running before theorising about the code: here,
`Object.keys(Admin.StoreNav)` in the console against the export list in the file settles it in one
line.

**5.00:** Still true in 5.00 (checked 2026-10-22): `App_Data/appsettings.json` has `EnableDiskCache: true` and `CacheDirectory` pointing at the site's `Presentation/Nop.Web/wwwroot/bundles`.

## #75 — a container that outlives its partial accumulates handlers

Phase 2c round B, 2026-09-24. Deleting a packname list asked "are you sure?" four times and would
have fired four deletes. The developer spotted the shape from the symptom alone.

`ensureSubPanel(id)` creates `#pnlists-panel`, `#cc-panel` and `#branch-addresses-panel` **once**;
`loadPartial` only swaps their `innerHTML`. The partial's inline script binds delegated handlers to
that surviving container, so each reload adds another set — measured 1 → 2 → 3 → 4 over three
reloads. Both add and delete call a reload, so ordinary use compounds it.

Rule: a partial that binds delegated handlers to an element it does **not** own must `.off('.ns')`
first and namespace every handler. Handlers bound to elements *inside* the replaced markup are safe
— those are new elements on every render, and that difference is worth stating in the code.

`e.stopPropagation()` is not a fix and hides the diagnosis: the duplicates are on the **same**
element, and `stopPropagation` stops bubbling to ancestors, not siblings on the same node.

## #76 — a model property nobody reads is a property nobody fills

Phase 2c, 2026-09-24. Twice in one phase, adding the first consumer of a model property revealed it
had never been populated:

- `BranchModel.StaffCount` always rendered 0. Two `PrepareBranchModelAsync` overloads exist; the
  branch page uses `(int storeId, int? branchId, int? parentBranchId)`, and the fix had been applied
  to the other one.
- `PackNameModel.StoreId` was 0 on the customer card, so a new deep link built
  `/Admin/MultiStore/Edit/0`. `CustomerOverrideModelFactory.PreparePackNameModelAsync` **receives**
  `storeId` and used it only for the settings model.

Both were silent for as long as nothing read them, and in both cases the field looked populated
because the method around it clearly had the value.

Rule: before using a model property that nothing currently renders, **find the write**, not the
declaration — and when a factory has more than one overload, confirm which one the screen in front of
you actually calls. Same family as #10: verifying that something *exists* is not verifying that it is
*filled*.

## #77 — a debugging probe left in the page becomes the thing you are measuring

Phase 2c round B, 2026-09-24. I monkey-patched `Admin.StoreNav.bindInlineAdd` in the live page to
test a hypothesis, never reloaded, and then spent several exchanges reporting behaviour of my own
stub back to the developer as if it were his code — including "the POST is dispatched", which his build
never did. The stub was visible the whole time: its error string was not in the source file.

Later, the same tab sat open across an app restart and its SPA state went stale (`tab: "info"`, empty
`branchPath`), so a reload fetched `branchId=0` and rendered an empty collection, which I briefly
reported as a regression.

Rule: **reload the page before every verification round**, and after any session where the page was
instrumented. When live behaviour contradicts the source, the first check is whether the page is
running the source at all — read the function back (`fn.toString()`) and look for something only the
real file contains.

### #76, third instance — the reader can exist while the writer does not

Phase 2c closing round, 2026-09-28. Removing a factory overload that had **no callers** turned out to
remove the only code that populated `PackNameListModel.MultiStoreSettingsModel`. The managers grid
renders with its parent's model and gates four columns on that object, so with nothing filling it the
record's constructor default applied — every flag false — and Employee Number, Print Name, Gender and
PO Entry were hidden on every packname list regardless of the store's settings.

The addition to the rule: #76 was about a property nothing read. This is the mirror image — something
*did* read it, and the absence was invisible because the default was a valid-looking object rather
than a null. **A default-constructed sub-model is indistinguishable from a populated one at the call
site**, so "the view reads it, therefore something fills it" does not follow.

Practical consequence, and the reason the audit was worth doing: deleting dead code is a good way to
find live bugs, because whatever the dead code touched is code nobody has checked.


### #78 — `document` is a container that outlives everything

Phase 2d, 2026-09-29. `_Branches.BranchTree.cshtml` bound its three handlers with
`$(document).on('click', '.branch-row', ...)` — no namespace, no teardown. The partial is reloaded
by `loadBranchTree()` on every branch save, create and delete, so each reload added another set.
After three saves, one click on a branch row asked the unsaved-changes question three times and
called `selectBranch` three times.

Lesson #75 named the panel case: a container created once and refilled with `.html()` outlives its
partial. **`document` is the extreme case of the same thing and is easy to miss**, because it does
not look like a container you own — which is exactly why it is chosen for delegation. The rule is
unchanged: delegate from the nearest stable ancestor, `.off('.ns')` first, namespace everything.

The measurement that proves it either way: count handlers on the node across repeated loads.

```javascript
($._data(document.querySelector('#panel'), 'events').click || []).length
```

Flat across four loads is the pass condition. This is worth running as the *first* check on any
partial that re-renders, not the last.

### #79 — `!important` utilities cannot be hidden by `.hide()`

Phase 2d, 2026-09-29. A wrapper carried both `managers-table` (which a toggle hides) and Bootstrap's
`d-flex`. It never hid. Measured in the page:

```javascript
w.style.display = 'none';        getComputedStyle(w).display  // "flex"
w.classList.add('d-none');       getComputedStyle(w).display  // "flex"
$(w).hide();                     getComputedStyle(w).display  // "flex"
```

`.d-flex` is `display:flex !important`, which beats an inline `display:none`; and in this build's
utility order it also beats `.d-none`, so even the framework's own hiding utility loses. jQuery's
`.hide()` reported success — it set the inline style — while the element stayed visible.

Two things follow. **A display utility and a scripted show/hide on the same element is a conflict**,
and the utility wins. And **`.hide()` returning is not evidence**: verify with
`getComputedStyle(el).display`, not with the inline style or with jQuery's `:visible`, which reads
the same losing signal.

The fix is to remove the utility class, not to escalate the selector.

**5.00:** nop's own admin helpers `$.fn.hideElement` / `showElement` (`wwwroot/js/admin.common.js:3–11`) only add and remove `d-none`, so in this build they lose to `d-flex` as well. The fix is still to remove the utility class.

### #80 — a synthetic event can leave state the handler never saw

Phase 2d, 2026-09-29. Testing a checkbox toggle, I set `chk.checked = false` then fired
`$(chk).trigger('click')`, and afterwards restored the original with `chk.checked = was`. Both
assignments change the value **without running the handler**, so the DOM was left in a presentation
that did not match the checkbox, and the next snapshot read as an inverted toggle. I reported a bug
that did not exist before re-testing from a fresh load.

For a checkbox, `el.click()` (the native method) toggles the state and then fires handlers, in that
order — which is what a user does. `$(el).trigger('click')` runs handlers first and the default
after, so the handler sees the pre-click value.

Rule: **drive a UI test the way a user drives it, and re-load before drawing a conclusion from
accumulated state.** Restoring a value by assignment is not restoring the UI. This is lesson #77's
"reload before every verification round", arriving from the other direction — there the page was
stale, here the state was.

### #81 — when a property's type changes, search for its name, not for its owner

Phase 3, 2026-09-29. Making `Subsidy.DurationStart` / `DurationEnd` non-null, the consumer list was built by
searching for the `Subsidy` type and its DTOs. The build then failed in `SubsidyOrderingService`, which reads
the dates as `subsidyVariation?.DurationStart ?? subsidy.DurationStart` — the `??` made the expression
non-null, and `.HasValue` stopped compiling. Nothing on that line names the type being changed.

Rule: **list the consumers of a changed member by the member's name** (`\.DurationStart\b` and its
`?.` / `??` / `.HasValue` / `.Value` forms), across every project in the solution, before sending the
change list — and compile-check the list against that search, not against the type's usages.

### #83 — the test plan is written against the code, not against the plan

Phase 3, 2026-09-29. Three of the plan's expectations were wrong when run: T13 said declining the
"change credit" question hides the row (declining *keeps* the limit, so the row stays); T15 expected a
pre-season subsidy as a zero-credit catalogue entry (the spec gives that entry only to
`OrderOutsidePeriod` subsidies, and the fixture had it off); and the SQL row named a "part G" that no
change ever wrote. Each cost a round of explanation, and the last one confused the reader.

Rule: **derive each expected value from the code path that produces it, and every named asset from the
change list that creates it.** An expectation that cannot be traced to a line of code, a spec clause or a
change number is a guess, and it gets fixed before the run, not reported after it.

### #84 — a store-scoped list cannot be read once the store is gone

Phase 3, 2026-09-29. Verifying a store deletion, the "after" snapshot asked the MultiStore lists for the
deleted store's rows. Those actions look the store up first and throw `No store found with the specified
id` — correct behaviour — which paused the debugger and hung the test script.

Rule: **plan the observation before the destructive step.** For anything that removes its own parent,
take the "before" through the normal screens and the "after" through the soft-delete-filtered lists that
do not need the parent, or through one SQL query of the `Deleted` flags.

### #85 — a feature that restricts must stop restricting when it is off, and SQL is a reader

Phase 4a, 2026-09-30. Part 1 made a closed subsidy period block ordering everywhere — catalogue,
add-to-cart, selector, info strip — through the SQL view `vw_CORE_SUB_SubsidyPackNameCanOrder`. The developer
asked what happens with subsidies switched off. The view never asked: a store with the feature off but old
subsidy rows would have been blocked everywhere. The review that followed found two more views with the
same gap, feeding the "only allocated products" catalogue and bulk-order restriction and the info strip's
subsidy button.

Why it was missed, three causes:
1. **The rule was written for doors.** `Feature_Toggles_Reference.md` §1.1 says to gate the paths that
   create or consume a feature's data and to hide its entry points. It never said that a feature's
   *restrictions* on other features switch off too, and it never mentioned SQL — the one layer
   `ICWFeatureService` cannot reach, and where subsidy's restrictions live.
2. **Nothing led to the rule.** `subsidy-system.md` never mentioned the feature switch, `CLAUDE.md` did
   not point to the feature reference, and the phase 4a plan did not list it.
3. **The check was about agreement, not correctness.** The analysis made every reader agree with the view
   and never asked whether the view was right with the feature off.

Rule: **for every reader of a feature's data — C#, Razor, JS or SQL — ask what it does when the feature is
off, before specifying it.** Doors close (§1.1) and restrictions lift (§1.5). SQL reads
`vw_CORE_SUB_StoreSubsidyVariationSetting.SubsidyEnabled`. The test plan always has a feature-off row.
Now written into `Feature_Toggles_Reference.md` §1.5 and §0.5, `subsidy-system.md` §1 and `CLAUDE.md` §0.4.

**5.00:** `Feature_Toggles_Reference.md` is in the 4.90 source's `CWPlans/` until it is migrated. The rule is in the 5.00 `CLAUDE.md` §0.4.

### #86 — before a test run, check shared state; reach a gate through a path that passes the earlier checks

Phase 4a Part 1, 2026-09-30. Two things cost time during the run. The tracker had already been filled by
another session running the same tests on the same site, so my run mutated fixtures (a subsidy toggle,
cart lines, the customer's context) under someone else's results. And T18 (no live branch) was first run
on a list the customer did not own, so the ownership check refused the add before the branch gate was ever
reached, so the refusal proved nothing about that gate.

Rule: **before a test run, read the tracker and the fixture state; if another run is in progress or done,
report and do not overwrite.** Record every fixture change and restore it. **For each gate, pick input that
passes every earlier check**, so the refusal proves the gate under test and nothing else.

### #87 — check the type of an extension point before carrying data in it

Phase 4a Part 2, 2026-09-30. O-03 moved the packname order details out of the view by putting the model in
`OrderDetailsModel.CustomProperties`. In nop 4.9 that is a `Dictionary<string, string>`: it did not compile,
and the developer caught it at build time. The spec had been written from memory of older nop versions, where it
held objects, and the one existing use in the plugin (a string label) did not reveal the type.

Rule: **before carrying data through a framework member — a property bag, a context, a cache — open its
declaration in this repository and read the type.** For a model, the pattern that works is a derived record
(`PackNameMiniShoppingCartModel`, `CWOrderDetailsModel`) read with `Model as …` in the view.

**5.00:** Still true in 5.00: `BaseNopModel.CustomProperties` is `Dictionary<string, string>` (`Nop.Web.Framework/Models/BaseNopModel.cs:41`).

### #88 — a switch is not tested until it is tested without clearing the cache

Phase 4b, 2026-09-30. T10 switched subsidies off and checked the catalogue: still restricted. Every reader was
correctly gated — the SQL views read the switch (4a.15) — but their results sat in the static cache, and a
setting write cleared only the feature-flag cache. Switching back on left the catalogue open the same way. It
passed after a cache clear, which is what a restart does, so a test run right after a build would never see it.

Rule: **when a spec gates readers on a setting, list every cache that holds their results and make the setting
write clear it** (`SettingCacheEventConsumer`). **Test the switch both ways in one running site, with no cache
clear or restart between the change and the check**, and after a fix re-run the whole plan, since the restart
clears the cache for every test.

### #89 — an AsyncLocal set inside an awaited method does not reach the caller

Phase 5 Part 1, 2026-10-01. The cart-line ACL window was opened by a helper,
`using var scope = await BeginCartLineCatalogScopeAsync(...)`, which called `PackNameCatalogScope.Begin` and
returned the disposable. An async method runs on a copy of the caller's execution context, and whatever it sets
on an `AsyncLocal` is undone when it returns: the caller carried on with no window, and a packname B line showed
"Product is not published" while A was selected (T6). The exchange picker had the same helper. The unit tests
opened the scope synchronously, so they could not see it; the add-to-cart path, which opens it in its own body,
worked.

Rule: **open an `AsyncLocal` scope in the method whose callees must see it. A helper may look the value up; the
caller sets it.** A unit test that pins the pitfall lives next to the scope (`PackNameCatalogScopeTests`).

**5.00:** `PackNameCatalogScopeTests` is in the 4.9 `Tests/CW.Tests`. It comes across with the CW.Core port.

### #91 — a visibility rule needs both ACL doors: the check and the query filter

Phase 5 Part 2, 2026-10-01. The allocation gate was put on `AuthorizeAsync<TEntity>` for products and on `ApplyAcl`
for categories and manufacturers only. Product pages, blocks and add-to-cart followed it, but a grouped product's
page listed an unallocated associated product: nop loads those with `GetAssociatedProductsAsync` →
`ApplyAcl(query, customer)`, a query filter, never a per-entity check (T16). The unit tests covered the rule, not
where nop reads it.

Rule: **a rule about what a customer may see goes on every ACL entry point nop has for that entity type —
`AuthorizeAsync` and `ApplyAcl` — and the test plan includes a page built from an ACL query (grouped product,
featured products), not only pages built from a check.**

### #92 — read the fixture back before trusting a test that passed on it

Phase 5 Part 2, 2026-10-01. Round 1 first ran against packname 20 and saw the whole catalogue: it also held an
unrestricted subsidy, and a packname is restricted only when every active subsidy restricts it. T4 then "passed"
on PA's related block, but PA had no related products — the block was "also purchased", and 143 was never in it.

Rule: **before running a plan, read back every premise of the fixture from the data (assignments, mappings,
related products) and from the rule's own conditions; a hidden item proves nothing unless it is shown when the
restriction is off.**

### #93 — a member removed from a model is searched in every file that reads the model, views included

Phase 6, 2026-10-02. The spec replaced `PackNameOrderDetailsModel.SubsidyTotals` with `SettlementTotals` and rewrote the
customer order page's totals table — and the same view read the old member a second time, in the order overview at
line 74. Plugin views compile at runtime, so the build passed and the page would have thrown for every order.

Rule: **when a change removes or renames a member, search the whole solution for its name — `.cs` and `.cshtml` — and
list every hit in the spec,** not only the block being rewritten (#81 for C#; views fail later, at render, so the search
is the only check before a test run).

### #94 — a spec never hedges an import: open the file and say what it has

Phase 6, 2026-10-02. Change 30 used `CWWidgetsZones` in `_CWOrderDetails.Info.cshtml` with the note "already in scope if
`_ViewImports.cshtml` imports it". It did not, and the partial had no `@using` of its own; the sibling partial that
already used the type imports it explicitly. The admin order page would have thrown.

Rule: **for every type a view or class newly uses, open its `@using` / `using` lines and the folder's `_ViewImports`,
and either confirm the namespace is there or add the line in the change.** A conditional note is a check that was not
done.

### #96 — nop's `ForeignKey<T>()` already indexes the column

Phase 6, 2026-10-02. A new builder wrote `.AsInt32().ForeignKey<Order>().Unique()`. nop's extension is
`column.Indexed().ForeignKey(…)`, so it creates `IX_<table>_<column>`; `.Unique()` asked for a second index under the same
default name, and the migration failed on the first start (rolled back, so nothing was left behind). The spec had been
checked for exact text and line numbers, never for what the framework calls do.

Rule: **before writing a builder or migration, open the nop extension methods it uses** (`Nop.Data/Extensions`). For a
unique foreign key, use FluentMigrator's own `.Unique().ForeignKey(table, column)` — one index — or create the unique
index with its own name in the migration.

**5.00:** Still true in 5.00: `Libraries/Nop.Data/Extensions/FluentMigratorExtensions.cs:113` is `column.Indexed().ForeignKey(...)`. Tables are created with `this.CreateTableIfNotExists<T>()` (`Create.TableFor<T>()` is gone, `CLAUDE.md` §8.3).

### #97 — expected values come from what the flow under test can actually reach

Phase 6, 2026-10-02. The test plan's hand checks assumed a 19.995 unit price reaches the settlement (nop rounds the unit
price to 20.00 at the cart, so it never does), a gift card can be applied to a packname cart (the packname checkout
refuses every POST to `/cart`), a fee and an order discount on a fully subsidised order (no payment step when nothing is
payable), a subsidy cut to $40 (a variation needs a store setting that was off; a negative top-up is refused) and a
second customer placing first for the same packname (the only admin has no packname). Each was found only on the dev
site, and each forced the figures to be redone there.

Rule: **while writing a test plan, check for every fixture element that the flow under test can reach it** — the
setting that enables it, the screen or endpoint that applies it, the actor who can do it — and derive the expected
figures from the prices the store displays, not from the stored ones. Where an element cannot be reached, say so in the
plan and give the substitute (e.g. lower the cap after carting instead of a second customer).

### #98 — in a page with several confirmation dialogs, click the one that is open; after a missed click, read before repeating

Phase 6 Part 2, 2026-10-07. Authorising a return needs "Save" and then "Yes, I'm sure" in a confirmation dialog. The
page holds three dialogs, two of them with a "Yes, I'm sure" button (status change and undo approval). A coordinate click
looked as if it missed, the status still read Pending, and the save was repeated — but the first click had gone through,
so the exchange workflow ran twice (it failed both times and undid its own credit notes, so no harm this time).

Rule: **pick a confirmation button inside the dialog that is shown (`.modal.show`), never by its text alone; and when an
action that runs a workflow seems not to have happened, read the log or the record before repeating it.**

### #99 — know what a printed line is before counting it as evidence; formatted negatives follow the currency

Phase 6 Part 2, 2026-10-07. "Gift wrapping: No" under the last invoice line was taken as a product attribute — it is the
order's checkout attribute, printed below the product table — so P1 first "passed" on a line that was not an attribute.
In the same run every deduction showed as `($300.00)`: the store's currency formats negatives with brackets, so a
"minus for customers" decision is rendered as brackets anyway.

Rule: **when a test checks a printed line, prove what produced it (a product with a real attribute, not whatever text
sits nearby); and before deciding how deductions should look, format a negative amount with the store's currency.**

### #102 — a method call in a spec is checked against the interface it calls, not against memory

Phase 7, 2026-10-08. Change 28 called `FormatPaymentMethodAdditionalFeeAsync(fee, true, currencyCode, false, languageId)`
— the argument order of `FormatPriceAsync`'s string overload, which the fee method does not share (`currencyCode, languageId,
priceIncludesTax`). The developer caught it on apply; the review pass (#94) had checked every `using` and no signature.

Rule: **for every call a spec adds to a nop or plugin service, open the interface and match the overload — parameter count,
types and order — and prefer copying the call from the nop factory that does the same job (`OrderModelFactory` here).**

### #104 — "on first load" is the server's response, not the page after its scripts

Phase 7 test run, 2026-10-08. F2 ("the fee is missing on first load") looked fixed when the checkout was read through
the browser: the page's own load script posts the pre-selected payment method and swaps the totals in, so the fee was
there by the time the DOM was read. Only the raw first response showed the totals without it — nop's factory marks the
first method selected without saving it, and the totals component reads the saved one (change 62).

Rule: **a claim about what a page shows on load is checked on the raw server response (`fetch` + parse), and the
page after its scripts is checked separately; when the two differ, the difference is the finding.**

### #105 — a storefront page is checked against the theme's base rules, not only its own stylesheet

Phase 7, 2026-10-08. The My Subsidies history and the Store Credit tables linked orders, and the links looked like the
plain cells around them: nop's DefaultClean theme sets `a { color: inherit }`, so a link has no colour unless the page
gives it one. The developer saw it; the run had checked that the links *worked*, not that they could be *seen*. The same
evening a hover underline on a flex control, a colour wheel pushed out of its panel and a caption sitting on its figure
came in one after the other — all visible at a glance, none in the test plan.

Rule: **when a phase adds a page or a control, look at it once as a user before closing — every link is visibly a link,
every control gives hover feedback without artefacts, nothing overflows its panel — and read the theme's base rules
for the elements the page relies on (`a`, `table`, `button`) before assuming the page's own stylesheet is enough.**

### #107 — a page is called dead after requesting it, not after reading the action list

Phase 8 Part 1 analysis, 2026-10-11. `BranchController` and its views were reported dead because the admin menu links
`Branch/Index` and the analysis did not find the action. `Index` was there (it redirects to `List`), and the page is one
the manager uses — it has been since 4.1. The wrong report also hid that the page bypassed every rule the phase added
to Manage Store's branch save; it surfaced only when a branch created there in the test run got no list.

Rule: **before calling a route, page or menu item dead, request it on the dev site (`fetch` and the final URL / status)
and grep for the action by name; report "dead" only with that evidence, and ask who uses a page before proposing to
delete it.**

### #108 — an override flag is saved as posted, never derived from another posted field

Phase 8 Part 1 test run (F1), 2026-10-11. Three settings actions set two *override for this store* flags to "posted
value ≠ stored value" whenever a third override was ticked. nop disables the input of an unticked override, a disabled
input is not posted, and the model binds `false` / `0` — so the comparison saw a change nobody made and one save
replaced the store's automation settings. The Generate buttons also handed the posted (defaulted) rule to their job.

Rule: **on a nop settings screen, every value and every override flag is saved exactly as posted; a value needed after
the save is read back from the settings for the store, not from the model. Test a settings change with the related
overrides unticked.**

### #109 — every writer of an entity gets the change, not only the one in scope

Phase 8 Part 1 (F2), 2026-10-11. The shipping picker, the offered-names filter and provisioning went into Manage Store's
`SaveBranch`; the standalone *Branches* page writes the same `Branch` rows and kept erasing the branch's methods,
creating branches without a list and accepting a looping parent. The analysis had listed it as an insert point and then
set it aside as dead (#107).

Rule: **before specifying a change to how an entity is saved, list every action that inserts or updates it (grep the
entity's `Insert*` / `Update*` calls), and give each one the change or a written reason why not.**

### #110 — a second entry point reuses the first one's validator, it never re-implements its rules

Phase 8 Part 2, 2026-10-18. The Excel import re-wrote by hand the checks nop's `CustomerInfoValidator` and
`AddressValidator` make for the add-packname form. Three review rounds then found one missing or different rule after
another (state required, phone and username format, length, date-of-birth minimum age, read-only checkbox attributes,
Company read from `CustomerSettings` instead of `AddressSettings`, a `{ComparisonValue}` placeholder only FluentValidation
fills). Each fix was a patch, and the copy would have drifted again with the next nop rule.

Rule: **when a new path creates what an existing path already creates (import vs form, admin vs storefront), it maps
its input onto the existing model and runs the existing validator; only parsing (text → ids, dates, yes/no) is new
code. A hand copy of a validator is a finding in review.**

### #111 — a shared row needs every deleter and reader listed, nop's own pages included

Phase 8 Part 2, 2026-10-18. One Address row was made both the packname's delivery address and the account's
billing/shipping address. The analysis checked who writes addresses (#109) but not who deletes them: the admin
packname delete then failed on `Customer.BillingAddressId`, and nop's My Account / checkout / admin customer pages
delete the row and the cascade takes the packname's address with it. Two fixes in a row followed from one design choice.

Rule: **before two owners share a row, list every reader, updater and deleter of that entity in the plugin AND in
nop (controllers, services, cascades and foreign-key rules), and write what each does to the other owner. If nop's
own pages cannot be made aware of the sharing, do not share — copy.**

### #112 — a new endpoint is designed for its failures before its happy path

Phase 8 Part 2, 2026-10-18. The import's first spec covered the normal file; the reviews then found, one by one, the
Serializable default of `TransactionScope`, Excel serial dates, culture decimals, a write-time refusal, an unhandled
exception (bare 500, nothing logged), a non-JSON reply, a check that re-read the sheet with a different row range.

Rule: **for every new endpoint, write down before the code: each stage (read, check, write, report), what can throw
or be refused in it, what the user sees and what stays in the database; the framework defaults it relies on
(isolation, culture, limits); and that every later check works on the data already read, never re-reading the source.**

### #113 — work is checked by someone who did not write it, against the whole feature, before it is handed over

Phase 8 Part 2, 2026-10-18. Each self-review looked at the diff of the previous round and found the next layer of
problems. Three independent reviewers, each with one lens (rows written and every consumer; settings and rules
parity with the form and 4.6; failure paths, HTTP/UI and compile-readiness), found the rest in one pass.

Rule: **before a spec is sent, and again after it is applied, run independent reviews of the whole feature (not the
diff) with one lens each, verify every finding in the code, and drop what cannot be shown. Report the root cause of
each finding, not only the fix.**

### #114 — every delivery is reviewed on its own before it is sent, and a page that only shows never writes

Phase 8 Part 2, 2026-10-19. A fix written after a whole-feature review (the admin popup's list picker) left a variable
it had removed in use; only a second, independent review of that delta caught the compile error before the developer built
it. The same popup created a list when it was merely opened (a GET), which re-opened a branch whose lists an admin had
switched off on purpose.

Rule: **after writing a change set, have someone who did not write it compile it by reading and trace its flows
before it is sent — the reviews that found the problems do not review their own fixes. A GET (a page, a popup, a
model factory) never inserts or updates; a row it would need is created by the save that needs it.**

### #115 — a cost that falls on other users is measured with another user present

Phase 8 Part 2, 2026-10-19. The Excel import wrote up to 500 rows in one transaction. A review blamed table scans for
the store waiting on it and added indexes (change 86) without measuring; the decision to stay synchronous waited on a
timing. T56, run with a second customer browsing, showed every page of that customer frozen for the whole 21 s: the
store's reads of the list being filled wait on the import's uncommitted rows, index or not.

Rule: **any write that can run longer than a second is measured with another user browsing before its design is
settled. Rows are checked first and then written in short transactions (a batch of rows, about half a second each),
never one transaction for a whole file; when a batch fails the result says what the batches before it created.**

### #116 — every key a server check reports on has a place on each form that posts to it

Phase 8 Part 2, 2026-10-19. The admin packname save reports a switched-off list on `PackNameListId`. The PackNames
tab shows that key; the Branches panel posts to the same save but had a message only for `PackNameList`, so the
refusal came back without a word (T46).

Rule: **when a server check adds a ModelState error, list every view that posts to that action and confirm each shows
that key (a validation message or a summary). A refusal the user cannot see is a bug.**

### #117 — a check made before a long write is made again where the write happens

Phase 8 Part 2, 2026-10-19. The import checks every row's name against the list, then writes for about 20 s. A name
added meanwhile by the add form was written a second time: the check had run before the write, and nothing in the
database refuses a second name. The form had the same gap, unnoticed because its window is milliseconds.

Rule: **when the time between a check and the write it protects can be long (a batch, a file, a background job),
repeat the check inside the write's transaction, and say in the design what still slips through (simultaneous
writes, snapshot isolation) unless the database enforces it.**

### #118 — a new page is looked at in the store's own theme before it is handed over

Phase 8 Part 2, 2026-10-19. The import page passed every functional check, but the developer found it read like an error
page: help, links and result in the theme's one grey, the theme's `button-1` rendered as a grey outlined box in that
context, and the result was paragraphs of plain text. It was rebuilt as numbered steps, a drop area and a result panel
with a headline per outcome (changes 115–119).

Rule: **before a new page goes into a spec, render it in the store's theme with real results (success, refusal,
partial) and look at it as the user who will use it: links distinguishable from text, one primary action, the outcome
readable from its headline. A theme class is checked where it is used, not assumed.**

### #119 — every switch a run turns off is turned back on, and the next run starts by reading the baseline

Phase 8 Part 2, 2026-10-19. Run 2's T24 switched *Employee number* off and the run went on without switching it back;
run 3 found it off when it read the store settings at its start (T0) and restored it before testing.

Rule: **a test that changes a setting restores it in the same step and says so in its result; every run's first check
reads the settings the plan lists as its baseline and fixes any drift before the first test.**

### #120 — a test step that switches something off cannot lose the means to switch it back

Phase 12, 2026-10-20. T5 switched the Subsidy feature off for store 1 through browser helpers defined in the page; a
navigation in the middle of the step reloaded the page, the helpers were gone (`adminTpl is not defined`) and the
feature stayed off until they were defined again and the restore was run by hand.

Rule: **the switch, the check and the restore of one step run in one call with the restore in a `finally`, or the
helpers they use are kept where a reload does not lose them (stored and re-loaded at the start of every call). A step
that navigates does so before it switches anything off or after it has switched it back.**

### #122 — two people clicking "at the same second" do not make a lock collide

Phase 9, 2026-10-20. T20 / T20b and T22 had two orders placed at an agreed second from two browsers. Each time the two
settlements ran one after the other (the lock is held for well under a second; the approval ran ten seconds late), so
the credit rule was proved but the "try again" refusal never happened.

Rule: **a race is proved by a test that holds the lock itself — a unit test with a fake locker, or one script that
starts both requests in parallel — never by synchronised clicks. Timed manual runs prove only what holds when the two
do not overlap.**

### #123 — measure page cost at the database with time windows, and look at the plan before blaming the code

Phase 9, 2026-10-20. The Extended Events session `CW_Phase9_Measure` with per-test time windows gave query counts and
SQL time per page. The first after-measurement looked like a regression; the cause was one view without an index on
`SubsidyPackName.PackNameId` (a scan per packname), not the C# change. SQL Server Express also evicts plans fast, so a
first run after a pause is not a fair sample.

Rule: **measure before and after with the same windows and the same data; when a number gets worse, read the slow
statement's plan before touching the code; run each measurement twice and keep the warm run.**

### #124 — "no billing address" is a state to test on purpose, not one a new account has

Phase 9, 2026-10-20. T27 meant to test a first order without a billing address of record; the packname registration
sets both billing and shipping on the new account, so the first two attempts tested nothing. Clearing the two columns by
SQL on a fresh account showed the real case (the checkout quotes with the default tax address, the order bills the
customer's own).

Rule: **before a test that depends on a missing value, read the value from the database; if the normal path always sets
it, make the state with a test-data script and say so in the result.**

### #125 — a guard probe sends a value that does nothing if the guard is wrong

Phase 11, 2026-10-21. To check how `BulkPackNames` matches the action name, a hand-built POST sent `BulkAction=delete`
(lower case), expecting a refusal. The match ignores case by design, so it was a real bulk delete and soft-deleted a
fixture packname. It was undone by SQL (soft delete, no subsidy rows or cart lines involved), but the plan had said
nothing else would be deleted.

Rule: **before sending a hand-built request to test a guard, read the code that applies it; probe with the harmless
action (Activate on an active packname) and change only the part under test. Never put a destructive action in a probe.**

### #126 — a closure promise is checked at closure, and a "swept" claim is checked by re-running the sweep

Phase 10, 2026-10-21. The documentation pass found three kinds of drift that no later phase had noticed. A closure
that **promised** a document change made none: phase 6 §4.3 said "a new `CLAUDE.md` §10.1 row at closure" for the
copied `CWInvoiceDocument.CreateSummary`; the row was never written and the section still said "three places" with
five rows in its table. A phase that **claimed a sweep closed** left most of it: phase 2d recorded the MultiStore
views' `@T()`-in-script and `alert()` sites as done, and the detector run again found 11 `@T()` sites in those views
and 23 `alert()`s in the plugin against the "two" phase 7 carried. And a reference document kept describing a
mechanism **four phases after it changed**: `subsidy-system.md` described top-ups as one accumulated row per period in
five places for a month after phase 2b made them insert-only, because the phase corrected the gap analysis and not
the reference.

Rule: **at every closure, (1) grep the phase document for "at closure", "will be", "to ride with" and "folded" and do
each promise before writing the closure record; (2) re-run every detector or sweep the phase claims to have closed and
paste the count, not the claim; (3) update the reference document that describes the mechanism changed, not only the
gap analysis row — a reference that disagrees with the code is a defect of the phase that changed the code.**

**5.00:** The 5.00 `CLAUDE.md` renumbered §10: the copied-bodies table is §10.3 (it was §10.1) and the theme section is §10.4 (it was §10.2). The 5.00 copy had this exact drift too. It was adapted from an older 4.9 text, said "three places" and was missing the `CreateSummary` and `SearchProductsAsync` rows. It was realigned on 2026-10-22.
