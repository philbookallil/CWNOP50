# Order Approval — Part 1 Spec: the engine

Status: **superseded in part by `Order_Approval_Analysis.md` §10 (2026-11-23)**; applied (2026-11-17, by Claude at Eduardo's request, from the verified scratch copy; CW.Core builds, 1,214 CW tests pass). Run on the dev site 2026-11-17 (22 of 28 pass, 1 with a note, 5 not run). Design and decisions: `Order_Approval_Analysis.md` (D1–D15). Part 2
(rule panels on branch / cost centre / department / customer, approver list, pending report, storefront status)
follows once Part 1 is applied and tested.

Every change below was applied to a scratch copy of the repository at commit `c283d04` and built there with no errors
and no new warnings; the CW.Core unit tests pass (824, filter `FullyQualifiedName~CW.Tests.Core`). The Razor views
compile at runtime, so they are proved only by opening the pages (test plan §6).

---

## 1. What Part 1 does

**An order that matches an approval rule is placed, and held.**

1. **Rules decide, at placement, from the cart.** Rules are attached to a branch (optionally with its child branches),
   a cost centre, a department, a packname or a customer (D2). A packname cart is matched line by line against each
   line's packname; a plain cart against the customer's branch (D5, D14). A rule asks for approval when it covers the
   cart and the amount is **strictly over** its threshold. The amount is goods and shipping incl tax **less subsidy**
   (D4); a threshold of 0 or none means every order. Every matching rule's approvers are asked, each once. An
   exchange replacement line is left out of both, and a cart of nothing else needs no approval (D6). Nothing happens while `Approvals` is off for the store.
2. **Payment.** When the cart needs approval, both checkouts offer only the methods in the store's
   `ApprovalPaymentMethods` (default `Payments.CheckMoneyOrder`), and placement refuses any other **before** a payment
   is taken (D3, D13). An order with nothing left to pay can always be held.
3. **Hold.** The order is placed normally — subsidy, store credit and stock are taken as for any order. On
   `OrderPlacedEvent` a `CORE_APR_OrderApproval` row is written (Pending): the row is what holds the order. Then each
   approver is e-mailed a link, the customer is told the order waits, and a staff note is added — at once for a
   packname order; for a plain order after CW has settled its store credit (`PlainOrderSettledEvent`), so the e-mails
   show the settled totals. If the row cannot be written, the order carries a note saying it is **not** held; if an
   e-mail cannot be queued, a note says so and *Send the links again* repeats it. The order stays nop-**Pending**:
   anything that would move it on through nop (payment, shipping, completion) is put back by an
   `OrderStatusChangedEvent` consumer; nop's admin *Change status*, *Add shipment* and the line / total / shipping
   method edits are refused by a filter; the storefront *Re-order* of a held order is refused (a later decline would
   put the lines back a second time).
4. **Approver's page.** The link opens the order (lines, packnames, totals with subsidy and credit) with **Accept** and
   **Decline (reason required)**. Opening the link changes nothing; deciding is a POST with an anti-forgery token, so
   mail scanners cannot decide. The page works for a guest, on a private or closed store. Only a SHA-256 hash of each
   link's token is stored; first decision wins (one lock per order). Once the order is decided, or the link replaced,
   the page shows the status only, not the order; it is served with `Referrer-Policy: no-referrer`.
5. **Accept** moves the order to Processing (D8) and e-mails the customer (D7).
6. **Decline** cancels the order through nop's `CancelOrderAsync` — stock, reward points and gift cards come back, and
   subsidy and store credit come back because the views leave cancelled orders out — then puts its lines back in the
   customer's cart with their packnames (`ReOrderAsync`), and e-mails the customer the reason. If the cancel fails the
   approval goes back to Pending; once the cancel has happened the decision stands, and a failure to refill the cart
   is noted on the order.
7. **Waiting.** An hourly task (*Process order approvals*) sends a reminder with a new link at the store's first and
   second reminder days (defaults 3 and 7) and expires the order at the expiry days (default 14): an expiry is a
   decline by the system (D9). An order cancelled or deleted while it waits ends its approval.
8. **Admin.** The order page gets an **Approval** card (status, amount, links sent) with **Approve / Decline / Send the
   links again / Change approver**, under a new permission *Approve, decline and re-send order approvals* (D10).
9. **Switch and settings.** `CWFeature.Approvals` (`MultiStoreSettings.ApprovalsEnable`), off by default, independent of
   PackName (D15), with the payment methods and the three day counts, on Store Settings and Default settings. Finishing
   an order already held never depends on the switch.

**Not in Part 1:** rule screens (Part 2; use `TestData/OrderApproval_TestData.sql`), the approver list and
pending report, the storefront approval status (the customer has the e-mails; the order shows *Pending*), pay after
approval for card orders (P2, later).

**Known limits, accepted for Part 1** (questions for you at the end of my message, not changes here):

- The link's token is in the approver's e-mail, so it is also in nop's queued e-mail body (System › Message queue,
  admins only). Anyone who can read the queue can act as that approver while the link is live. The link dies with the
  decision, a resend or a change of approver.
- An order with nothing to pay is marked Paid at placement, so nop raises `OrderPaidEvent` for it while it is held.
  Nothing in CW listens to it today; an ERP export written later must check the approval.
- nop's recurring orders (a later cycle) are not checked against the rules. Packname recurring orders are already
  refused; plain recurring orders are not used.

---

## 2. How it is built (what a reviewer needs)

| Piece | Where | Why there |
|---|---|---|
| Rule matching | `Services/Approvals/OrderApprovalRuleMatcher` (static, pure) | Tested alone (§7) |
| Rule service | `IOrderApprovalRuleService` | Depends on no placement service, so placement can ask it before paying |
| Placement → hold | `OrderApprovalPlacementContext` (scoped) carries placement's decision to `OrderPlacedEvent`, and a plain order's e-mails to `PlainOrderSettledEvent` (new, published by the plain `PlaceOrderAsync` after settlement) | nop's plain placement publishes `OrderPlacedEvent` inside `base.PlaceOrderAsync`, after the cart is gone, before its status check and before CW settles the store credit; deciding from the cart and holding on the event works for both placements |
| Approval service | `IOrderApprovalService` | Depends on `IOrderProcessingOverrideService` (cancel, re-order, release); nothing placement uses depends on it, so there is no cycle. Placement reads the approval table itself (`IRepository<OrderApproval>`) for the Re-order refusal |
| Keep it Pending | `OrderApprovalEventConsumer` (`OrderStatusChangedEvent`) | nop's own seam: `SetOrderStatusAsync` stops when a handler puts the status back |
| Admin guard | `Filters/OrderApprovalAdminGuardFilter` | nop's `ChangeOrderStatus` can set Cancelled without nop's cancel (no stock back); shipments and line edits must wait. A global filter, so it reads the approval table directly and only for the guarded actions |
| Payment filter | `Factories/CheckoutOverrideModelFactory` (nop's `ICheckoutModelFactory`) | Both checkouts build their payment step from it |
| E-mails | `OrderApprovalWorkflowMessageService` (on nop's `WorkflowMessageService`) | 6 templates, installed once each |
| Install | `OrderApprovalInstallService` (templates, task) and `OrderApprovalLocaleResources` | Keeps `CWCorePlugin` changes to four lines |
| Routes | `Infrastructure/OrderApprovalRouteProvider` | `orderapproval/{token}`, `/accept`, `/decline` |
| Tables | `CORE_APR_Approver`, `CORE_APR_ApprovalRule`, `CORE_APR_ApprovalRuleApprover`, `CORE_APR_OrderApproval` (unique `OrderId`, FK cascade), `CORE_APR_OrderApprovalRequest` (unique `TokenHash`) | In `SchemaMigration` for new installs; `OrderApprovalMigration` (Update) for this database |

**Changed in existing files** (23): the feature switch (`CWFeature`, `CWFeatureSet`, `CWFeatureService`), the
settings (`MultiStoreSettings`, its model, validator, factory, controller and both views), the tables
(`CWNameCompatibility`, `SchemaMigration`, `MigrationVersions`), `NopStartup`, `CWCorePlugin`,
`CWPermissionConfigManager`, the token groups (`CWTokenGroupNames`, `MessageTokenProviderOverride.GetTokenGroups`),
placement (`OrderProcessingOverrideService`, its interface), CW's copy of the admin order page (`Order/Edit.cshtml`),
the `.csproj` and `plugin.json`.

**`CWFeatureSet` gains a sixth parameter.** Every `new CWFeatureSet(...)` changes: two in `CWFeatureService` (in this
spec) and one in the tests (`SubsidiesMenuItemTests`, which I update).

**`OrderProcessingOverrideService` gets three constructor parameters at the end** (`IOrderApprovalRuleService`,
`OrderApprovalPlacementContext`, `IRepository<OrderApproval>`). Nothing constructs it by hand.

**Independent review.** A reviewer who had not seen the design read the built code against nop 5.00. It confirmed
the hold timing in both placements, the status revert (no recursion), the subsidy / credit / stock release, the
re-order in an approver's (guest) request, no DI cycles, the locking and the views. Its eight findings are fixed in
this spec: mixed exchange carts, decision failures after the cancel, a failed hold, plain-order e-mails before
settlement, the global filter's cost, the decided order shown on the link, a wrong code comment, and Re-order / line
edits of a held order.

---

## 3. Labels

- **ADD FILE** — a new file, given whole. Create it at the path given (no byte-order mark needed).
- **REPLACE** — FIND the lines quoted (at the line numbers stated, counted in the file as it stands when the change is
  reached) and replace them with REPLACE WITH. Every FIND and REPLACE is whole lines. When a file takes several blocks
  they are listed **bottom to top**; apply them in that order and every FIND stays at the lines it states.

Changes 1–47 are the new files (ADD FILE), 48–70 the edits (REPLACE). None depends on another to be applied; the build needs all of them.

---

## 4. After applying

1. Build the solution (no errors expected; Razor views are compiled when first opened).
2. Empty `Presentation\Nop.Web\wwwroot\bundles` (a new `.js` file).
3. Start the site. `plugin.json` is now `2026_11_17_DATA_0.2`, so nop runs CW.Core's update at start: it creates the
   five `CORE_APR_*` tables (`OrderApprovalMigration`), installs the resources, the six e-mail templates, the task and
   the permission.
4. I add the tests (`Tests/CW.Tests/Core/Services/Approvals/`, 3 files, and the one-line fix in
   `SubsidiesMenuItemTests`) and run the suite.

---

## 5. Test data

`CWPlans/TestData/OrderApproval_TestData.sql`: set the store, a branch, optionally a customer, and an approver
e-mail you can read; it creates the approver, a branch rule (with child branches, threshold 100) and optionally a
customer rule (every order). Re-runnable. The links arrive by e-mail; with no SMTP set up, read them in the queued
e-mail's body (System › Message queue).

---

## 6. Test plan

Side effects: orders are placed and cancelled, carts change, e-mails are queued, an approver row and rules are
created. Nothing is deleted.

| # | Test | Expected |
|---|---|---|
| T1 | Update the plugin; check Schedule tasks, Message templates, ACL permissions, the five tables | Task *Process order approvals* (hourly, enabled); six `OrderApproval.*` templates; permission *Approve, decline and re-send order approvals* for Administrators; tables exist |
| T2 | Store Settings and Default settings: the **Order approvals** card | The switch; the other four rows hide while it is off; payment methods list shows installed methods; negative days refused; save and reload keeps the values; override checkboxes work per store |
| T3 | **Switch off**, rules in place, order over the threshold | Placed as today: all payment methods offered, no hold, no e-mails, no Approval card |
| T4 | Switch on. Packname order **under** the threshold | No hold |
| T5 | Packname order **over** the threshold, paid on account (Check / Money order) | Payment step offers only the listed methods. Order placed, **Pending**; Approval card shows *Waiting for approval*, the approver and one live link; note *waiting for approval by …*; e-mails queued: approver request (with link), customer *waiting*; subsidy and store credit shown as spent |
| T6 | Same, with a card method selected anyway (e.g. post the old selection) | Placement refused with *needs approval and cannot be paid with …*; nothing charged |
| T7 | Order fully covered by subsidy over a threshold-0 customer rule | Held although nothing to pay; status stays Pending (not Processing) |
| T8 | Open the link as a guest (private window), on a store that requires sign-in | Review page: lines with packnames, totals; Accept and Decline. Opening it twice changes nothing |
| T9 | **Accept** | Page says approved; order **Processing**; note *approved by …*; customer e-mail *approved*; link now says already decided |
| T10 | New held order; **Decline** without a reason, then with one | First refused (*please say why*); then order **Cancelled**, lines back in the ordering customer's cart with their packnames, subsidy and store credit available again (bar / My Subsidies / Credits for me), stock returned; customer e-mail with the reason |
| T11 | Held order: admin **Change status** to Processing; **Add shipment** | Both refused with *waiting for approval*; order unchanged |
| T12 | Held order: admin **Mark as paid** | Payment status Paid; order **stays Pending** |
| T13 | Approval card: **Send the links again** | Old link says replaced; new e-mail with a new link that works |
| T14 | Approval card: **Change approver** (new name and e-mail) | Approver created; old links replaced; new approver's link works |
| T15 | Approval card: **Approve** / **Decline** as admin | As T9 / T10, *decided by …(admin…)* |
| T16 | Two browsers on the same link: Accept in both at once | One wins; the other says already decided |
| T17 | Held order: admin **Cancel order** | Approval *Cancelled*; links say already decided |
| T18 | Reminders and expiry: set the store's days to 0 / 0 / 1 (expiry 1 day), back-date `RequestedOnUtc` of a held order by 2 days in SQL, run the task (*Run now*) | Order expired: cancelled, lines back in cart, customer e-mail *not approved in time*. With reminder days 1 / 2 and expiry 0: reminder #1 with a new link; the old link still works |
| T19 | Plain (non-packname) order by a customer whose branch has the rule | Held the same way |
| T20 | Exchange replacement checkout by a customer covered by a rule | Not held |
| T21 | Switch **off** while an order is held: accept it from its link | Works (finishing is never gated) |
| T22 | Approver link with a made-up token | *This approval link is not valid*; no order shown |
| T23 | Open a decided order's link again | Status and who decided only; no lines or totals |
| T24 | Held order: storefront **Re-order** from the order page | Refused with *waiting for approval and cannot be ordered again*; cart unchanged |
| T25 | Held plain order: admin edit of a line, the totals or the shipping method | Refused with *waiting for approval* |
| T26 | Exchange replacement **plus** new goods over the threshold in one cart | Held (the replacement line is left out of the amount, the new goods are judged) |
| T27 | Plain order part-paid by store credit, held | The approver's and the customer's e-mails show the totals with the store credit taken off |

---

## 7. Unit tests (added after the code is applied)

`Tests/CW.Tests/Core/Services/Approvals/`: `OrderApprovalRuleMatcherTests` (scopes, child branches, thresholds incl.
the exact-threshold case, approvers unioned, rules without approvers ignored, branch tree with a loop),
`OrderApprovalTokenHelperTests` (URL-safe 32-byte tokens, 64-hex hash, empty tokens),
`OrderApprovalPlacementContextTests` (decision taken once and per customer, the switch, the setting defaults); and
`SubsidiesMenuItemTests` (sixth `CWFeatureSet` argument). 824 CW.Core tests pass with them in the scratch copy.

---

## 8. Documents to update after the apply (mine)

`CLAUDE.md` §13.4 (registrations), §14.1 (`CORE_APR_*`), §15.3 (the approval settings), §18 (permission), §20/§21
(task); `Feature_Toggles_Reference.md` (a sixth feature: §1 table, §2.1–2.3); `Order_Approval_Analysis.md` (Part 1
applied).

---

## 9. The changes
### 1. ADD FILE `Plugins/Nop.Plugin.CW.Core/Areas/Admin/Components/OrderApprovalAdminViewComponent.cs`

New file, 53 lines. Depends on: nothing outside this change list.

````csharp
using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Plugin.CW.Core.Areas.Admin.Factories;
using Nop.Web.Framework.Components;

namespace Nop.Plugin.CW.Core.Areas.Admin.Components;

/// <summary>
/// The approval card of the admin order page: where the order's approval stands, the links sent, and the admin's
/// decide / resend / change approver buttons while it waits
/// </summary>
/// <remarks>
/// Rendered by CW.Core's copy of nop's order page, as the first card. Nothing for an order that never needed approval,
/// and nothing for a vendor. Not gated on the Approvals switch: an approval that exists is shown.
/// </remarks>
[ViewComponent(Name = "OrderApprovalAdmin")]
public class OrderApprovalAdminViewComponent : NopViewComponent
{
    #region Fields

    protected readonly IOrderApprovalAdminModelFactory _orderApprovalAdminModelFactory;
    protected readonly IWorkContext _workContext;

    #endregion

    #region Ctor

    public OrderApprovalAdminViewComponent(IOrderApprovalAdminModelFactory orderApprovalAdminModelFactory,
        IWorkContext workContext)
    {
        _orderApprovalAdminModelFactory = orderApprovalAdminModelFactory;
        _workContext = workContext;
    }

    #endregion

    #region Methods

    public async Task<IViewComponentResult> InvokeAsync(int orderId)
    {
        if (await _workContext.GetCurrentVendorAsync() != null)
            return Content(string.Empty);

        var model = await _orderApprovalAdminModelFactory.PrepareOrderApprovalAdminModelAsync(orderId);

        if (model == null)
            return Content(string.Empty);

        return View("~/Plugins/CW.Core/Areas/Admin/Views/Shared/Components/OrderApprovalAdmin/Default.cshtml", model);
    }

    #endregion
}
````

### 2. ADD FILE `Plugins/Nop.Plugin.CW.Core/Areas/Admin/Controllers/OrderApprovalAdminController.cs`

New file, 97 lines. Depends on: nothing outside this change list.

````csharp
using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Plugin.CW.Core.Services.Approvals;
using Nop.Plugin.CW.Core.Services.Security;
using Nop.Services.Localization;
using Nop.Web.Areas.Admin.Controllers;
using Nop.Web.Framework.Mvc.Filters;

namespace Nop.Plugin.CW.Core.Areas.Admin.Controllers;

/// <summary>
/// The admin's actions on a held order, posted by the approval card of the order page
/// </summary>
/// <remarks>
/// The card sits inside nop's order form, so its buttons post here by AJAX rather than with forms of their own. Each
/// action answers <c>{ success, message }</c> and the page reloads on success. Not gated on the Approvals switch: an
/// order already waiting is finished whatever the switch says now.
/// </remarks>
public class OrderApprovalAdminController : BaseAdminController
{
    #region Fields

    protected readonly ILocalizationService _localizationService;
    protected readonly IOrderApprovalService _orderApprovalService;
    protected readonly IWorkContext _workContext;

    #endregion

    #region Ctor

    public OrderApprovalAdminController(ILocalizationService localizationService,
        IOrderApprovalService orderApprovalService,
        IWorkContext workContext)
    {
        _localizationService = localizationService;
        _orderApprovalService = orderApprovalService;
        _workContext = workContext;
    }

    #endregion

    #region Utilities

    /// <summary>
    /// The answer the card reads: success, and the message to show
    /// </summary>
    protected virtual async Task<IActionResult> AnswerAsync(OrderApprovalResult result)
    {
        return Json(new
        {
            success = result == OrderApprovalResult.Done,
            message = await _localizationService.GetResourceAsync($"Plugins.CW.Core.Admin.OrderApproval.Result.{result}")
        });
    }

    #endregion

    #region Methods

    [HttpPost]
    [CheckPermission(CWPermissionConfigManager.ADMIN_MANAGE_ORDER_APPROVALS)]
    public virtual async Task<IActionResult> Approve(int orderId)
    {
        var admin = await _workContext.GetCurrentCustomerAsync();

        return await AnswerAsync(await _orderApprovalService.AcceptByAdminAsync(orderId, admin));
    }

    [HttpPost]
    [CheckPermission(CWPermissionConfigManager.ADMIN_MANAGE_ORDER_APPROVALS)]
    public virtual async Task<IActionResult> Decline(int orderId, string reason)
    {
        var admin = await _workContext.GetCurrentCustomerAsync();

        return await AnswerAsync(await _orderApprovalService.DeclineByAdminAsync(orderId, admin, reason));
    }

    [HttpPost]
    [CheckPermission(CWPermissionConfigManager.ADMIN_MANAGE_ORDER_APPROVALS)]
    public virtual async Task<IActionResult> Resend(int orderId)
    {
        var admin = await _workContext.GetCurrentCustomerAsync();

        return await AnswerAsync(await _orderApprovalService.ResendAsync(orderId, admin));
    }

    [HttpPost]
    [CheckPermission(CWPermissionConfigManager.ADMIN_MANAGE_ORDER_APPROVALS)]
    public virtual async Task<IActionResult> ChangeApprover(int orderId, string name, string email)
    {
        var admin = await _workContext.GetCurrentCustomerAsync();

        return await AnswerAsync(await _orderApprovalService.ChangeApproverAsync(orderId, admin, name, email));
    }

    #endregion
}
````

### 3. ADD FILE `Plugins/Nop.Plugin.CW.Core/Areas/Admin/Factories/IOrderApprovalAdminModelFactory.cs`

New file, 19 lines. Depends on: nothing outside this change list.

````csharp
using Nop.Plugin.CW.Core.Areas.Admin.Models.Approvals;

namespace Nop.Plugin.CW.Core.Areas.Admin.Factories;

/// <summary>
/// Prepares the approval card of the admin order page
/// </summary>
public interface IOrderApprovalAdminModelFactory
{
    /// <summary>
    /// Prepares the approval card of an order
    /// </summary>
    /// <param name="orderId">Order identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the model; null when the order never needed approval
    /// </returns>
    Task<OrderApprovalAdminModel> PrepareOrderApprovalAdminModelAsync(int orderId);
}
````

### 4. ADD FILE `Plugins/Nop.Plugin.CW.Core/Areas/Admin/Factories/OrderApprovalAdminModelFactory.cs`

New file, 100 lines. Depends on: nothing outside this change list.

````csharp
using Nop.Core.Domain.Directory;
using Nop.Plugin.CW.Core.Areas.Admin.Models.Approvals;
using Nop.Plugin.CW.Core.Domain.Approvals;
using Nop.Plugin.CW.Core.Services.Approvals;
using Nop.Plugin.CW.Core.Services.Security;
using Nop.Services.Catalog;
using Nop.Services.Directory;
using Nop.Services.Helpers;
using Nop.Services.Localization;
using Nop.Services.Security;

namespace Nop.Plugin.CW.Core.Areas.Admin.Factories;

/// <summary>
/// Prepares the approval card of the admin order page
/// </summary>
public class OrderApprovalAdminModelFactory : IOrderApprovalAdminModelFactory
{
    #region Fields

    protected readonly CurrencySettings _currencySettings;
    protected readonly ICurrencyService _currencyService;
    protected readonly IDateTimeHelper _dateTimeHelper;
    protected readonly ILocalizationService _localizationService;
    protected readonly IOrderApprovalService _orderApprovalService;
    protected readonly IPermissionService _permissionService;
    protected readonly IPriceFormatter _priceFormatter;

    #endregion

    #region Ctor

    public OrderApprovalAdminModelFactory(CurrencySettings currencySettings,
        ICurrencyService currencyService,
        IDateTimeHelper dateTimeHelper,
        ILocalizationService localizationService,
        IOrderApprovalService orderApprovalService,
        IPermissionService permissionService,
        IPriceFormatter priceFormatter)
    {
        _currencySettings = currencySettings;
        _currencyService = currencyService;
        _dateTimeHelper = dateTimeHelper;
        _localizationService = localizationService;
        _orderApprovalService = orderApprovalService;
        _permissionService = permissionService;
        _priceFormatter = priceFormatter;
    }

    #endregion

    #region Utilities

    protected virtual async Task<string> FormatDateAsync(DateTime? utc)
        => utc.HasValue ? (await _dateTimeHelper.ConvertToUserTimeAsync(utc.Value, DateTimeKind.Utc)).ToString("g") : null;

    #endregion

    #region Methods

    public virtual async Task<OrderApprovalAdminModel> PrepareOrderApprovalAdminModelAsync(int orderId)
    {
        var approval = await _orderApprovalService.GetOrderApprovalByOrderIdAsync(orderId);

        if (approval == null)
            return null;

        //the thresholds and the measured amount are in the primary store currency
        var primaryCurrency = await _currencyService.GetCurrencyByIdAsync(_currencySettings.PrimaryStoreCurrencyId);

        var model = new OrderApprovalAdminModel
        {
            OrderId = orderId,
            Status = await _localizationService.GetResourceAsync($"Plugins.CW.Core.OrderApproval.Status.{approval.Status}"),
            IsPending = approval.Status == OrderApprovalStatus.Pending,
            CanManage = await _permissionService.AuthorizeAsync(CWPermissionConfigManager.ADMIN_MANAGE_ORDER_APPROVALS),
            MeasuredAmount = await _priceFormatter.FormatPriceAsync(approval.MeasuredAmount, true, primaryCurrency),
            RequestedOn = await FormatDateAsync(approval.RequestedOnUtc),
            LastReminderNumber = approval.LastReminderNumber,
            DecidedBy = approval.DecidedBy,
            DecidedOn = await FormatDateAsync(approval.DecidedOnUtc),
            DecisionComment = approval.DecisionComment
        };

        foreach (var request in await _orderApprovalService.GetRequestsAsync(approval.Id))
        {
            model.Requests.Add(new OrderApprovalRequestAdminModel
            {
                ApproverName = request.ApproverName,
                ApproverEmail = request.ApproverEmail,
                SentOn = await FormatDateAsync(request.SentOnUtc),
                IsLive = request.RevokedOnUtc == null && model.IsPending
            });
        }

        return model;
    }

    #endregion
}
````

### 5. ADD FILE `Plugins/Nop.Plugin.CW.Core/Areas/Admin/Models/Approvals/OrderApprovalAdminModel.cs`

New file, 54 lines. Depends on: nothing outside this change list.

````csharp
using Nop.Web.Framework.Models;

namespace Nop.Plugin.CW.Core.Areas.Admin.Models.Approvals;

/// <summary>
/// The approval card of the admin order page
/// </summary>
public record OrderApprovalAdminModel : BaseNopModel
{
    public int OrderId { get; set; }

    public string Status { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the order is held
    /// </summary>
    public bool IsPending { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the admin may decide, resend and change the approver
    /// </summary>
    public bool CanManage { get; set; }

    public string MeasuredAmount { get; set; }

    public string RequestedOn { get; set; }

    public int LastReminderNumber { get; set; }

    public string DecidedBy { get; set; }

    public string DecidedOn { get; set; }

    public string DecisionComment { get; set; }

    public IList<OrderApprovalRequestAdminModel> Requests { get; set; } = [];
}

/// <summary>
/// One approval link sent for an order
/// </summary>
public record OrderApprovalRequestAdminModel : BaseNopModel
{
    public string ApproverName { get; set; }

    public string ApproverEmail { get; set; }

    public string SentOn { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the link still works
    /// </summary>
    public bool IsLive { get; set; }
}
````

### 6. ADD FILE `Plugins/Nop.Plugin.CW.Core/Areas/Admin/Views/Shared/Components/OrderApprovalAdmin/Default.cshtml`

New file, 106 lines. Depends on: nothing outside this change list.

````cshtml
@model Nop.Plugin.CW.Core.Areas.Admin.Models.Approvals.OrderApprovalAdminModel

@{
    NopHtml.AddScriptParts(ResourceLocation.Footer, "~/Plugins/CW.Core/Content/Admin/Approvals/admin.order.approval.js");

    const string hideApprovalBlockAttributeName = "OrderPage.HideApprovalBlock";
    var hideApprovalBlock = await genericAttributeService.GetAttributeAsync<bool>(await workContext.GetCurrentCustomerAsync(), hideApprovalBlockAttributeName);
}

<nop-card asp-name="order-approval" asp-icon="fas fa-user-check" asp-title="@T("Plugins.CW.Core.Admin.OrderApproval.Title")" asp-hide-block-attribute-name="@hideApprovalBlockAttributeName" asp-hide="@hideApprovalBlock" asp-advanced="false">
    <div class="card-body cw-order-approval">
        <div class="form-group row">
            <div class="col-md-3"><label class="col-form-label">@T("Plugins.CW.Core.Admin.OrderApproval.Status")</label></div>
            <div class="col-md-9"><div class="form-text-row">@Model.Status</div></div>
        </div>
        <div class="form-group row">
            <div class="col-md-3"><label class="col-form-label">@T("Plugins.CW.Core.Admin.OrderApproval.MeasuredAmount")</label></div>
            <div class="col-md-9"><div class="form-text-row">@Model.MeasuredAmount</div></div>
        </div>
        <div class="form-group row">
            <div class="col-md-3"><label class="col-form-label">@T("Plugins.CW.Core.Admin.OrderApproval.RequestedOn")</label></div>
            <div class="col-md-9"><div class="form-text-row">@Model.RequestedOn</div></div>
        </div>
        @if (Model.IsPending)
        {
            <div class="form-group row">
                <div class="col-md-3"><label class="col-form-label">@T("Plugins.CW.Core.Admin.OrderApproval.Reminders")</label></div>
                <div class="col-md-9"><div class="form-text-row">@Model.LastReminderNumber</div></div>
            </div>
        }
        else
        {
            <div class="form-group row">
                <div class="col-md-3"><label class="col-form-label">@T("Plugins.CW.Core.Admin.OrderApproval.DecidedBy")</label></div>
                <div class="col-md-9"><div class="form-text-row">@Model.DecidedBy @(string.IsNullOrEmpty(Model.DecidedOn) ? "" : $"({Model.DecidedOn})")</div></div>
            </div>
            @if (!string.IsNullOrEmpty(Model.DecisionComment))
            {
                <div class="form-group row">
                    <div class="col-md-3"><label class="col-form-label">@T("Plugins.CW.Core.Admin.OrderApproval.DecisionComment")</label></div>
                    <div class="col-md-9"><div class="form-text-row">@Model.DecisionComment</div></div>
                </div>
            }
        }

        <table class="table table-bordered table-sm">
            <thead>
                <tr>
                    <th>@T("Plugins.CW.Core.Admin.OrderApproval.Requests.Approver")</th>
                    <th>@T("Plugins.CW.Core.Admin.OrderApproval.Requests.Email")</th>
                    <th>@T("Plugins.CW.Core.Admin.OrderApproval.Requests.SentOn")</th>
                    <th>@T("Plugins.CW.Core.Admin.OrderApproval.Requests.Live")</th>
                </tr>
            </thead>
            <tbody>
                @foreach (var request in Model.Requests)
                {
                    <tr>
                        <td>@request.ApproverName</td>
                        <td>@request.ApproverEmail</td>
                        <td>@request.SentOn</td>
                        <td>@(request.IsLive ? T("Admin.Common.Yes").Text : T("Admin.Common.No").Text)</td>
                    </tr>
                }
            </tbody>
        </table>

        @if (Model.IsPending && Model.CanManage)
        {
            @*No name attributes: these inputs sit inside nop's order form, and must not be posted with it*@
            <div class="form-group row">
                <div class="col-md-3"><label class="col-form-label" for="cw-approval-reason">@T("Plugins.CW.Core.Admin.OrderApproval.Reason")</label></div>
                <div class="col-md-9"><textarea id="cw-approval-reason" class="form-control" rows="2"></textarea></div>
            </div>
            <div class="form-group row">
                <div class="col-md-3"></div>
                <div class="col-md-9">
                    <button type="button" class="btn btn-success cw-approval-approve">@T("Plugins.CW.Core.Admin.OrderApproval.Approve")</button>
                    <button type="button" class="btn btn-danger cw-approval-decline">@T("Plugins.CW.Core.Admin.OrderApproval.Decline")</button>
                    <button type="button" class="btn btn-default cw-approval-resend">@T("Plugins.CW.Core.Admin.OrderApproval.Resend")</button>
                </div>
            </div>
            <div class="form-group row">
                <div class="col-md-3"><label class="col-form-label">@T("Plugins.CW.Core.Admin.OrderApproval.ChangeApprover")</label></div>
                <div class="col-md-9 form-inline">
                    <input id="cw-approval-name" type="text" class="form-control mr-2" placeholder="@T("Plugins.CW.Core.Admin.OrderApproval.ApproverName")" />
                    <input id="cw-approval-email" type="email" class="form-control mr-2" placeholder="@T("Plugins.CW.Core.Admin.OrderApproval.ApproverEmail")" />
                    <button type="button" class="btn btn-primary cw-approval-change">@T("Plugins.CW.Core.Admin.OrderApproval.ChangeApprover.Button")</button>
                </div>
            </div>

            <div class="cw-order-approval-config"
                 data-order-id="@Model.OrderId"
                 data-approve-url="@Url.Action("Approve", "OrderApprovalAdmin", new { area = AreaNames.ADMIN })"
                 data-decline-url="@Url.Action("Decline", "OrderApprovalAdmin", new { area = AreaNames.ADMIN })"
                 data-resend-url="@Url.Action("Resend", "OrderApprovalAdmin", new { area = AreaNames.ADMIN })"
                 data-change-url="@Url.Action("ChangeApprover", "OrderApprovalAdmin", new { area = AreaNames.ADMIN })"
                 data-confirm-approve="@T("Plugins.CW.Core.Admin.OrderApproval.Confirm.Approve")"
                 data-confirm-decline="@T("Plugins.CW.Core.Admin.OrderApproval.Confirm.Decline")"
                 data-confirm-resend="@T("Plugins.CW.Core.Admin.OrderApproval.Confirm.Resend")"
                 data-confirm-change="@T("Plugins.CW.Core.Admin.OrderApproval.Confirm.Change")"
                 data-error-text="@T("Plugins.CW.Core.Admin.OrderApproval.Result.Failed")"
                 style="display: none;"></div>
        }
    </div>
</nop-card>
````

### 7. ADD FILE `Plugins/Nop.Plugin.CW.Core/Content/Admin/Approvals/admin.order.approval.js`

New file, 67 lines. Depends on: nothing outside this change list.

````javascript
/*
 * The approval card of the admin order page: approve, decline, resend and change approver.
 *
 * The card sits inside nop's order form, so its buttons post by AJAX instead of with forms of their own. The
 * actions answer an anonymous object, so its members arrive lowercase: { success, message }. On success the page
 * reloads, which shows the order's new status and notes.
 *
 * The handlers are delegated from the card, which is rendered with the page and never replaced, and bound once
 * (the .off first keeps it so if the script is ever loaded twice).
 */
(function () {
    'use strict';

    $(function () {
        var $card = $('.cw-order-approval').off('.cwapproval');
        var $cfg = $card.find('.cw-order-approval-config');

        if ($cfg.length === 0)
            return;

        var orderId = $cfg.data('orderId');

        function post(url, data, confirmText) {
            if (!confirm(confirmText))
                return;

            data.orderId = orderId;

            $.ajax({
                cache: false,
                type: 'POST',
                url: url,
                data: addAntiForgeryToken(data),
                success: function (answer) {
                    if (answer && answer.success) {
                        location.reload();
                        return;
                    }

                    display_nop_error({ error: (answer && answer.message) || $cfg.data('errorText') });
                },
                error: function () {
                    display_nop_error({ error: $cfg.data('errorText') });
                }
            });
        }

        $card.on('click.cwapproval', '.cw-approval-approve', function () {
            post($cfg.data('approveUrl'), {}, $cfg.data('confirmApprove'));
        });

        $card.on('click.cwapproval', '.cw-approval-decline', function () {
            post($cfg.data('declineUrl'), { reason: $('#cw-approval-reason').val() }, $cfg.data('confirmDecline'));
        });

        $card.on('click.cwapproval', '.cw-approval-resend', function () {
            post($cfg.data('resendUrl'), {}, $cfg.data('confirmResend'));
        });

        $card.on('click.cwapproval', '.cw-approval-change', function () {
            post($cfg.data('changeUrl'), {
                name: $('#cw-approval-name').val(),
                email: $('#cw-approval-email').val()
            }, $cfg.data('confirmChange'));
        });
    });
})();
````

### 8. ADD FILE `Plugins/Nop.Plugin.CW.Core/Controllers/OrderApprovalController.cs`

New file, 97 lines. Depends on: nothing outside this change list.

````csharp
using Microsoft.AspNetCore.Mvc;
using Nop.Plugin.CW.Core.Factories;
using Nop.Plugin.CW.Core.Services.Approvals;
using Nop.Web.Controllers;
using Nop.Web.Framework.Mvc.Filters;

namespace Nop.Plugin.CW.Core.Controllers;

/// <summary>
/// The page an approver opens from the approval e-mail
/// </summary>
/// <remarks>
/// <para>
/// The approver usually has no account, so the link's token is the only credential, and the page is open even when the
/// store requires sign-in or is closed. Opening the link only shows the order: accepting and declining are POSTs with
/// an anti-forgery token, so a mail scanner that follows links cannot decide an order.
/// </para>
/// <para>
/// Not gated on the Approvals switch: an order already waiting is finished whatever the switch says now.
/// </para>
/// </remarks>
[AutoValidateAntiforgeryToken]
public class OrderApprovalController : BasePublicController
{
    #region Fields

    protected readonly IOrderApprovalModelFactory _orderApprovalModelFactory;
    protected readonly IOrderApprovalService _orderApprovalService;

    #endregion

    #region Ctor

    public OrderApprovalController(IOrderApprovalModelFactory orderApprovalModelFactory,
        IOrderApprovalService orderApprovalService)
    {
        _orderApprovalModelFactory = orderApprovalModelFactory;
        _orderApprovalService = orderApprovalService;
    }

    #endregion

    #region Utilities

    /// <summary>
    /// The review page: the link's token is in its address, so the browser is told not to pass the address on to any
    /// page the approver goes to from here
    /// </summary>
    protected virtual IActionResult ReviewView(object model)
    {
        Response.Headers["Referrer-Policy"] = "no-referrer";

        return View("~/Plugins/CW.Core/Views/OrderApproval/Review.cshtml", model);
    }

    #endregion

    #region Methods

    //available even when navigation is not allowed
    [CheckAccessPublicStore(ignore: true)]
    //available even when a store is closed
    [CheckAccessClosedStore(ignore: true)]
    public virtual async Task<IActionResult> Review(string token)
    {
        var model = await _orderApprovalModelFactory.PrepareReviewModelAsync(token);

        return ReviewView(model);
    }

    [HttpPost]
    [CheckAccessPublicStore(ignore: true)]
    [CheckAccessClosedStore(ignore: true)]
    public virtual async Task<IActionResult> Accept(string token)
    {
        var result = await _orderApprovalService.AcceptByTokenAsync(token);
        var model = await _orderApprovalModelFactory.PrepareReviewModelAsync(token, result);

        return ReviewView(model);
    }

    [HttpPost]
    [CheckAccessPublicStore(ignore: true)]
    [CheckAccessClosedStore(ignore: true)]
    public virtual async Task<IActionResult> Decline(string token, string declineReason)
    {
        var result = await _orderApprovalService.DeclineByTokenAsync(token, declineReason);
        var model = await _orderApprovalModelFactory.PrepareReviewModelAsync(token, result);

        //a decline refused for its missing reason keeps what was typed (nothing) and the form open
        model.DeclineReason = result == OrderApprovalResult.ReasonRequired ? declineReason : null;

        return ReviewView(model);
    }

    #endregion
}
````

### 9. ADD FILE `Plugins/Nop.Plugin.CW.Core/Data/Mapping/Approvals/ApprovalRuleApproverBuilder.cs`

New file, 17 lines. Depends on: nothing outside this change list.

````csharp
using System.Data;
using FluentMigrator.Builders.Create.Table;
using Nop.Data.Extensions;
using Nop.Data.Mapping.Builders;
using Nop.Plugin.CW.Core.Domain.Approvals;

namespace Nop.Plugin.CW.Core.Data.Mapping.Approvals;

public class ApprovalRuleApproverBuilder : NopEntityBuilder<ApprovalRuleApprover>
{
    public override void MapEntity(CreateTableExpressionBuilder table)
    {
        table.WithColumn(nameof(ApprovalRuleApprover.Id)).AsInt32().PrimaryKey().Identity()
             .WithColumn(nameof(ApprovalRuleApprover.ApprovalRuleId)).AsInt32().ForeignKey<ApprovalRule>(onDelete: Rule.Cascade).NotNullable()
             .WithColumn(nameof(ApprovalRuleApprover.ApproverId)).AsInt32().ForeignKey<Approver>(onDelete: Rule.None).NotNullable();
    }
}
````

### 10. ADD FILE `Plugins/Nop.Plugin.CW.Core/Data/Mapping/Approvals/ApprovalRuleBuilder.cs`

New file, 21 lines. Depends on: nothing outside this change list.

````csharp
using FluentMigrator.Builders.Create.Table;
using Nop.Data.Mapping.Builders;
using Nop.Plugin.CW.Core.Domain.Approvals;

namespace Nop.Plugin.CW.Core.Data.Mapping.Approvals;

public class ApprovalRuleBuilder : NopEntityBuilder<ApprovalRule>
{
    public override void MapEntity(CreateTableExpressionBuilder table)
    {
        table.WithColumn(nameof(ApprovalRule.Id)).AsInt32().PrimaryKey().Identity()
             .WithColumn(nameof(ApprovalRule.StoreId)).AsInt32().NotNullable()
             .WithColumn(nameof(ApprovalRule.ScopeTypeId)).AsInt32().NotNullable()
             .WithColumn(nameof(ApprovalRule.ScopeId)).AsInt32().NotNullable()
             .WithColumn(nameof(ApprovalRule.IncludeChildBranches)).AsBoolean().NotNullable()
             .WithColumn(nameof(ApprovalRule.Threshold)).AsDecimal(18, 4).Nullable()
             .WithColumn(nameof(ApprovalRule.Active)).AsBoolean().NotNullable()
             .WithColumn(nameof(ApprovalRule.CreatedOnUtc)).AsDateTime2().NotNullable()
             .WithColumn(nameof(ApprovalRule.UpdatedOnUtc)).AsDateTime2().NotNullable();
    }
}
````

### 11. ADD FILE `Plugins/Nop.Plugin.CW.Core/Data/Mapping/Approvals/ApproverBuilder.cs`

New file, 19 lines. Depends on: nothing outside this change list.

````csharp
using FluentMigrator.Builders.Create.Table;
using Nop.Data.Mapping.Builders;
using Nop.Plugin.CW.Core.Domain.Approvals;

namespace Nop.Plugin.CW.Core.Data.Mapping.Approvals;

public class ApproverBuilder : NopEntityBuilder<Approver>
{
    public override void MapEntity(CreateTableExpressionBuilder table)
    {
        table.WithColumn(nameof(Approver.Id)).AsInt32().PrimaryKey().Identity()
             .WithColumn(nameof(Approver.StoreId)).AsInt32().NotNullable()
             .WithColumn(nameof(Approver.Name)).AsString(400).NotNullable()
             .WithColumn(nameof(Approver.Email)).AsString(255).NotNullable()
             .WithColumn(nameof(Approver.Active)).AsBoolean().NotNullable()
             .WithColumn(nameof(Approver.Deleted)).AsBoolean().NotNullable()
             .WithColumn(nameof(Approver.CreatedOnUtc)).AsDateTime2().NotNullable();
    }
}
````

### 12. ADD FILE `Plugins/Nop.Plugin.CW.Core/Data/Mapping/Approvals/OrderApprovalBuilder.cs`

New file, 30 lines. Depends on: nothing outside this change list.

````csharp
using System.Data;
using FluentMigrator.Builders.Create.Table;
using Nop.Core.Domain.Orders;
using Nop.Data.Mapping;
using Nop.Data.Mapping.Builders;
using Nop.Plugin.CW.Core.Domain.Approvals;

namespace Nop.Plugin.CW.Core.Data.Mapping.Approvals;

public class OrderApprovalBuilder : NopEntityBuilder<OrderApproval>
{
    public override void MapEntity(CreateTableExpressionBuilder table)
    {
        //One approval per order. The unique index also serves the foreign key, so the key is declared without the
        //extra index nop's ForeignKey<T>() adds, as OrderSettlementBuilder does.
        table.WithColumn(nameof(OrderApproval.Id)).AsInt32().PrimaryKey().Identity()
             .WithColumn(nameof(OrderApproval.OrderId)).AsInt32().NotNullable().Unique()
                .ForeignKey(NameCompatibilityManager.GetTableName(typeof(Order)), nameof(Order.Id)).OnDelete(Rule.Cascade)
             .WithColumn(nameof(OrderApproval.StatusId)).AsInt32().NotNullable()
             .WithColumn(nameof(OrderApproval.MeasuredAmount)).AsDecimal(18, 4).NotNullable()
             .WithColumn(nameof(OrderApproval.RequestedOnUtc)).AsDateTime2().NotNullable()
             .WithColumn(nameof(OrderApproval.LastReminderNumber)).AsInt32().NotNullable()
             .WithColumn(nameof(OrderApproval.DecidedBy)).AsString(500).Nullable()
             .WithColumn(nameof(OrderApproval.DecidedByApproverId)).AsInt32().Nullable()
             .WithColumn(nameof(OrderApproval.DecidedByCustomerId)).AsInt32().Nullable()
             .WithColumn(nameof(OrderApproval.DecidedOnUtc)).AsDateTime2().Nullable()
             .WithColumn(nameof(OrderApproval.DecisionComment)).AsString(int.MaxValue).Nullable()
             .WithColumn(nameof(OrderApproval.CreatedOnUtc)).AsDateTime2().NotNullable();
    }
}
````

### 13. ADD FILE `Plugins/Nop.Plugin.CW.Core/Data/Mapping/Approvals/OrderApprovalRequestBuilder.cs`

New file, 23 lines. Depends on: nothing outside this change list.

````csharp
using System.Data;
using FluentMigrator.Builders.Create.Table;
using Nop.Data.Extensions;
using Nop.Data.Mapping.Builders;
using Nop.Plugin.CW.Core.Domain.Approvals;

namespace Nop.Plugin.CW.Core.Data.Mapping.Approvals;

public class OrderApprovalRequestBuilder : NopEntityBuilder<OrderApprovalRequest>
{
    public override void MapEntity(CreateTableExpressionBuilder table)
    {
        //An approval link is looked up by the hash of its token, so the hash is unique and indexed.
        table.WithColumn(nameof(OrderApprovalRequest.Id)).AsInt32().PrimaryKey().Identity()
             .WithColumn(nameof(OrderApprovalRequest.OrderApprovalId)).AsInt32().ForeignKey<OrderApproval>(onDelete: Rule.Cascade).NotNullable()
             .WithColumn(nameof(OrderApprovalRequest.ApproverId)).AsInt32().ForeignKey<Approver>(onDelete: Rule.None).NotNullable()
             .WithColumn(nameof(OrderApprovalRequest.ApproverName)).AsString(400).NotNullable()
             .WithColumn(nameof(OrderApprovalRequest.ApproverEmail)).AsString(255).NotNullable()
             .WithColumn(nameof(OrderApprovalRequest.TokenHash)).AsString(64).NotNullable().Unique()
             .WithColumn(nameof(OrderApprovalRequest.SentOnUtc)).AsDateTime2().NotNullable()
             .WithColumn(nameof(OrderApprovalRequest.RevokedOnUtc)).AsDateTime2().Nullable();
    }
}
````

### 14. ADD FILE `Plugins/Nop.Plugin.CW.Core/Data/Migrations/Approvals/OrderApprovalMigration.cs`

New file, 51 lines. Depends on: nothing outside this change list.

````csharp
using FluentMigrator;
using Nop.Data.Extensions;
using Nop.Data.Mapping;
using Nop.Data.Migrations;
using Nop.Plugin.CW.Core.Domain.Approvals;

namespace Nop.Plugin.CW.Core.Data.Migrations;

/// <summary>
/// Adds the order approval tables to a database installed before they were part of the schema
/// </summary>
/// <remarks>
/// An installation creates the tables from <see cref="SchemaMigration"/> and nop marks this migration as applied without
/// running it; a plugin update runs it, and it creates each table only when it is missing.
/// </remarks>
[NopMigration(MigrationVersions.OrderApproval, "CW.Core order approval tables", MigrationProcessType.Update)]
public class OrderApprovalMigration : Migration
{
    /// <summary>
    /// The approval tables, each after the tables its foreign keys point to
    /// </summary>
    private static readonly Type[] Tables =
    [
        typeof(Approver),
        typeof(ApprovalRule),
        typeof(ApprovalRuleApprover),
        typeof(OrderApproval),
        typeof(OrderApprovalRequest)
    ];

    public override void Up()
    {
        this.CreateTableIfNotExists<Approver>();
        this.CreateTableIfNotExists<ApprovalRule>();
        this.CreateTableIfNotExists<ApprovalRuleApprover>();
        this.CreateTableIfNotExists<OrderApproval>();
        this.CreateTableIfNotExists<OrderApprovalRequest>();
    }

    public override void Down()
    {
        //Enumerable.Reverse by name: on an array, .Reverse() binds to the in-place span overload, which returns nothing.
        foreach (var entityType in Enumerable.Reverse(Tables))
        {
            var table = NameCompatibilityManager.GetTableName(entityType);

            if (Schema.Table(table).Exists())
                Delete.Table(table);
        }
    }
}
````

### 15. ADD FILE `Plugins/Nop.Plugin.CW.Core/Domain/Approvals/ApprovalRule.cs`

New file, 58 lines. Depends on: nothing outside this change list.

````csharp
using Nop.Core;

namespace Nop.Plugin.CW.Core.Domain.Approvals;

/// <summary>
/// One rule of the order approvals: orders touching its scope, over its threshold, wait for its approvers
/// </summary>
public class ApprovalRule : BaseEntity
{
    /// <summary>
    /// Gets or sets the store identifier
    /// </summary>
    public int StoreId { get; set; }

    /// <summary>
    /// Gets or sets the scope type identifier
    /// </summary>
    public int ScopeTypeId { get; set; }

    /// <summary>
    /// Gets or sets the scope type
    /// </summary>
    public ApprovalScopeType ScopeType
    {
        get => (ApprovalScopeType)ScopeTypeId;
        set => ScopeTypeId = (int)value;
    }

    /// <summary>
    /// Gets or sets the identifier of the branch, cost centre, department, packname or customer the rule is attached to
    /// </summary>
    public int ScopeId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a branch rule also covers the branch's child branches, at any depth
    /// </summary>
    public bool IncludeChildBranches { get; set; }

    /// <summary>
    /// Gets or sets the amount an order must be over to need approval (incl tax, after subsidy); null or 0 means every order
    /// </summary>
    public decimal? Threshold { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the rule applies
    /// </summary>
    public bool Active { get; set; }

    /// <summary>
    /// Gets or sets the date and time of creation (UTC)
    /// </summary>
    public DateTime CreatedOnUtc { get; set; }

    /// <summary>
    /// Gets or sets the date and time of the last change (UTC)
    /// </summary>
    public DateTime UpdatedOnUtc { get; set; }
}
````

### 16. ADD FILE `Plugins/Nop.Plugin.CW.Core/Domain/Approvals/ApprovalRuleApprover.cs`

New file, 19 lines. Depends on: nothing outside this change list.

````csharp
using Nop.Core;

namespace Nop.Plugin.CW.Core.Domain.Approvals;

/// <summary>
/// An approver of an approval rule
/// </summary>
public class ApprovalRuleApprover : BaseEntity
{
    /// <summary>
    /// Gets or sets the approval rule identifier
    /// </summary>
    public int ApprovalRuleId { get; set; }

    /// <summary>
    /// Gets or sets the approver identifier
    /// </summary>
    public int ApproverId { get; set; }
}
````

### 17. ADD FILE `Plugins/Nop.Plugin.CW.Core/Domain/Approvals/ApprovalScopeType.cs`

New file, 33 lines. Depends on: nothing outside this change list.

````csharp
namespace Nop.Plugin.CW.Core.Domain.Approvals;

/// <summary>
/// What an approval rule is attached to
/// </summary>
public enum ApprovalScopeType
{
    /// <summary>
    /// A branch: matches a line whose packname is in the branch, or a plain order whose customer is; with
    /// <see cref="ApprovalRule.IncludeChildBranches"/>, its child branches too
    /// </summary>
    Branch = 10,

    /// <summary>
    /// A cost centre: matches a line whose packname has it
    /// </summary>
    CostCenter = 20,

    /// <summary>
    /// A department: matches a line whose packname is in it
    /// </summary>
    Department = 30,

    /// <summary>
    /// A packname: matches a line ordered for it
    /// </summary>
    PackName = 40,

    /// <summary>
    /// A customer: matches every order the customer places
    /// </summary>
    Customer = 50
}
````

### 18. ADD FILE `Plugins/Nop.Plugin.CW.Core/Domain/Approvals/Approver.cs`

New file, 40 lines. Depends on: nothing outside this change list.

````csharp
using Nop.Core;
using Nop.Core.Domain.Common;

namespace Nop.Plugin.CW.Core.Domain.Approvals;

/// <summary>
/// A person who approves orders from an e-mail link. Not a customer: an approver needs no account.
/// </summary>
public class Approver : BaseEntity, ISoftDeletedEntity
{
    /// <summary>
    /// Gets or sets the store identifier
    /// </summary>
    public int StoreId { get; set; }

    /// <summary>
    /// Gets or sets the name shown in e-mails and on the admin pages
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the e-mail address the approval requests go to; unique per store among the live approvers
    /// </summary>
    public string Email { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the approver receives requests
    /// </summary>
    public bool Active { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the approver has been deleted
    /// </summary>
    public bool Deleted { get; set; }

    /// <summary>
    /// Gets or sets the date and time of creation (UTC)
    /// </summary>
    public DateTime CreatedOnUtc { get; set; }
}
````

### 19. ADD FILE `Plugins/Nop.Plugin.CW.Core/Domain/Approvals/OrderApproval.cs`

New file, 78 lines. Depends on: nothing outside this change list.

````csharp
using Nop.Core;

namespace Nop.Plugin.CW.Core.Domain.Approvals;

/// <summary>
/// The approval of one held order: one row per order, written when the order is placed and needs approval
/// </summary>
/// <remarks>
/// While <see cref="Status"/> is Pending the order is held: it stays nop-Pending, it cannot be shipped or moved on
/// in admin, and its subsidy and store credit stay spent (they come back when the order is cancelled).
/// </remarks>
public class OrderApproval : BaseEntity
{
    /// <summary>
    /// Gets or sets the order identifier
    /// </summary>
    public int OrderId { get; set; }

    /// <summary>
    /// Gets or sets the status identifier
    /// </summary>
    public int StatusId { get; set; }

    /// <summary>
    /// Gets or sets the status
    /// </summary>
    public OrderApprovalStatus Status
    {
        get => (OrderApprovalStatus)StatusId;
        set => StatusId = (int)value;
    }

    /// <summary>
    /// Gets or sets the amount the thresholds were compared with at placement: goods and shipping incl tax, less subsidy
    /// </summary>
    public decimal MeasuredAmount { get; set; }

    /// <summary>
    /// Gets or sets when the current requests were sent (UTC): at placement, and again on a resend or a change of
    /// approver. Reminders and expiry count from it.
    /// </summary>
    public DateTime RequestedOnUtc { get; set; }

    /// <summary>
    /// Gets or sets the number of the last reminder sent since <see cref="RequestedOnUtc"/> (0 to 2)
    /// </summary>
    public int LastReminderNumber { get; set; }

    /// <summary>
    /// Gets or sets who decided: the approver's name and e-mail, the admin's e-mail, or the system for an expiry
    /// </summary>
    public string DecidedBy { get; set; }

    /// <summary>
    /// Gets or sets the approver who decided, when an approver did
    /// </summary>
    public int? DecidedByApproverId { get; set; }

    /// <summary>
    /// Gets or sets the admin who decided on the approver's behalf, when one did
    /// </summary>
    public int? DecidedByCustomerId { get; set; }

    /// <summary>
    /// Gets or sets when the decision was made (UTC)
    /// </summary>
    public DateTime? DecidedOnUtc { get; set; }

    /// <summary>
    /// Gets or sets the reason of a decline or an expiry
    /// </summary>
    public string DecisionComment { get; set; }

    /// <summary>
    /// Gets or sets the date and time of creation (UTC)
    /// </summary>
    public DateTime CreatedOnUtc { get; set; }
}
````

### 20. ADD FILE `Plugins/Nop.Plugin.CW.Core/Domain/Approvals/OrderApprovalRequest.cs`

New file, 49 lines. Depends on: nothing outside this change list.

````csharp
using Nop.Core;

namespace Nop.Plugin.CW.Core.Domain.Approvals;

/// <summary>
/// One approval link sent to one approver: the request at placement, a reminder, a resend
/// </summary>
/// <remarks>
/// Only the SHA-256 hash of the link's token is stored, so a copy of the database cannot act on an order. A link
/// works while its order's approval is Pending and the request is not revoked; a resend or a change of approver
/// revokes every earlier link of the order.
/// </remarks>
public class OrderApprovalRequest : BaseEntity
{
    /// <summary>
    /// Gets or sets the order approval identifier
    /// </summary>
    public int OrderApprovalId { get; set; }

    /// <summary>
    /// Gets or sets the approver identifier
    /// </summary>
    public int ApproverId { get; set; }

    /// <summary>
    /// Gets or sets the approver's name when the request was sent
    /// </summary>
    public string ApproverName { get; set; }

    /// <summary>
    /// Gets or sets the approver's e-mail address when the request was sent
    /// </summary>
    public string ApproverEmail { get; set; }

    /// <summary>
    /// Gets or sets the SHA-256 hash of the link's token, as 64 lowercase hex characters
    /// </summary>
    public string TokenHash { get; set; }

    /// <summary>
    /// Gets or sets when the request was sent (UTC)
    /// </summary>
    public DateTime SentOnUtc { get; set; }

    /// <summary>
    /// Gets or sets when the link stopped working (UTC); null while it works
    /// </summary>
    public DateTime? RevokedOnUtc { get; set; }
}
````

### 21. ADD FILE `Plugins/Nop.Plugin.CW.Core/Domain/Approvals/OrderApprovalStatus.cs`

New file, 32 lines. Depends on: nothing outside this change list.

````csharp
namespace Nop.Plugin.CW.Core.Domain.Approvals;

/// <summary>
/// Where an order's approval stands
/// </summary>
public enum OrderApprovalStatus
{
    /// <summary>
    /// Waiting for an approver: the order is held
    /// </summary>
    Pending = 10,

    /// <summary>
    /// Accepted: the order is released
    /// </summary>
    Approved = 20,

    /// <summary>
    /// Declined: the order was cancelled and its lines went back to the cart
    /// </summary>
    Declined = 30,

    /// <summary>
    /// Not answered in time: the order was cancelled and its lines went back to the cart
    /// </summary>
    Expired = 40,

    /// <summary>
    /// The order was cancelled or deleted another way while it waited
    /// </summary>
    Cancelled = 50
}
````

### 22. ADD FILE `Plugins/Nop.Plugin.CW.Core/Domain/Messages/CWMessageTemplateSystemNames.Approvals.cs`

New file, 38 lines. Depends on: nothing outside this change list.

````csharp
namespace Nop.Plugin.CW.Core.Domain.Messages;

public static partial class CWMessageTemplateSystemNames
{
    #region Order approval templates

    /// <summary>
    /// Sent to each approver of an order that needs approval: the order and the link to accept or decline it
    /// </summary>
    public const string OrderApprovalApproverRequest = "OrderApproval.ApproverNotification";

    /// <summary>
    /// Sent to the approvers of an order still waiting, at the store's reminder days, with a new link
    /// </summary>
    public const string OrderApprovalApproverReminder = "OrderApproval.ApproverReminder";

    /// <summary>
    /// Sent to the customer who placed an order that needs approval, naming the approvers asked
    /// </summary>
    public const string OrderApprovalAwaitingCustomer = "OrderApproval.AwaitingCustomerNotification";

    /// <summary>
    /// Sent to the customer when the order is accepted
    /// </summary>
    public const string OrderApprovalApprovedCustomer = "OrderApproval.ApprovedCustomerNotification";

    /// <summary>
    /// Sent to the customer when the order is declined: the reason, and the items back in the cart
    /// </summary>
    public const string OrderApprovalDeclinedCustomer = "OrderApproval.DeclinedCustomerNotification";

    /// <summary>
    /// Sent to the customer when nobody answered in time: the order was cancelled and the items are back in the cart
    /// </summary>
    public const string OrderApprovalExpiredCustomer = "OrderApproval.ExpiredCustomerNotification";

    #endregion
}
````

### 23. ADD FILE `Plugins/Nop.Plugin.CW.Core/Domain/Orders/PlainOrderSettledEvent.cs`

New file, 24 lines. Depends on: nothing outside this change list.

````csharp
using Nop.Core.Domain.Orders;

namespace Nop.Plugin.CW.Core.Domain.Orders;

/// <summary>
/// Published once a plain (non-packname) order is placed and its store credit settled: nop's own placement publishes
/// <c>OrderPlacedEvent</c> before CW settles the order, so whatever must read the settled order waits for this
/// </summary>
public class PlainOrderSettledEvent
{
    /// <summary>
    /// Ctor
    /// </summary>
    /// <param name="order">The order</param>
    public PlainOrderSettledEvent(Order order)
    {
        Order = order;
    }

    /// <summary>
    /// Gets the order
    /// </summary>
    public Order Order { get; }
}
````

### 24. ADD FILE `Plugins/Nop.Plugin.CW.Core/Factories/CheckoutOverrideModelFactory.cs`

New file, 153 lines. Depends on: nothing outside this change list.

````csharp
using Nop.Core;
using Nop.Core.Domain.Common;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Core.Domain.Security;
using Nop.Core.Domain.Shipping;
using Nop.Core.Domain.Tax;
using Nop.Plugin.CW.Core.Services.Approvals;
using Nop.Services.Catalog;
using Nop.Services.Common;
using Nop.Services.Customers;
using Nop.Services.Directory;
using Nop.Services.Helpers;
using Nop.Services.Localization;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Services.Shipping;
using Nop.Services.Shipping.Pickup;
using Nop.Services.Stores;
using Nop.Services.Tax;
using Nop.Web.Factories;
using Nop.Web.Models.Checkout;

namespace Nop.Plugin.CW.Core.Factories;

/// <summary>
/// nop's checkout model factory, with the payment methods narrowed for an order that needs approval
/// </summary>
/// <remarks>
/// Both checkouts build their payment step from <see cref="PreparePaymentMethodModelAsync"/>: nop's one-page and
/// multi-step checkout, and the packname checkout. An order that will wait for an approver may only be paid with a
/// method that does not take the money at checkout (<c>MultiStoreSettings.ApprovalPaymentMethods</c>), so the others are
/// not offered. Placement refuses them too, whatever the page offered.
/// </remarks>
public class CheckoutOverrideModelFactory : CheckoutModelFactory
{
    #region Fields

    protected readonly IOrderApprovalRuleService _orderApprovalRuleService;

    #endregion

    #region Ctor

    public CheckoutOverrideModelFactory(AddressSettings addressSettings,
        CaptchaSettings captchaSettings,
        CommonSettings commonSettings,
        IAddressModelFactory addressModelFactory,
        IAddressService addressService,
        ICountryService countryService,
        ICurrencyService currencyService,
        ICustomerService customerService,
        IDateTimeHelper dateTimeHelper,
        IGenericAttributeService genericAttributeService,
        ILocalizationService localizationService,
        IOrderProcessingService orderProcessingService,
        IOrderTotalCalculationService orderTotalCalculationService,
        IPaymentPluginManager paymentPluginManager,
        IPaymentService paymentService,
        IPickupPluginManager pickupPluginManager,
        IPriceFormatter priceFormatter,
        IRewardPointService rewardPointService,
        IShippingPluginManager shippingPluginManager,
        IShippingService shippingService,
        IShoppingCartService shoppingCartService,
        IStateProvinceService stateProvinceService,
        IStoreContext storeContext,
        IStoreMappingService storeMappingService,
        ITaxService taxService,
        IWorkContext workContext,
        OrderSettings orderSettings,
        PaymentSettings paymentSettings,
        RewardPointsSettings rewardPointsSettings,
        ShippingSettings shippingSettings,
        TaxSettings taxSettings,
        IOrderApprovalRuleService orderApprovalRuleService) : base(addressSettings,
            captchaSettings,
            commonSettings,
            addressModelFactory,
            addressService,
            countryService,
            currencyService,
            customerService,
            dateTimeHelper,
            genericAttributeService,
            localizationService,
            orderProcessingService,
            orderTotalCalculationService,
            paymentPluginManager,
            paymentService,
            pickupPluginManager,
            priceFormatter,
            rewardPointService,
            shippingPluginManager,
            shippingService,
            shoppingCartService,
            stateProvinceService,
            storeContext,
            storeMappingService,
            taxService,
            workContext,
            orderSettings,
            paymentSettings,
            rewardPointsSettings,
            shippingSettings,
            taxSettings)
    {
        _orderApprovalRuleService = orderApprovalRuleService;
    }

    #endregion

    #region Methods

    /// <summary>
    /// Prepare payment method model; an order that needs approval is offered only the methods that can wait for it
    /// </summary>
    /// <param name="cart">Cart</param>
    /// <param name="filterByCountryId">Filter by country identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the payment method model
    /// </returns>
    public override async Task<CheckoutPaymentMethodModel> PreparePaymentMethodModelAsync(IList<ShoppingCartItem> cart, int filterByCountryId)
    {
        var model = await base.PreparePaymentMethodModelAsync(cart, filterByCountryId);

        if (!model.PaymentMethods.Any())
            return model;

        var customer = await _workContext.GetCurrentCustomerAsync();
        var store = await _storeContext.GetCurrentStoreAsync();

        var requirement = await _orderApprovalRuleService.EvaluateCartAsync(customer, store.Id, cart);
        if (!requirement.Required)
            return model;

        var hadSelection = model.PaymentMethods.Any(method => method.Selected);

        foreach (var method in model.PaymentMethods.ToList())
            if (!await _orderApprovalRuleService.IsPaymentMethodAllowedAsync(store.Id, method.PaymentMethodSystemName))
                model.PaymentMethods.Remove(method);

        //nop selects the first method when none is; keep that true for what is left
        if (hadSelection && !model.PaymentMethods.Any(method => method.Selected) && model.PaymentMethods.Any())
            model.PaymentMethods[0].Selected = true;

        return model;
    }

    #endregion
}
````

### 25. ADD FILE `Plugins/Nop.Plugin.CW.Core/Factories/IOrderApprovalModelFactory.cs`

New file, 21 lines. Depends on: nothing outside this change list.

````csharp
using Nop.Plugin.CW.Core.Models.Approvals;
using Nop.Plugin.CW.Core.Services.Approvals;

namespace Nop.Plugin.CW.Core.Factories;

/// <summary>
/// Prepares the page an approver opens from the e-mail
/// </summary>
public interface IOrderApprovalModelFactory
{
    /// <summary>
    /// Prepares the review page of an approval link
    /// </summary>
    /// <param name="token">The token from the link</param>
    /// <param name="result">What was just done with the link, to say so on the page; null when it was only opened</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the model
    /// </returns>
    Task<OrderApprovalReviewModel> PrepareReviewModelAsync(string token, OrderApprovalResult? result = null);
}
````

### 26. ADD FILE `Plugins/Nop.Plugin.CW.Core/Factories/OrderApprovalModelFactory.cs`

New file, 164 lines. Depends on: nothing outside this change list.

````csharp
using Nop.Core;
using Nop.Core.Domain.Orders;
using Nop.Plugin.CW.Core.Domain.Approvals;
using Nop.Plugin.CW.Core.Models.Approvals;
using Nop.Plugin.CW.Core.Services.Approvals;
using Nop.Plugin.CW.Core.Services.Orders;
using Nop.Services.Catalog;
using Nop.Services.Customers;
using Nop.Services.Directory;
using Nop.Services.Helpers;
using Nop.Services.Localization;
using Nop.Services.Orders;

namespace Nop.Plugin.CW.Core.Factories;

/// <summary>
/// Prepares the page an approver opens from the e-mail
/// </summary>
/// <remarks>
/// The approver is usually not signed in, so nothing here reads the current customer: the order is found from the link.
/// What was ordered is shown only while the link can still decide the order; afterwards the page says only where the
/// order stands.
/// </remarks>
public class OrderApprovalModelFactory : IOrderApprovalModelFactory
{
    #region Fields

    protected readonly ICurrencyService _currencyService;
    protected readonly ICustomerService _customerService;
    protected readonly ICWOrderService _cwOrderService;
    protected readonly IDateTimeHelper _dateTimeHelper;
    protected readonly ILocalizationService _localizationService;
    protected readonly IOrderApprovalService _orderApprovalService;
    protected readonly IOrderService _orderService;
    protected readonly IPriceFormatter _priceFormatter;
    protected readonly IProductService _productService;
    protected readonly IWorkContext _workContext;

    #endregion

    #region Ctor

    public OrderApprovalModelFactory(ICurrencyService currencyService,
        ICustomerService customerService,
        ICWOrderService cwOrderService,
        IDateTimeHelper dateTimeHelper,
        ILocalizationService localizationService,
        IOrderApprovalService orderApprovalService,
        IOrderService orderService,
        IPriceFormatter priceFormatter,
        IProductService productService,
        IWorkContext workContext)
    {
        _currencyService = currencyService;
        _customerService = customerService;
        _cwOrderService = cwOrderService;
        _dateTimeHelper = dateTimeHelper;
        _localizationService = localizationService;
        _orderApprovalService = orderApprovalService;
        _orderService = orderService;
        _priceFormatter = priceFormatter;
        _productService = productService;
        _workContext = workContext;
    }

    #endregion

    #region Utilities

    /// <summary>
    /// An amount of the order, in the currency the customer ordered in
    /// </summary>
    protected virtual async Task<string> FormatAsync(Order order, decimal amount, int languageId)
        => await _priceFormatter.FormatPriceAsync(_currencyService.ConvertCurrency(amount, order.CurrencyRate), true, order.CustomerCurrencyCode, false, languageId);

    /// <summary>
    /// The lines and totals of the order, as the approver reviews them
    /// </summary>
    protected virtual async Task PrepareOrderDetailsAsync(OrderApprovalReviewModel model, Order order, int languageId)
    {
        foreach (var item in await _orderService.GetOrderItemsAsync(order.Id))
        {
            var product = await _productService.GetProductByIdAsync(item.ProductId);

            model.Lines.Add(new OrderApprovalReviewLineModel
            {
                ProductName = product == null ? string.Empty : await _localizationService.GetLocalizedAsync(product, entity => entity.Name, languageId),
                AttributeInfo = item.AttributeDescription,
                PackName = (await _cwOrderService.GetPackNameOrderItemAsync(item.Id))?.PackName,
                Quantity = item.Quantity,
                UnitPrice = await FormatAsync(order, item.UnitPriceInclTax, languageId),
                Total = await FormatAsync(order, item.PriceInclTax, languageId)
            });
        }

        //the order as subsidy and store credit settled it; an order neither paid for has no settlement row
        var settlement = await _cwOrderService.GetOrderSettlementAsync(order.Id);
        var subsidy = (settlement?.SubsidyStockAmount ?? decimal.Zero) + (settlement?.SubsidyShippingAmount ?? decimal.Zero);
        var storeCredit = settlement?.CreditNoteAmount ?? decimal.Zero;

        model.Gross = await FormatAsync(order, settlement?.SettleableAmount ?? order.OrderTotal, languageId);
        model.Subsidy = subsidy > decimal.Zero ? await FormatAsync(order, subsidy, languageId) : null;
        model.StoreCredit = storeCredit > decimal.Zero ? await FormatAsync(order, storeCredit, languageId) : null;
        model.Payable = await FormatAsync(order, order.OrderTotal, languageId);
    }

    #endregion

    #region Methods

    public virtual async Task<OrderApprovalReviewModel> PrepareReviewModelAsync(string token, OrderApprovalResult? result = null)
    {
        var model = new OrderApprovalReviewModel { Token = token };

        var (approval, request) = await _orderApprovalService.GetByTokenAsync(token);
        var order = approval == null ? null : await _orderService.GetOrderByIdAsync(approval.OrderId);

        if (approval == null || request == null || order == null || order.Deleted)
        {
            model.Message = await _localizationService.GetResourceAsync("Plugins.CW.Core.OrderApproval.Result.NotFound");
            model.MessageIsError = true;
            return model;
        }

        var languageId = (await _workContext.GetWorkingLanguageAsync()).Id;
        var customer = await _customerService.GetCustomerByIdAsync(order.CustomerId);

        model.ShowOrder = true;
        model.ApproverName = request.ApproverName;
        model.OrderNumber = order.CustomOrderNumber;
        model.CustomerName = customer == null ? string.Empty : await _customerService.GetCustomerFullNameAsync(customer);
        model.CreatedOn = (await _dateTimeHelper.ConvertToUserTimeAsync(order.CreatedOnUtc, DateTimeKind.Utc)).ToString("D");
        model.Status = await _localizationService.GetResourceAsync($"Plugins.CW.Core.OrderApproval.Status.{approval.Status}");
        model.DecidedBy = approval.DecidedBy;
        model.DecisionComment = approval.DecisionComment;
        model.CanDecide = approval.Status == OrderApprovalStatus.Pending && request.RevokedOnUtc == null;

        if (model.CanDecide)
            await PrepareOrderDetailsAsync(model, order, languageId);

        //what the page says: what was just done, or why the link no longer decides
        var message = result switch
        {
            OrderApprovalResult.Done => approval.Status == OrderApprovalStatus.Approved ? "Plugins.CW.Core.OrderApproval.Result.Accepted" : "Plugins.CW.Core.OrderApproval.Result.Declined",
            OrderApprovalResult.NotFound => "Plugins.CW.Core.OrderApproval.Result.NotFound",
            OrderApprovalResult.Busy => "Plugins.CW.Core.OrderApproval.Result.Busy",
            OrderApprovalResult.Failed => "Plugins.CW.Core.OrderApproval.Result.Failed",
            OrderApprovalResult.ReasonRequired => "Plugins.CW.Core.OrderApproval.Result.ReasonRequired",
            _ when approval.Status != OrderApprovalStatus.Pending => "Plugins.CW.Core.OrderApproval.Result.AlreadyDecided",
            _ when request.RevokedOnUtc != null => "Plugins.CW.Core.OrderApproval.Result.LinkReplaced",
            _ => null
        };

        if (message != null)
        {
            model.Message = await _localizationService.GetResourceAsync(message);
            model.MessageIsError = result is not OrderApprovalResult.Done;
        }

        return model;
    }

    #endregion
}
````

### 27. ADD FILE `Plugins/Nop.Plugin.CW.Core/Filters/OrderApprovalAdminGuardFilter.cs`

New file, 136 lines. Depends on: nothing outside this change list.

````csharp
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Nop.Data;
using Nop.Plugin.CW.Core.Domain.Approvals;
using Nop.Services.Localization;
using Nop.Services.Messages;
using Nop.Web.Areas.Admin.Controllers;
using Nop.Web.Areas.Admin.Models.Orders;
using Nop.Web.Framework;

namespace Nop.Plugin.CW.Core.Filters;

/// <summary>
/// Refuses nop's admin actions that would move an order held for approval on, or change what the approver is
/// approving: setting its status by hand, adding a shipment, and editing its lines, totals or shipping method
/// </summary>
/// <remarks>
/// <para>
/// Payment, shipping and completion move an order through nop's <c>SetOrderStatusAsync</c>, and
/// <c>OrderApprovalEventConsumer</c> puts a held order back there. <c>ChangeOrderStatus</c> sets any status the admin
/// picks, Cancelled included, without nop's cancel (the stock would not come back); a shipment would send goods nobody
/// has approved; an edited line or total would change the order after the approver was shown it. The Cancel and Delete
/// buttons stay open: either ends the approval. Matched on the method name, as <see cref="PackNameOrderAdminGuardFilter"/>
/// explains.
/// </para>
/// <para>
/// A global filter is built for every request, so it reads the approval table itself rather than through the approval
/// service and its dependencies, and only once the action is one it guards. No feature-flag check: a held order is
/// protected whatever the switch says now. Fails closed: a guarded action whose order cannot be identified is refused.
/// </para>
/// </remarks>
public class OrderApprovalAdminGuardFilter : CoreActionFilter, IAsyncActionFilter
{
    #region Fields

    /// <summary>
    /// nop OrderController METHOD names that would move a held order on or change it
    /// </summary>
    private static readonly string[] BlockedMethods =
    [
        "ChangeOrderStatus",
        "AddShipment",
        "EditOrderItem",
        "DeleteOrderItem",
        "AddProductToOrder",
        "AddProductToOrderDetails",
        "EditOrderTotals",
        "EditShippingMethod"
    ];

    protected readonly ILocalizationService _localizationService;
    protected readonly INotificationService _notificationService;
    protected readonly IRepository<OrderApproval> _orderApprovalRepository;

    #endregion

    #region Ctor

    public OrderApprovalAdminGuardFilter(ILocalizationService localizationService,
        INotificationService notificationService,
        IRepository<OrderApproval> orderApprovalRepository)
    {
        _localizationService = localizationService;
        _notificationService = notificationService;
        _orderApprovalRepository = orderApprovalRepository;
    }

    #endregion

    #region Utilities

    /// <summary>
    /// The order id, from whichever argument the action carries it in
    /// </summary>
    private static int ResolveOrderId(ActionExecutingContext context)
    {
        if (context.ActionArguments.TryGetValue("model", out var model) && model is ShipmentModel shipmentModel)
            return shipmentModel.OrderId;

        if (context.ActionArguments.TryGetValue("searchModel", out var searchModel) && searchModel is AddProductToOrderSearchModel productSearchModel)
            return productSearchModel.OrderId;

        if (context.ActionArguments.TryGetValue("orderId", out var orderId) && orderId is int orderIdValue)
            return orderIdValue;

        //ChangeOrderStatus, the AddShipment GET, EditOrderItem, DeleteOrderItem, EditOrderTotals and EditShippingMethod
        //bind the order id as the route's "id"
        if (context.ActionArguments.TryGetValue("id", out var id) && id is int idValue)
            return idValue;

        return 0;
    }

    #endregion

    #region Methods

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (context.ActionDescriptor is not ControllerActionDescriptor descriptor
            || context.Controller is not OrderController
            || !BlockedMethods.Contains(descriptor.MethodInfo.Name))
        {
            await next();
            return;
        }

        var orderId = ResolveOrderId(context);

        if (orderId > 0 && !await _orderApprovalRepository.Table
                .AnyAsync(approval => approval.OrderId == orderId && approval.StatusId == (int)OrderApprovalStatus.Pending))
        {
            await next();
            return;
        }

        //the product search grid behind "Add product" reads JSON: a redirect would render as nothing having happened
        var isAjax = string.Equals(context.HttpContext.Request.Headers.XRequestedWith, "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);

        if (orderId > 0 && !isAjax)
        {
            _notificationService.ErrorNotification(await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.OrderApproval.OrderHeld"));
            context.Result = CreateRedirect("Edit", "Order", new RouteValueDictionary { ["id"] = orderId }, AreaNames.ADMIN);
            return;
        }

        context.Result = IsGet(context.HttpContext) && !isAjax
            ? CreateRedirect("List", "Order", null, AreaNames.ADMIN)
            : new StatusCodeResult(StatusCodes.Status403Forbidden);
    }

    #endregion
}
````

### 28. ADD FILE `Plugins/Nop.Plugin.CW.Core/Infrastructure/OrderApprovalRouteProvider.cs`

New file, 42 lines. Depends on: nothing outside this change list.

````csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Nop.Plugin.CW.Core.Services.Approvals;
using Nop.Web.Framework.Mvc.Routing;

namespace Nop.Plugin.CW.Core.Infrastructure;

/// <summary>
/// The routes of the approver's review page
/// </summary>
/// <remarks>
/// The token is part of the path, so the link in the e-mail carries nothing a mail client might strip from a query.
/// </remarks>
public class OrderApprovalRouteProvider : IRouteProvider
{
    /// <summary>
    /// Register routes
    /// </summary>
    /// <param name="endpointRouteBuilder">Route builder</param>
    public void RegisterRoutes(IEndpointRouteBuilder endpointRouteBuilder)
    {
        endpointRouteBuilder.MapControllerRoute(
            name: OrderApprovalDefaults.AcceptRouteName,
            pattern: $"{OrderApprovalDefaults.ReviewPath}/{{token}}/accept",
            defaults: new { controller = "OrderApproval", action = "Accept" });

        endpointRouteBuilder.MapControllerRoute(
            name: OrderApprovalDefaults.DeclineRouteName,
            pattern: $"{OrderApprovalDefaults.ReviewPath}/{{token}}/decline",
            defaults: new { controller = "OrderApproval", action = "Decline" });

        endpointRouteBuilder.MapControllerRoute(
            name: OrderApprovalDefaults.ReviewRouteName,
            pattern: $"{OrderApprovalDefaults.ReviewPath}/{{token}}",
            defaults: new { controller = "OrderApproval", action = "Review" });
    }

    /// <summary>
    /// Gets a priority of route provider
    /// </summary>
    public int Priority => 0;
}
````

### 29. ADD FILE `Plugins/Nop.Plugin.CW.Core/Models/Approvals/OrderApprovalReviewModel.cs`

New file, 84 lines. Depends on: nothing outside this change list.

````csharp
using Nop.Web.Framework.Models;

namespace Nop.Plugin.CW.Core.Models.Approvals;

/// <summary>
/// The page an approver opens from the e-mail: the order, and accept or decline while it waits
/// </summary>
public record OrderApprovalReviewModel : BaseNopModel
{
    /// <summary>
    /// Gets or sets the token of the link, posted back with the decision
    /// </summary>
    public string Token { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the link still decides the order
    /// </summary>
    public bool CanDecide { get; set; }

    /// <summary>
    /// Gets or sets what the page says above the order: why it cannot be decided, or what was just decided
    /// </summary>
    public string Message { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether <see cref="Message"/> reports an error
    /// </summary>
    public bool MessageIsError { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the order is shown; not for an unknown link
    /// </summary>
    public bool ShowOrder { get; set; }

    public string ApproverName { get; set; }

    public string OrderNumber { get; set; }

    public string CustomerName { get; set; }

    public string CreatedOn { get; set; }

    public string Status { get; set; }

    public string DecidedBy { get; set; }

    public string DecisionComment { get; set; }

    public IList<OrderApprovalReviewLineModel> Lines { get; set; } = [];

    public string Gross { get; set; }

    public string Subsidy { get; set; }

    public string StoreCredit { get; set; }

    public string Payable { get; set; }

    /// <summary>
    /// Gets or sets the reason the approver gives when declining
    /// </summary>
    public string DeclineReason { get; set; }
}

/// <summary>
/// One line of the order an approver reviews
/// </summary>
public record OrderApprovalReviewLineModel : BaseNopModel
{
    public string ProductName { get; set; }

    /// <summary>
    /// Gets or sets the attributes as nop formats them for the order: HTML
    /// </summary>
    public string AttributeInfo { get; set; }

    public string PackName { get; set; }

    public int Quantity { get; set; }

    public string UnitPrice { get; set; }

    public string Total { get; set; }
}
````

### 30. ADD FILE `Plugins/Nop.Plugin.CW.Core/Services/Approvals/IOrderApprovalInstallService.cs`

New file, 19 lines. Depends on: nothing outside this change list.

````csharp
namespace Nop.Plugin.CW.Core.Services.Approvals;

/// <summary>
/// Installs what the order approvals need besides their tables: the e-mails and the scheduled task
/// </summary>
public interface IOrderApprovalInstallService
{
    /// <summary>
    /// Installs each e-mail and the task once: a store that has edited an e-mail, or changed the task, keeps that
    /// </summary>
    /// <returns>A task that represents the asynchronous operation</returns>
    Task InstallOrUpdateAsync();

    /// <summary>
    /// Removes the scheduled task, which the scheduler could not find the type of once the plugin is gone
    /// </summary>
    /// <returns>A task that represents the asynchronous operation</returns>
    Task UninstallAsync();
}
````

### 31. ADD FILE `Plugins/Nop.Plugin.CW.Core/Services/Approvals/IOrderApprovalRuleService.cs`

New file, 37 lines. Depends on: nothing outside this change list.

````csharp
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Orders;

namespace Nop.Plugin.CW.Core.Services.Approvals;

/// <summary>
/// Decides whether a cart must wait for an approver, and which payment methods such an order may use
/// </summary>
/// <remarks>
/// Depends on no order-placement service, so placement itself can ask it before taking a payment.
/// </remarks>
public interface IOrderApprovalRuleService
{
    /// <summary>
    /// Decides whether the customer's cart needs approval. A packname cart is matched line by line against the
    /// packname of each line; any other cart against the customer's branch. An exchange cart never needs approval.
    /// </summary>
    /// <param name="customer">The customer placing the order</param>
    /// <param name="storeId">Store identifier</param>
    /// <param name="cart">The customer's cart, used when it has no packname lines</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the requirement; not required when Approvals is off for the store
    /// </returns>
    Task<OrderApprovalRequirement> EvaluateCartAsync(Customer customer, int storeId, IList<ShoppingCartItem> cart);

    /// <summary>
    /// Gets a value indicating whether an order waiting for approval may be paid with a payment method
    /// </summary>
    /// <param name="storeId">Store identifier</param>
    /// <param name="paymentMethodSystemName">Payment method system name</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains true when the store's ApprovalPaymentMethods lists the method
    /// </returns>
    Task<bool> IsPaymentMethodAllowedAsync(int storeId, string paymentMethodSystemName);
}
````

### 32. ADD FILE `Plugins/Nop.Plugin.CW.Core/Services/Approvals/IOrderApprovalService.cs`

New file, 172 lines. Depends on: nothing outside this change list.

````csharp
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Orders;
using Nop.Plugin.CW.Core.Domain.Approvals;

namespace Nop.Plugin.CW.Core.Services.Approvals;

/// <summary>
/// The approvals of held orders: holding an order, the approvers' and admins' decisions, the reminders and the expiry
/// </summary>
public interface IOrderApprovalService
{
    #region Queries

    /// <summary>
    /// Gets the approval of an order
    /// </summary>
    /// <param name="orderId">Order identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the approval; null when the order never needed one
    /// </returns>
    Task<OrderApproval> GetOrderApprovalByOrderIdAsync(int orderId);

    /// <summary>
    /// Gets a value indicating whether an order is held for approval
    /// </summary>
    /// <param name="orderId">Order identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains true while the order's approval is pending
    /// </returns>
    Task<bool> IsOrderHeldAsync(int orderId);

    /// <summary>
    /// Gets the links sent for an approval, newest first
    /// </summary>
    /// <param name="orderApprovalId">Order approval identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the requests
    /// </returns>
    Task<IList<OrderApprovalRequest>> GetRequestsAsync(int orderApprovalId);

    /// <summary>
    /// Gets the approval and the request an approval link belongs to
    /// </summary>
    /// <param name="token">The token from the link</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the approval and the request; both null for an unknown token
    /// </returns>
    Task<(OrderApproval approval, OrderApprovalRequest request)> GetByTokenAsync(string token);

    #endregion

    #region Placement

    /// <summary>
    /// Holds an order that needs approval: records its approval, which is what keeps it Pending. The e-mails are
    /// <see cref="NotifyHoldAsync"/>'s.
    /// </summary>
    /// <param name="order">The order just placed</param>
    /// <param name="requirement">What placement decided from the cart</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains true when the order is held; false when none of its approvers is active any more (the
    /// order carries a note saying so)
    /// </returns>
    Task<bool> HoldOrderAsync(Order order, OrderApprovalRequirement requirement);

    /// <summary>
    /// Sends a held order's first e-mails: a link to each approver, and the customer that the order waits
    /// </summary>
    /// <param name="order">The held order, settled</param>
    /// <param name="requirement">What placement decided from the cart</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    Task NotifyHoldAsync(Order order, OrderApprovalRequirement requirement);

    /// <summary>
    /// Ends a pending approval whose order was cancelled or deleted another way, and revokes its links
    /// </summary>
    /// <param name="orderId">Order identifier</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    Task MarkCancelledAsync(int orderId);

    #endregion

    #region Decisions

    /// <summary>
    /// Accepts an order from an approver's link: the order is released and the customer told
    /// </summary>
    /// <param name="token">The token from the link</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains what happened
    /// </returns>
    Task<OrderApprovalResult> AcceptByTokenAsync(string token);

    /// <summary>
    /// Declines an order from an approver's link: the order is cancelled, its lines go back to the customer's cart
    /// and the customer is told why
    /// </summary>
    /// <param name="token">The token from the link</param>
    /// <param name="reason">Why; required</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains what happened
    /// </returns>
    Task<OrderApprovalResult> DeclineByTokenAsync(string token, string reason);

    /// <summary>
    /// Accepts an order on the approvers' behalf
    /// </summary>
    /// <param name="orderId">Order identifier</param>
    /// <param name="admin">The admin deciding</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains what happened
    /// </returns>
    Task<OrderApprovalResult> AcceptByAdminAsync(int orderId, Customer admin);

    /// <summary>
    /// Declines an order on the approvers' behalf
    /// </summary>
    /// <param name="orderId">Order identifier</param>
    /// <param name="admin">The admin deciding</param>
    /// <param name="reason">Why; required</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains what happened
    /// </returns>
    Task<OrderApprovalResult> DeclineByAdminAsync(int orderId, Customer admin, string reason);

    /// <summary>
    /// Sends the order's approvers new links and revokes the old ones; reminders and expiry count again from now
    /// </summary>
    /// <param name="orderId">Order identifier</param>
    /// <param name="admin">The admin asking</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains what happened
    /// </returns>
    Task<OrderApprovalResult> ResendAsync(int orderId, Customer admin);

    /// <summary>
    /// Replaces the order's approvers with one approver, found by e-mail address in the order's store or created,
    /// and sends them a link; the old links are revoked and reminders and expiry count again from now
    /// </summary>
    /// <param name="orderId">Order identifier</param>
    /// <param name="admin">The admin asking</param>
    /// <param name="name">The approver's name</param>
    /// <param name="email">The approver's e-mail address</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains what happened
    /// </returns>
    Task<OrderApprovalResult> ChangeApproverAsync(int orderId, Customer admin, string name, string email);

    #endregion

    #region Task

    /// <summary>
    /// Sends the reminders due and expires the approvals not answered in time, in every store; an approval whose order
    /// was deleted or cancelled meanwhile is ended
    /// </summary>
    /// <returns>A task that represents the asynchronous operation</returns>
    Task ProcessPendingApprovalsAsync();

    #endregion
}
````

### 33. ADD FILE `Plugins/Nop.Plugin.CW.Core/Services/Approvals/OrderApprovalDefaults.cs`

New file, 77 lines. Depends on: nothing outside this change list.

````csharp
using Nop.Plugin.CW.Core.Domain.Messages;

namespace Nop.Plugin.CW.Core.Services.Approvals;

/// <summary>
/// The fixed values of the order approvals
/// </summary>
public static class OrderApprovalDefaults
{
    /// <summary>
    /// The route of an approver's review page: the link in the approval e-mails
    /// </summary>
    public const string ReviewRouteName = "Plugin.CW.Core.OrderApproval.Review";

    /// <summary>
    /// The path of the review page, without the token
    /// </summary>
    public const string ReviewPath = "orderapproval";

    /// <summary>
    /// The route the review page posts an acceptance to
    /// </summary>
    public const string AcceptRouteName = "Plugin.CW.Core.OrderApproval.Accept";

    /// <summary>
    /// The route the review page posts a decline to
    /// </summary>
    public const string DeclineRouteName = "Plugin.CW.Core.OrderApproval.Decline";

    /// <summary>
    /// How many random bytes an approval link's token carries
    /// </summary>
    public const int TokenBytes = 32;

    /// <summary>
    /// The longest decline reason kept
    /// </summary>
    public const int DecisionCommentMaxLength = 2000;

    /// <summary>
    /// The lock that lets one change at a time act on an order's approval: {0} is the order id
    /// </summary>
    public const string DecisionLockKeyFormat = "CW.orderapproval.decide.{0}";

    /// <summary>
    /// How long a decision may hold its lock: it cancels the order and puts its lines back in the cart
    /// </summary>
    public static TimeSpan DecisionLockTime => TimeSpan.FromSeconds(60);

    /// <summary>
    /// The type name nop's scheduler finds the task by
    /// </summary>
    public static string TaskTypeFullName => typeof(ProcessOrderApprovalsTask).FullName;

    /// <summary>
    /// The task's name on System › Schedule tasks
    /// </summary>
    public const string TaskName = "Process order approvals";

    /// <summary>
    /// How often the task runs: hourly
    /// </summary>
    public const int TaskSeconds = 60 * 60;

    /// <summary>
    /// The e-mails the order approvals send, each installed once
    /// </summary>
    public static string[] MessageTemplates =>
    [
        CWMessageTemplateSystemNames.OrderApprovalApproverRequest,
        CWMessageTemplateSystemNames.OrderApprovalApproverReminder,
        CWMessageTemplateSystemNames.OrderApprovalAwaitingCustomer,
        CWMessageTemplateSystemNames.OrderApprovalApprovedCustomer,
        CWMessageTemplateSystemNames.OrderApprovalDeclinedCustomer,
        CWMessageTemplateSystemNames.OrderApprovalExpiredCustomer
    ];
}
````

### 34. ADD FILE `Plugins/Nop.Plugin.CW.Core/Services/Approvals/OrderApprovalEventConsumer.cs`

New file, 213 lines. Depends on: nothing outside this change list.

````csharp
using Nop.Core.Domain.Messages;
using Nop.Core.Domain.Orders;
using Nop.Core.Events;
using Nop.Plugin.CW.Core.Domain.Orders;
using Nop.Plugin.CW.Core.Services.Messages;
using Nop.Services.Events;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.Messages;
using Nop.Services.Orders;

namespace Nop.Plugin.CW.Core.Services.Approvals;

/// <summary>
/// Holds an order that needs approval when it is placed, and keeps a held order from moving on until it is decided
/// </summary>
/// <remarks>
/// <para>
/// The hold is written on <c>OrderPlacedEvent</c>, which both placements publish before they check the order's
/// status: an order with nothing to pay is Paid at once, and nop's check would otherwise move it to Processing. The
/// e-mails go at once for a packname order, which is settled before the event; a plain order is settled after nop's
/// placement returns, so its e-mails wait for <c>PlainOrderSettledEvent</c>.
/// </para>
/// <para>
/// A held order is kept Pending through nop's own seam: <c>SetOrderStatusAsync</c> publishes
/// <c>OrderStatusChangedEvent</c> and stops when a handler has put the status back. That covers payment, shipping and
/// completion. nop's admin <c>ChangeOrderStatus</c> publishes the event too, but sets any status the admin picks,
/// Cancelled included, without nop's cancel (stock is not returned); <c>OrderApprovalAdminGuardFilter</c> refuses it
/// on a held order. The Cancel button cancels through nop's cancel, and ends the approval here.
/// </para>
/// </remarks>
public class OrderApprovalEventConsumer : IConsumer<OrderPlacedEvent>,
    IConsumer<PlainOrderSettledEvent>,
    IConsumer<OrderStatusChangedEvent>,
    IConsumer<EntityDeletedEvent<Order>>,
    IConsumer<AdditionalTokensAddedEvent>
{
    #region Fields

    protected readonly ILocalizationService _localizationService;
    protected readonly ILogger _logger;
    protected readonly IOrderApprovalService _orderApprovalService;
    protected readonly IOrderService _orderService;
    protected readonly OrderApprovalPlacementContext _placementContext;

    #endregion

    #region Ctor

    public OrderApprovalEventConsumer(ILocalizationService localizationService,
        ILogger logger,
        IOrderApprovalService orderApprovalService,
        IOrderService orderService,
        OrderApprovalPlacementContext placementContext)
    {
        _localizationService = localizationService;
        _logger = logger;
        _orderApprovalService = orderApprovalService;
        _orderService = orderService;
        _placementContext = placementContext;
    }

    #endregion

    #region Utilities

    /// <summary>
    /// Adds a note to the order, for staff only
    /// </summary>
    protected virtual async Task AddOrderNoteAsync(Order order, string note)
    {
        await _orderService.InsertOrderNoteAsync(new OrderNote
        {
            OrderId = order.Id,
            Note = note,
            DisplayToCustomer = false,
            CreatedOnUtc = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Sends a held order's first e-mails; a failure is logged and noted, the order stays held (Resend or Change
    /// approver on its Approval card sends them again)
    /// </summary>
    protected virtual async Task NotifyAsync(Order order, OrderApprovalRequirement requirement)
    {
        try
        {
            await _orderApprovalService.NotifyHoldAsync(order, requirement);
        }
        catch (Exception exception)
        {
            await _logger.ErrorAsync($"[Approvals] Order {order.Id} is held, and its approval e-mails could not be sent.", exception);
            await AddOrderNoteAsync(order, await _localizationService.GetResourceAsync("Plugins.CW.Core.OrderApproval.Note.NotifyFailed"));
        }
    }

    #endregion

    #region Methods

    /// <summary>
    /// Holds the order when placement decided it needs approval
    /// </summary>
    /// <param name="eventMessage">Event message</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public async Task HandleEventAsync(OrderPlacedEvent eventMessage)
    {
        var order = eventMessage.Order;
        if (order == null)
            return;

        var requirement = _placementContext.Take(order.CustomerId);
        if (requirement?.Required != true)
            return;

        //the order is placed whatever happens here: an exception would reach placement and tell the customer it failed
        bool held;

        try
        {
            held = await _orderApprovalService.HoldOrderAsync(order, requirement);
        }
        catch (Exception exception)
        {
            await _logger.ErrorAsync($"[Approvals] Order {order.Id} needed approval, and recording it failed: the order is not held.", exception);
            await AddOrderNoteAsync(order, await _localizationService.GetResourceAsync("Plugins.CW.Core.OrderApproval.Note.HoldFailed"));
            return;
        }

        if (!held)
            return;

        if (requirement.NotifyAfterSettlement)
            _placementContext.SetPendingNotification(order.Id, requirement);
        else
            await NotifyAsync(order, requirement);
    }

    /// <summary>
    /// Sends a held plain order's e-mails, now that its store credit is settled
    /// </summary>
    /// <param name="eventMessage">Event message</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public async Task HandleEventAsync(PlainOrderSettledEvent eventMessage)
    {
        if (eventMessage.Order == null)
            return;

        var requirement = _placementContext.TakePendingNotification(eventMessage.Order.Id);

        if (requirement != null)
            await NotifyAsync(eventMessage.Order, requirement);
    }

    /// <summary>
    /// Puts a held order back to Pending when something tries to move it on, and ends its approval when it is cancelled
    /// </summary>
    /// <param name="eventMessage">Event message</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public async Task HandleEventAsync(OrderStatusChangedEvent eventMessage)
    {
        var order = eventMessage.Order;

        if (order == null || eventMessage.PreviousOrderStatus != OrderStatus.Pending || order.OrderStatus == OrderStatus.Pending)
            return;

        if (!await _orderApprovalService.IsOrderHeldAsync(order.Id))
            return;

        if (order.OrderStatus == OrderStatus.Cancelled)
        {
            await _orderApprovalService.MarkCancelledAsync(order.Id);
            return;
        }

        order.OrderStatusId = (int)OrderStatus.Pending;
        await _orderService.UpdateOrderAsync(order);
    }

    /// <summary>
    /// Ends the approval of a deleted order
    /// </summary>
    /// <param name="eventMessage">Event message</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public async Task HandleEventAsync(EntityDeletedEvent<Order> eventMessage)
    {
        if (eventMessage.Entity != null)
            await _orderApprovalService.MarkCancelledAsync(eventMessage.Entity.Id);
    }

    /// <summary>
    /// Lists the approval tokens on the admin message template page
    /// </summary>
    /// <param name="eventMessage">Event message</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public Task HandleEventAsync(AdditionalTokensAddedEvent eventMessage)
    {
        if (eventMessage.TokenGroups == null || eventMessage.TokenGroups.Contains(CWTokenGroupNames.OrderApprovalTokens))
        {
            eventMessage.AdditionalTokens.Add("%OrderApproval.ApproverName%");
            eventMessage.AdditionalTokens.Add("%OrderApproval.Link%");
            eventMessage.AdditionalTokens.Add("%OrderApproval.Approvers%");
            eventMessage.AdditionalTokens.Add("%OrderApproval.Amount%");
            eventMessage.AdditionalTokens.Add("%OrderApproval.Reason%");
            eventMessage.AdditionalTokens.Add("%OrderApproval.DecidedBy%");
        }

        return Task.CompletedTask;
    }

    #endregion
}
````

### 35. ADD FILE `Plugins/Nop.Plugin.CW.Core/Services/Approvals/OrderApprovalInstallService.cs`

New file, 146 lines. Depends on: nothing outside this change list.

````csharp
using Nop.Core.Domain.Messages;
using Nop.Core.Domain.ScheduleTasks;
using Nop.Plugin.CW.Core.Domain.Messages;
using Nop.Services.Localization;
using Nop.Services.Messages;
using Nop.Services.ScheduleTasks;

namespace Nop.Plugin.CW.Core.Services.Approvals;

/// <summary>
/// Installs the e-mails and the scheduled task of the order approvals
/// </summary>
public class OrderApprovalInstallService : IOrderApprovalInstallService
{
    #region Fields

    protected readonly EmailAccountSettings _emailAccountSettings;
    protected readonly IEmailAccountService _emailAccountService;
    protected readonly ILocalizationService _localizationService;
    protected readonly IMessageTemplateService _messageTemplateService;
    protected readonly IScheduleTaskService _scheduleTaskService;

    #endregion

    #region Ctor

    public OrderApprovalInstallService(EmailAccountSettings emailAccountSettings,
        IEmailAccountService emailAccountService,
        ILocalizationService localizationService,
        IMessageTemplateService messageTemplateService,
        IScheduleTaskService scheduleTaskService)
    {
        _emailAccountSettings = emailAccountSettings;
        _emailAccountService = emailAccountService;
        _localizationService = localizationService;
        _messageTemplateService = messageTemplateService;
        _scheduleTaskService = scheduleTaskService;
    }

    #endregion

    #region Utilities

    /// <summary>
    /// The e-mails as installed: name, subject, body and the description the admin template list shows
    /// </summary>
    protected static (string Name, string Subject, string Body, string Description)[] Templates =>
    [
        (CWMessageTemplateSystemNames.OrderApprovalApproverRequest,
            "%Store.Name%: order %Order.OrderNumber% needs your approval",
            "Hello %OrderApproval.ApproverName%,<br /><br />" +
            "%Order.CustomerFullName% placed order %Order.OrderNumber% at %Store.Name% on %Order.CreatedOn%, and it needs your approval.<br />" +
            "Amount after subsidy: %OrderApproval.Amount%<br /><br />" +
            "%Order.Product(s)%<br />" +
            "<a href=\"%OrderApproval.Link%\">Review the order and accept or decline it</a><br /><br />" +
            "The order is not processed until it is accepted.<br />",
            "Sent to each approver of an order that needs approval: the order, and a link to accept or decline it."),
        (CWMessageTemplateSystemNames.OrderApprovalApproverReminder,
            "Reminder - %Store.Name%: order %Order.OrderNumber% is waiting for your approval",
            "Hello %OrderApproval.ApproverName%,<br /><br />" +
            "Order %Order.OrderNumber%, placed by %Order.CustomerFullName% on %Order.CreatedOn%, is still waiting for your approval.<br />" +
            "Amount after subsidy: %OrderApproval.Amount%<br /><br />" +
            "%Order.Product(s)%<br />" +
            "<a href=\"%OrderApproval.Link%\">Review the order and accept or decline it</a><br />",
            "Sent to the approvers of an order still waiting for approval, at the reminder days of the store's settings."),
        (CWMessageTemplateSystemNames.OrderApprovalAwaitingCustomer,
            "%Store.Name%: order %Order.OrderNumber% is waiting for approval",
            "Hello %Order.CustomerFullName%,<br /><br />" +
            "Thank you for your order %Order.OrderNumber%. It needs approval before it is processed, and has been sent to %OrderApproval.Approvers%.<br />" +
            "We will let you know when they decide.<br /><br />" +
            "%Order.Product(s)%<br />",
            "Sent to the customer who placed an order that needs approval, naming the approvers asked."),
        (CWMessageTemplateSystemNames.OrderApprovalApprovedCustomer,
            "%Store.Name%: order %Order.OrderNumber% is approved",
            "Hello %Order.CustomerFullName%,<br /><br />" +
            "Your order %Order.OrderNumber% was approved by %OrderApproval.DecidedBy% and is now being processed.<br /><br />" +
            "%Order.Product(s)%<br />",
            "Sent to the customer when an order waiting for approval is accepted."),
        (CWMessageTemplateSystemNames.OrderApprovalDeclinedCustomer,
            "%Store.Name%: order %Order.OrderNumber% was declined",
            "Hello %Order.CustomerFullName%,<br /><br />" +
            "Your order %Order.OrderNumber% was declined by %OrderApproval.DecidedBy%:<br />" +
            "<i>%OrderApproval.Reason%</i><br /><br />" +
            "The order was cancelled and its items are back in your cart, so you can change it and order again.<br /><br />" +
            "%Order.Product(s)%<br />",
            "Sent to the customer when an order waiting for approval is declined: the reason, and the items back in the cart."),
        (CWMessageTemplateSystemNames.OrderApprovalExpiredCustomer,
            "%Store.Name%: order %Order.OrderNumber% was not approved in time",
            "Hello %Order.CustomerFullName%,<br /><br />" +
            "Your order %Order.OrderNumber% was not approved in time: %OrderApproval.Reason%<br /><br />" +
            "The order was cancelled and its items are back in your cart, so you can order again.<br /><br />" +
            "%Order.Product(s)%<br />",
            "Sent to the customer when an order waiting for approval is not answered within the expiry days of the store's settings.")
    ];

    #endregion

    #region Methods

    public virtual async Task InstallOrUpdateAsync()
    {
        var emailAccount = await _emailAccountService.GetEmailAccountByIdAsync(_emailAccountSettings.DefaultEmailAccountId)
            ?? (await _emailAccountService.GetAllEmailAccountsAsync()).FirstOrDefault();

        foreach (var template in Templates)
        {
            if ((await _messageTemplateService.GetMessageTemplatesByNameAsync(template.Name)).Any())
                continue;

            await _messageTemplateService.InsertMessageTemplateAsync(new MessageTemplate
            {
                Name = template.Name,
                Subject = template.Subject,
                Body = template.Body,
                IsActive = true,
                EmailAccountId = emailAccount?.Id ?? 0
            });

            await _localizationService.AddOrUpdateLocaleResourceAsync(
                $"Admin.ContentManagement.MessageTemplates.Description.{template.Name}", template.Description);
        }

        if (await _scheduleTaskService.GetTaskByTypeAsync(OrderApprovalDefaults.TaskTypeFullName) != null)
            return;

        await _scheduleTaskService.InsertTaskAsync(new ScheduleTask
        {
            Name = OrderApprovalDefaults.TaskName,
            Seconds = OrderApprovalDefaults.TaskSeconds,
            Type = OrderApprovalDefaults.TaskTypeFullName,
            Enabled = true,
            LastEnabledUtc = DateTime.UtcNow,
            StopOnError = false
        });
    }

    public virtual async Task UninstallAsync()
    {
        var task = await _scheduleTaskService.GetTaskByTypeAsync(OrderApprovalDefaults.TaskTypeFullName);

        if (task != null)
            await _scheduleTaskService.DeleteTaskAsync(task);
    }

    #endregion
}
````

### 36. ADD FILE `Plugins/Nop.Plugin.CW.Core/Services/Approvals/OrderApprovalLocaleResources.cs`

New file, 120 lines. Depends on: nothing outside this change list.

````csharp
namespace Nop.Plugin.CW.Core.Services.Approvals;

/// <summary>
/// The text of the order approvals, installed with the plugin's other resources
/// </summary>
public static class OrderApprovalLocaleResources
{
    /// <summary>
    /// Every resource of the order approvals, by key
    /// </summary>
    public static Dictionary<string, string> Resources => new()
    {
        //settings
        ["Plugins.CW.Core.Admin.MultiStores.Settings.ApprovalsSection"] = "Order approvals",
        ["Plugins.CW.Core.Admin.MultiStores.Settings.ApprovalsEnable"] = "Enable order approvals",
        ["Plugins.CW.Core.Admin.MultiStores.Settings.ApprovalsEnable.Hint"] = "Orders that match an approval rule wait for an approver, who accepts or declines them from an e-mail link. Orders already waiting are finished whether this is on or off.",
        ["Plugins.CW.Core.Admin.MultiStores.Settings.ApprovalPaymentMethods"] = "Payment methods that can wait for approval",
        ["Plugins.CW.Core.Admin.MultiStores.Settings.ApprovalPaymentMethods.Hint"] = "The methods offered for an order that needs approval: methods that do not take the money at checkout (account, purchase order). Card methods take it at once and are not offered. An order with nothing left to pay can always wait.",
        ["Plugins.CW.Core.Admin.MultiStores.Settings.ApprovalFirstReminderDays"] = "First reminder after (days)",
        ["Plugins.CW.Core.Admin.MultiStores.Settings.ApprovalFirstReminderDays.Hint"] = "Days after the request before the approvers are reminded. 0 sends no first reminder.",
        ["Plugins.CW.Core.Admin.MultiStores.Settings.ApprovalSecondReminderDays"] = "Second reminder after (days)",
        ["Plugins.CW.Core.Admin.MultiStores.Settings.ApprovalSecondReminderDays.Hint"] = "Days after the request before the approvers are reminded again. 0 sends no second reminder.",
        ["Plugins.CW.Core.Admin.MultiStores.Settings.ApprovalExpiryDays"] = "Expire after (days)",
        ["Plugins.CW.Core.Admin.MultiStores.Settings.ApprovalExpiryDays.Hint"] = "Days after the request before an unanswered order is cancelled and its items go back to the customer's cart. 0 never expires an order.",
        ["Plugins.CW.Core.Admin.MultiStores.Settings.ApprovalDays.NotNegative"] = "Enter 0 or a number of days.",

        //placement
        ["Plugins.CW.Core.OrderApproval.PaymentMethodNotAllowed"] = "This order needs approval and cannot be paid with the selected payment method. Please choose another one.",

        //order notes
        ["Plugins.CW.Core.OrderApproval.Note.Held"] = "The order is waiting for approval by {0} (approval rules {1}).",
        ["Plugins.CW.Core.OrderApproval.Note.Approved"] = "The order was approved by {0}.",
        ["Plugins.CW.Core.OrderApproval.Note.Declined"] = "The order was declined by {0}: {1}",
        ["Plugins.CW.Core.OrderApproval.Note.Expired"] = "The order's approval expired ({0}): {1}",
        ["Plugins.CW.Core.OrderApproval.Note.ReOrderWarnings"] = "Some items could not go back to the customer's cart: {0}",
        ["Plugins.CW.Core.OrderApproval.Note.Cancelled"] = "The order was cancelled or deleted while it waited for approval; its approval links no longer work.",
        ["Plugins.CW.Core.OrderApproval.Note.Resent"] = "The approval links were sent again to {0} by {1}.",
        ["Plugins.CW.Core.OrderApproval.Note.ApproverChanged"] = "The approver was changed to {0} by {1}.",
        ["Plugins.CW.Core.OrderApproval.Note.EmailFailed"] = "The approval e-mail to {0} could not be queued (see the log). Send the links again from the Approval card.",
        ["Plugins.CW.Core.OrderApproval.Note.NotifyFailed"] = "The order is waiting for approval, but its approval e-mails could not be sent (see the log). Send the links again or change the approver from the Approval card.",
        ["Plugins.CW.Core.OrderApproval.Note.HoldFailed"] = "This order needed approval, but the approval could not be recorded (see the log): the order is NOT held. Check it before it is processed.",
        ["Plugins.CW.Core.OrderApproval.Note.NoApprover"] = "This order matched an approval rule, but none of the rule's approvers is active any more: the order is not held.",
        ["Plugins.CW.Core.OrderApproval.Note.ReOrderFailed"] = "The order was cancelled, but its items could not be put back in the customer's cart (see the log).",
        ["Plugins.CW.Core.OrderApproval.ReOrderHeld"] = "This order is waiting for approval and cannot be ordered again until it is decided.",

        //who decided
        ["Plugins.CW.Core.OrderApproval.DecidedBy.Admin"] = "{0} (admin, on the approvers' behalf)",
        ["Plugins.CW.Core.OrderApproval.DecidedBy.System"] = "The system",
        ["Plugins.CW.Core.OrderApproval.DecidedBy.OrderCancelled"] = "Order cancelled",
        ["Plugins.CW.Core.OrderApproval.ExpiredReason"] = "No approver answered within {0} days.",

        //statuses
        ["Plugins.CW.Core.OrderApproval.Status.Pending"] = "Waiting for approval",
        ["Plugins.CW.Core.OrderApproval.Status.Approved"] = "Approved",
        ["Plugins.CW.Core.OrderApproval.Status.Declined"] = "Declined",
        ["Plugins.CW.Core.OrderApproval.Status.Expired"] = "Expired",
        ["Plugins.CW.Core.OrderApproval.Status.Cancelled"] = "Cancelled",

        //approver's page
        ["Plugins.CW.Core.OrderApproval.PageTitle"] = "Order approval",
        ["Plugins.CW.Core.OrderApproval.Intro"] = "{0} placed order {1} on {2}, and it needs your approval.",
        ["Plugins.CW.Core.OrderApproval.Status"] = "Status:",
        ["Plugins.CW.Core.OrderApproval.DecidedBy"] = "Decided by:",
        ["Plugins.CW.Core.OrderApproval.DecisionComment"] = "Reason:",
        ["Plugins.CW.Core.OrderApproval.Column.Product"] = "Product",
        ["Plugins.CW.Core.OrderApproval.Column.For"] = "For",
        ["Plugins.CW.Core.OrderApproval.Column.Price"] = "Price",
        ["Plugins.CW.Core.OrderApproval.Column.Quantity"] = "Qty.",
        ["Plugins.CW.Core.OrderApproval.Column.Total"] = "Total",
        ["Plugins.CW.Core.OrderApproval.Totals.Gross"] = "Total before subsidy and credit",
        ["Plugins.CW.Core.OrderApproval.Totals.Subsidy"] = "Subsidy",
        ["Plugins.CW.Core.OrderApproval.Totals.StoreCredit"] = "Store credit",
        ["Plugins.CW.Core.OrderApproval.Totals.Payable"] = "Payable",
        ["Plugins.CW.Core.OrderApproval.Accept"] = "Accept order",
        ["Plugins.CW.Core.OrderApproval.Decline"] = "Decline order",
        ["Plugins.CW.Core.OrderApproval.DeclineReason"] = "To decline, say why (the customer will read this):",
        ["Plugins.CW.Core.OrderApproval.Result.Accepted"] = "Thank you. The order is approved and will now be processed.",
        ["Plugins.CW.Core.OrderApproval.Result.Declined"] = "The order is declined. The customer has been told why, and the items are back in their cart.",
        ["Plugins.CW.Core.OrderApproval.Result.NotFound"] = "This approval link is not valid.",
        ["Plugins.CW.Core.OrderApproval.Result.AlreadyDecided"] = "This order has already been decided.",
        ["Plugins.CW.Core.OrderApproval.Result.LinkReplaced"] = "This link was replaced by a newer e-mail. Please use the link in the latest e-mail.",
        ["Plugins.CW.Core.OrderApproval.Result.Busy"] = "The order is being decided right now. Please reload this page in a moment.",
        ["Plugins.CW.Core.OrderApproval.Result.Failed"] = "The decision could not be completed. The order is still waiting; please try again later.",
        ["Plugins.CW.Core.OrderApproval.Result.ReasonRequired"] = "Please say why the order is declined.",

        //admin order page
        ["Plugins.CW.Core.Admin.OrderApproval.Title"] = "Approval",
        ["Plugins.CW.Core.Admin.OrderApproval.Status"] = "Status",
        ["Plugins.CW.Core.Admin.OrderApproval.MeasuredAmount"] = "Amount after subsidy",
        ["Plugins.CW.Core.Admin.OrderApproval.RequestedOn"] = "Requested on",
        ["Plugins.CW.Core.Admin.OrderApproval.Reminders"] = "Reminders sent",
        ["Plugins.CW.Core.Admin.OrderApproval.DecidedBy"] = "Decided by",
        ["Plugins.CW.Core.Admin.OrderApproval.DecisionComment"] = "Reason",
        ["Plugins.CW.Core.Admin.OrderApproval.Requests.Approver"] = "Approver",
        ["Plugins.CW.Core.Admin.OrderApproval.Requests.Email"] = "E-mail",
        ["Plugins.CW.Core.Admin.OrderApproval.Requests.SentOn"] = "Sent on",
        ["Plugins.CW.Core.Admin.OrderApproval.Requests.Live"] = "Link works",
        ["Plugins.CW.Core.Admin.OrderApproval.Reason"] = "Reason (to decline)",
        ["Plugins.CW.Core.Admin.OrderApproval.Approve"] = "Approve",
        ["Plugins.CW.Core.Admin.OrderApproval.Decline"] = "Decline",
        ["Plugins.CW.Core.Admin.OrderApproval.Resend"] = "Send the links again",
        ["Plugins.CW.Core.Admin.OrderApproval.ChangeApprover"] = "Change approver",
        ["Plugins.CW.Core.Admin.OrderApproval.ApproverName"] = "Name",
        ["Plugins.CW.Core.Admin.OrderApproval.ApproverEmail"] = "E-mail",
        ["Plugins.CW.Core.Admin.OrderApproval.ChangeApprover.Button"] = "Change and send",
        ["Plugins.CW.Core.Admin.OrderApproval.Confirm.Approve"] = "Approve this order on the approvers' behalf?",
        ["Plugins.CW.Core.Admin.OrderApproval.Confirm.Decline"] = "Decline this order on the approvers' behalf? It will be cancelled and its items will go back to the customer's cart.",
        ["Plugins.CW.Core.Admin.OrderApproval.Confirm.Resend"] = "Send the approvers new links? The links already sent will stop working.",
        ["Plugins.CW.Core.Admin.OrderApproval.Confirm.Change"] = "Replace the approvers with this one? The links already sent will stop working.",
        ["Plugins.CW.Core.Admin.OrderApproval.OrderHeld"] = "This order is waiting for approval. Approve or decline it on its Approval card first.",
        ["Plugins.CW.Core.Admin.OrderApproval.Result.Done"] = "Done.",
        ["Plugins.CW.Core.Admin.OrderApproval.Result.NotFound"] = "This order has no approval.",
        ["Plugins.CW.Core.Admin.OrderApproval.Result.AlreadyDecided"] = "This order's approval has already been decided.",
        ["Plugins.CW.Core.Admin.OrderApproval.Result.LinkReplaced"] = "The link was replaced.",
        ["Plugins.CW.Core.Admin.OrderApproval.Result.Busy"] = "The order's approval is being changed right now. Please reload the page and try again.",
        ["Plugins.CW.Core.Admin.OrderApproval.Result.ReasonRequired"] = "Please enter the reason for declining.",
        ["Plugins.CW.Core.Admin.OrderApproval.Result.InvalidApprover"] = "Please enter the approver's name and a valid e-mail address.",
        ["Plugins.CW.Core.Admin.OrderApproval.Result.Failed"] = "The action could not be completed; the order is still waiting. See the log for details."
    };
}
````

### 37. ADD FILE `Plugins/Nop.Plugin.CW.Core/Services/Approvals/OrderApprovalPlacementContext.cs`

New file, 63 lines. Depends on: nothing outside this change list.

````csharp
namespace Nop.Plugin.CW.Core.Services.Approvals;

/// <summary>
/// Carries the approval decision placement made from the cart to the moment the order exists, and, for a plain order,
/// to the moment it is settled
/// </summary>
/// <remarks>
/// Placement decides from the cart, before it takes a payment, because that is when a payment method can still be
/// refused. The order is held when <c>OrderPlacedEvent</c> is published, which nop does inside its own placement for a
/// plain order, after the cart is gone and before CW settles its store credit; that order's e-mails wait for
/// <c>PlainOrderSettledEvent</c>. Registered per request (scoped): one checkout places one order.
/// </remarks>
public class OrderApprovalPlacementContext
{
    #region Fields

    private readonly Dictionary<int, OrderApprovalRequirement> _requirementsByCustomer = new();
    private readonly Dictionary<int, OrderApprovalRequirement> _notificationsByOrder = new();

    #endregion

    #region Methods

    /// <summary>
    /// Keeps the decision for the order the customer is placing
    /// </summary>
    /// <param name="customerId">Customer identifier</param>
    /// <param name="requirement">The decision</param>
    public void Set(int customerId, OrderApprovalRequirement requirement)
    {
        ArgumentNullException.ThrowIfNull(requirement);

        _requirementsByCustomer[customerId] = requirement;
    }

    /// <summary>
    /// Takes the decision for the order the customer has just placed; null when placement made none
    /// </summary>
    /// <param name="customerId">Customer identifier</param>
    public OrderApprovalRequirement Take(int customerId)
        => _requirementsByCustomer.Remove(customerId, out var requirement) ? requirement : null;

    /// <summary>
    /// Keeps a held order's e-mails until the order is settled
    /// </summary>
    /// <param name="orderId">Order identifier</param>
    /// <param name="requirement">The decision the order was held on</param>
    public void SetPendingNotification(int orderId, OrderApprovalRequirement requirement)
    {
        ArgumentNullException.ThrowIfNull(requirement);

        _notificationsByOrder[orderId] = requirement;
    }

    /// <summary>
    /// Takes a held order's waiting e-mails; null when there are none
    /// </summary>
    /// <param name="orderId">Order identifier</param>
    public OrderApprovalRequirement TakePendingNotification(int orderId)
        => _notificationsByOrder.Remove(orderId, out var requirement) ? requirement : null;

    #endregion
}
````

### 38. ADD FILE `Plugins/Nop.Plugin.CW.Core/Services/Approvals/OrderApprovalRequirement.cs`

New file, 47 lines. Depends on: nothing outside this change list.

````csharp
namespace Nop.Plugin.CW.Core.Services.Approvals;

/// <summary>
/// One line of a cart, as the approval rules see it: who it is for and where that packname sits
/// </summary>
/// <param name="PackNameId">The packname the line is for; null on a plain cart</param>
/// <param name="BranchId">The packname's branch, or on a plain cart the customer's; null when there is none</param>
/// <param name="CostCenterId">The packname's cost centre; null when there is none</param>
/// <param name="DepartmentId">The packname's department; null when there is none</param>
public record ApprovalLineScope(int? PackNameId, int? BranchId, int? CostCenterId, int? DepartmentId);

/// <summary>
/// Whether a cart needs approval, and from whom
/// </summary>
public record OrderApprovalRequirement
{
    /// <summary>
    /// A cart that needs no approval
    /// </summary>
    public static OrderApprovalRequirement NotRequired { get; } = new();

    /// <summary>
    /// Gets a value indicating whether the order must wait for an approver
    /// </summary>
    public bool Required { get; init; }

    /// <summary>
    /// Gets the amount the thresholds were compared with: goods and shipping incl tax, less subsidy
    /// </summary>
    public decimal Amount { get; init; }

    /// <summary>
    /// Gets the approvers to ask, each once
    /// </summary>
    public IReadOnlyList<int> ApproverIds { get; init; } = [];

    /// <summary>
    /// Gets the rules that asked for approval
    /// </summary>
    public IReadOnlyList<int> RuleIds { get; init; } = [];

    /// <summary>
    /// Gets a value indicating whether the e-mails wait until the order is settled: nop's own placement publishes
    /// OrderPlacedEvent before CW settles a plain order's store credit, and the e-mails show the settled totals
    /// </summary>
    public bool NotifyAfterSettlement { get; init; }
}
````

### 39. ADD FILE `Plugins/Nop.Plugin.CW.Core/Services/Approvals/OrderApprovalResult.cs`

New file, 47 lines. Depends on: nothing outside this change list.

````csharp
namespace Nop.Plugin.CW.Core.Services.Approvals;

/// <summary>
/// What happened to a decision, a resend or a change of approver
/// </summary>
public enum OrderApprovalResult
{
    /// <summary>
    /// Done
    /// </summary>
    Done = 0,

    /// <summary>
    /// No approval matches the link or the order
    /// </summary>
    NotFound = 10,

    /// <summary>
    /// The approval was already decided, expired or cancelled
    /// </summary>
    AlreadyDecided = 20,

    /// <summary>
    /// The link was replaced by a resend or a change of approver
    /// </summary>
    LinkReplaced = 30,

    /// <summary>
    /// Another change to the same approval was running; nothing was done
    /// </summary>
    Busy = 40,

    /// <summary>
    /// A decline needs a reason
    /// </summary>
    ReasonRequired = 50,

    /// <summary>
    /// The approver's name or e-mail address is missing or not valid
    /// </summary>
    InvalidApprover = 60,

    /// <summary>
    /// The order could not be cancelled or released; the approval is still pending and the error is logged
    /// </summary>
    Failed = 70
}
````

### 40. ADD FILE `Plugins/Nop.Plugin.CW.Core/Services/Approvals/OrderApprovalRuleMatcher.cs`

New file, 145 lines. Depends on: nothing outside this change list.

````csharp
using Nop.Plugin.CW.Core.Domain.Approvals;

namespace Nop.Plugin.CW.Core.Services.Approvals;

/// <summary>
/// Decides from the rules of a store whether a cart needs approval. Pure: no database, so the rules are tested alone.
/// </summary>
public static class OrderApprovalRuleMatcher
{
    #region Utilities

    /// <summary>
    /// Whether a rule's scope covers one line
    /// </summary>
    /// <param name="rule">The rule</param>
    /// <param name="line">The line</param>
    /// <param name="branchAncestors">Each branch's ancestors, nearest first, the branch itself not included</param>
    private static bool CoversLine(ApprovalRule rule, ApprovalLineScope line, IReadOnlyDictionary<int, IReadOnlyList<int>> branchAncestors)
    {
        switch (rule.ScopeType)
        {
            case ApprovalScopeType.Branch:
                if (!line.BranchId.HasValue)
                    return false;

                if (line.BranchId.Value == rule.ScopeId)
                    return true;

                return rule.IncludeChildBranches
                    && branchAncestors.TryGetValue(line.BranchId.Value, out var ancestors)
                    && ancestors.Contains(rule.ScopeId);

            case ApprovalScopeType.CostCenter:
                return line.CostCenterId == rule.ScopeId;

            case ApprovalScopeType.Department:
                return line.DepartmentId == rule.ScopeId;

            case ApprovalScopeType.PackName:
                return line.PackNameId == rule.ScopeId;

            default:
                return false;
        }
    }

    /// <summary>
    /// Whether an amount is over a rule's threshold; a rule without one (null or 0) asks for every order
    /// </summary>
    /// <param name="threshold">The rule's threshold</param>
    /// <param name="amount">The amount measured</param>
    public static bool IsOverThreshold(decimal? threshold, decimal amount)
        => !threshold.HasValue || threshold.Value <= decimal.Zero || amount > threshold.Value;

    #endregion

    #region Methods

    /// <summary>
    /// Matches the rules against a cart
    /// </summary>
    /// <param name="rules">The store's active rules</param>
    /// <param name="approverIdsByRule">Each rule's active approvers; a rule with none asks no one and is skipped</param>
    /// <param name="lines">The cart's lines</param>
    /// <param name="customerId">The customer placing the order</param>
    /// <param name="amount">Goods and shipping incl tax, less subsidy</param>
    /// <param name="branchAncestors">Each branch's ancestors, nearest first, the branch itself not included</param>
    /// <returns>The requirement: required when one rule both covers the cart and is over its threshold</returns>
    public static OrderApprovalRequirement Match(IEnumerable<ApprovalRule> rules,
        IReadOnlyDictionary<int, IReadOnlyList<int>> approverIdsByRule,
        IReadOnlyCollection<ApprovalLineScope> lines,
        int customerId,
        decimal amount,
        IReadOnlyDictionary<int, IReadOnlyList<int>> branchAncestors)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(approverIdsByRule);
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(branchAncestors);

        var ruleIds = new List<int>();
        var approverIds = new List<int>();

        foreach (var rule in rules.Where(rule => rule.Active).OrderBy(rule => rule.Id))
        {
            var covers = rule.ScopeType == ApprovalScopeType.Customer
                ? rule.ScopeId == customerId
                : lines.Any(line => CoversLine(rule, line, branchAncestors));

            if (!covers || !IsOverThreshold(rule.Threshold, amount))
                continue;

            if (!approverIdsByRule.TryGetValue(rule.Id, out var ruleApproverIds) || ruleApproverIds.Count == 0)
                continue;

            ruleIds.Add(rule.Id);

            foreach (var approverId in ruleApproverIds)
                if (!approverIds.Contains(approverId))
                    approverIds.Add(approverId);
        }

        if (ruleIds.Count == 0)
            return OrderApprovalRequirement.NotRequired with { Amount = amount };

        return new OrderApprovalRequirement
        {
            Required = true,
            Amount = amount,
            ApproverIds = approverIds,
            RuleIds = ruleIds
        };
    }

    /// <summary>
    /// Each branch's ancestors, nearest first, from the store's branches. A loop in the parent links ends the walk
    /// where it repeats.
    /// </summary>
    /// <param name="parentByBranch">Each branch's parent, null for a top branch</param>
    public static IReadOnlyDictionary<int, IReadOnlyList<int>> BuildAncestors(IReadOnlyDictionary<int, int?> parentByBranch)
    {
        ArgumentNullException.ThrowIfNull(parentByBranch);

        var result = new Dictionary<int, IReadOnlyList<int>>();

        foreach (var branchId in parentByBranch.Keys)
        {
            var ancestors = new List<int>();
            var seen = new HashSet<int> { branchId };
            var parentId = parentByBranch[branchId];

            while (parentId.HasValue && seen.Add(parentId.Value))
            {
                ancestors.Add(parentId.Value);
                parentId = parentByBranch.TryGetValue(parentId.Value, out var next) ? next : null;
            }

            result[branchId] = ancestors;
        }

        return result;
    }

    #endregion
}
````

### 41. ADD FILE `Plugins/Nop.Plugin.CW.Core/Services/Approvals/OrderApprovalRuleService.cs`

New file, 177 lines. Depends on: nothing outside this change list.

````csharp
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Orders;
using Nop.Data;
using Nop.Plugin.CW.Core.Domain.Approvals;
using Nop.Plugin.CW.Core.Domain.Settings;
using Nop.Plugin.CW.Core.Services.Branches;
using Nop.Plugin.CW.Core.Services.Exchanges;
using Nop.Plugin.CW.Core.Services.Orders;
using Nop.Plugin.CW.Core.Services.Settings;
using Nop.Plugin.CW.Core.Services.Settlement;
using Nop.Services.Configuration;
using Nop.Services.Logging;

namespace Nop.Plugin.CW.Core.Services.Approvals;

/// <summary>
/// Decides whether a cart must wait for an approver, and which payment methods such an order may use
/// </summary>
public class OrderApprovalRuleService : IOrderApprovalRuleService
{
    #region Fields

    protected readonly IBranchService _branchService;
    protected readonly ICWFeatureService _cwFeatureService;
    protected readonly IExchangeService _exchangeService;
    protected readonly ILogger _logger;
    protected readonly IOrderTotalCalculationOverrideService _orderTotalCalculationService;
    protected readonly IRepository<ApprovalRule> _approvalRuleRepository;
    protected readonly IRepository<ApprovalRuleApprover> _approvalRuleApproverRepository;
    protected readonly IRepository<Approver> _approverRepository;
    protected readonly ISettingService _settingService;
    protected readonly IShoppingCartOverrideService _shoppingCartService;

    #endregion

    #region Ctor

    public OrderApprovalRuleService(IBranchService branchService,
        ICWFeatureService cwFeatureService,
        IExchangeService exchangeService,
        ILogger logger,
        IOrderTotalCalculationOverrideService orderTotalCalculationService,
        IRepository<ApprovalRule> approvalRuleRepository,
        IRepository<ApprovalRuleApprover> approvalRuleApproverRepository,
        IRepository<Approver> approverRepository,
        ISettingService settingService,
        IShoppingCartOverrideService shoppingCartService)
    {
        _branchService = branchService;
        _cwFeatureService = cwFeatureService;
        _exchangeService = exchangeService;
        _logger = logger;
        _orderTotalCalculationService = orderTotalCalculationService;
        _approvalRuleRepository = approvalRuleRepository;
        _approvalRuleApproverRepository = approvalRuleApproverRepository;
        _approverRepository = approverRepository;
        _settingService = settingService;
        _shoppingCartService = shoppingCartService;
    }

    #endregion

    #region Utilities

    /// <summary>
    /// The active approvers of each rule; a rule whose approvers are all inactive or deleted is left out, and logged,
    /// because it would hold an order nobody is asked about
    /// </summary>
    /// <param name="rules">The rules</param>
    protected virtual async Task<IReadOnlyDictionary<int, IReadOnlyList<int>>> GetApproverIdsByRuleAsync(IList<ApprovalRule> rules)
    {
        var ruleIds = rules.Select(rule => rule.Id).ToArray();

        var pairs = await (from mapping in _approvalRuleApproverRepository.Table
                           join approver in _approverRepository.Table on mapping.ApproverId equals approver.Id
                           where ruleIds.Contains(mapping.ApprovalRuleId) && approver.Active && !approver.Deleted
                           select new { mapping.ApprovalRuleId, mapping.ApproverId })
            .ToListAsync();

        var result = pairs
            .GroupBy(pair => pair.ApprovalRuleId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<int>)group.Select(pair => pair.ApproverId).Distinct().ToList());

        foreach (var rule in rules.Where(rule => !result.ContainsKey(rule.Id)))
            await _logger.WarningAsync($"[Approvals] Rule {rule.Id} ({rule.ScopeType} {rule.ScopeId}, store {rule.StoreId}) has no active approver and holds no order.");

        return result;
    }

    /// <summary>
    /// Each branch of the store with its ancestors, nearest first
    /// </summary>
    /// <param name="storeId">Store identifier</param>
    protected virtual async Task<IReadOnlyDictionary<int, IReadOnlyList<int>>> GetBranchAncestorsAsync(int storeId)
    {
        var branches = await _branchService.GetAllBranchesByStoreIdAsync(storeId);

        return OrderApprovalRuleMatcher.BuildAncestors(branches.ToDictionary(branch => branch.Id, branch => branch.ParentBranchId));
    }

    #endregion

    #region Methods

    public virtual async Task<OrderApprovalRequirement> EvaluateCartAsync(Customer customer, int storeId, IList<ShoppingCartItem> cart)
    {
        ArgumentNullException.ThrowIfNull(customer);

        if (!await _cwFeatureService.IsEnabledAsync(CWFeature.Approvals, storeId))
            return OrderApprovalRequirement.NotRequired;

        //an exchange replaces goods already approved: its replacement lines are left out, and a cart of nothing else
        //needs no approval; the goods added beside them are judged as any order
        var replacements = await _exchangeService.GetCartReturnRequestItemsAsync(customer.Id);

        var rules = await _approvalRuleRepository.Table
            .Where(rule => rule.StoreId == storeId && rule.Active)
            .ToListAsync();

        if (rules.Count == 0)
            return OrderApprovalRequirement.NotRequired;

        var packNameCart = (await _cwFeatureService.IsEnabledAsync(CWFeature.PackName, storeId)
                ? await _shoppingCartService.GetPackNameShoppingCartItemsAsync(customer, 0, storeId)
                : [])
            .Where(item => !replacements.ContainsKey(item.Id))
            .ToList();

        List<ApprovalLineScope> lines;
        IList<ShoppingCartItem> quoteCart;

        if (packNameCart.Count > 0)
        {
            lines = packNameCart
                .Select(item => new ApprovalLineScope(item.PackNameId,
                    item.BranchId > 0 ? item.BranchId : null,
                    item.CostCenterId > 0 ? item.CostCenterId : null,
                    item.DepartmentId > 0 ? item.DepartmentId : null))
                .ToList();
            quoteCart = packNameCart.Cast<ShoppingCartItem>().ToList();
        }
        else
        {
            var branch = await _branchService.GetBranchByCustomerIdAsync(customer.Id);
            lines = [new ApprovalLineScope(null, branch?.Id, null, null)];
            quoteCart = (cart ?? []).Where(item => !replacements.ContainsKey(item.Id)).ToList();
        }

        if (quoteCart.Count == 0)
            return OrderApprovalRequirement.NotRequired;

        //what the order costs beyond the subsidy, which is itself a pre-approval: goods and shipping incl tax, store
        //credit not taken off (it is spending too)
        var plan = await _orderTotalCalculationService.QuoteCartSettlementAsync(quoteCart);
        var amount = Math.Max(decimal.Zero, plan.SettleableAmount - plan.AmountFor(SettlementSourceNames.SUBSIDY));

        return OrderApprovalRuleMatcher.Match(rules,
            await GetApproverIdsByRuleAsync(rules),
            lines,
            customer.Id,
            amount,
            await GetBranchAncestorsAsync(storeId));
    }

    public virtual async Task<bool> IsPaymentMethodAllowedAsync(int storeId, string paymentMethodSystemName)
    {
        if (string.IsNullOrWhiteSpace(paymentMethodSystemName))
            return false;

        var settings = await _settingService.LoadSettingAsync<MultiStoreSettings>(storeId);

        return settings.ApprovalPaymentMethods?.Any(name =>
            string.Equals(name?.Trim(), paymentMethodSystemName.Trim(), StringComparison.InvariantCultureIgnoreCase)) == true;
    }

    #endregion
}
````

### 42. ADD FILE `Plugins/Nop.Plugin.CW.Core/Services/Approvals/OrderApprovalService.cs`

New file, 729 lines. Depends on: nothing outside this change list.

````csharp
using Nop.Core;
using Nop.Core.Caching;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Orders;
using Nop.Data;
using Nop.Plugin.CW.Core.Domain.Approvals;
using Nop.Plugin.CW.Core.Domain.Messages;
using Nop.Plugin.CW.Core.Domain.Settings;
using Nop.Plugin.CW.Core.Services.Messages;
using Nop.Plugin.CW.Core.Services.Orders;
using Nop.Plugin.CW.Core.Services.Subsidies;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.Orders;

namespace Nop.Plugin.CW.Core.Services.Approvals;

/// <summary>
/// The approvals of held orders
/// </summary>
/// <remarks>
/// <para>
/// A held order is a real order: its subsidy, store credit and stock were taken at placement. Accepting it moves it to
/// Processing. Declining or expiring it cancels it through nop's <c>CancelOrderAsync</c>, which gives the stock, reward
/// points and gift cards back; the subsidy and store credit come back because the views leave cancelled orders out. Its
/// lines then go back to the customer's cart through <c>ReOrderAsync</c>, with their packnames.
/// </para>
/// <para>
/// Every change to one approval runs under one lock per order, and each re-reads the approval inside it: the first
/// decision wins, and a resend or a change of approver cannot cross a decision.
/// </para>
/// </remarks>
public class OrderApprovalService : IOrderApprovalService
{
    #region Fields

    protected readonly ILocalizationService _localizationService;
    protected readonly ILocker _locker;
    protected readonly ILogger _logger;
    protected readonly IOrderApprovalWorkflowMessageService _messageService;
    protected readonly IOrderProcessingOverrideService _orderProcessingService;
    protected readonly IOrderService _orderService;
    protected readonly IRepository<Approver> _approverRepository;
    protected readonly IRepository<OrderApproval> _orderApprovalRepository;
    protected readonly IRepository<OrderApprovalRequest> _requestRepository;
    protected readonly ISettingService _settingService;
    protected readonly ISubsidyCreditService _subsidyCreditService;

    #endregion

    #region Ctor

    public OrderApprovalService(ILocalizationService localizationService,
        ILocker locker,
        ILogger logger,
        IOrderApprovalWorkflowMessageService messageService,
        IOrderProcessingOverrideService orderProcessingService,
        IOrderService orderService,
        IRepository<Approver> approverRepository,
        IRepository<OrderApproval> orderApprovalRepository,
        IRepository<OrderApprovalRequest> requestRepository,
        ISettingService settingService,
        ISubsidyCreditService subsidyCreditService)
    {
        _localizationService = localizationService;
        _locker = locker;
        _logger = logger;
        _messageService = messageService;
        _orderProcessingService = orderProcessingService;
        _orderService = orderService;
        _approverRepository = approverRepository;
        _orderApprovalRepository = orderApprovalRepository;
        _requestRepository = requestRepository;
        _settingService = settingService;
        _subsidyCreditService = subsidyCreditService;
    }

    #endregion

    #region Utilities

    /// <summary>
    /// Adds a note to the order, for staff only
    /// </summary>
    protected virtual async Task AddOrderNoteAsync(Order order, string note)
    {
        await _orderService.InsertOrderNoteAsync(new OrderNote
        {
            OrderId = order.Id,
            Note = note,
            DisplayToCustomer = false,
            CreatedOnUtc = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Stops every live link of an approval
    /// </summary>
    protected virtual async Task RevokeRequestsAsync(OrderApproval approval)
    {
        var live = await _requestRepository.Table
            .Where(request => request.OrderApprovalId == approval.Id && request.RevokedOnUtc == null)
            .ToListAsync();

        if (live.Count == 0)
            return;

        var now = DateTime.UtcNow;
        foreach (var request in live)
            request.RevokedOnUtc = now;

        await _requestRepository.UpdateAsync(live);
    }

    /// <summary>
    /// Sends each approver a new link to the order. A link whose e-mail could not be queued is logged and noted on the
    /// order; the others still go.
    /// </summary>
    /// <returns>The approvers' names, comma separated</returns>
    protected virtual async Task<string> SendRequestsAsync(Order order, OrderApproval approval, IList<Approver> approvers, bool reminder)
    {
        foreach (var approver in approvers)
        {
            var token = OrderApprovalTokenHelper.CreateToken();

            var request = new OrderApprovalRequest
            {
                OrderApprovalId = approval.Id,
                ApproverId = approver.Id,
                ApproverName = approver.Name,
                ApproverEmail = approver.Email,
                TokenHash = OrderApprovalTokenHelper.HashToken(token),
                SentOnUtc = DateTime.UtcNow
            };

            await _requestRepository.InsertAsync(request);

            try
            {
                await _messageService.SendApproverRequestAsync(order, approval, request, token, reminder);
            }
            catch (Exception exception)
            {
                await _logger.ErrorAsync($"[Approvals] Order {order.Id}: the approval e-mail to {approver.Email} could not be queued.", exception);
                await AddOrderNoteAsync(order, string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.OrderApproval.Note.EmailFailed"), approver.Email));
            }
        }

        return string.Join(", ", approvers.Select(approver => approver.Name));
    }

    /// <summary>
    /// Sends a customer e-mail of the approvals; one that could not be queued is logged, the decision stands
    /// </summary>
    protected virtual async Task SendCustomerNotificationAsync(Order order, OrderApproval approval, string messageTemplateName, string approverNames)
    {
        try
        {
            await _messageService.SendCustomerNotificationAsync(order, approval, messageTemplateName, approverNames);
        }
        catch (Exception exception)
        {
            await _logger.ErrorAsync($"[Approvals] Order {order.Id}: the customer e-mail {messageTemplateName} could not be queued.", exception);
        }
    }

    /// <summary>
    /// Puts a decision that could not be carried out back to pending
    /// </summary>
    protected virtual async Task UndoDecisionAsync(OrderApproval approval)
    {
        approval.Status = OrderApprovalStatus.Pending;
        approval.DecidedBy = null;
        approval.DecidedByApproverId = null;
        approval.DecidedByCustomerId = null;
        approval.DecidedOnUtc = null;
        approval.DecisionComment = null;
        await _orderApprovalRepository.UpdateAsync(approval);
    }

    /// <summary>
    /// The approvers an approval's links went to, live or not, that can still be asked
    /// </summary>
    protected virtual async Task<IList<Approver>> GetRequestedApproversAsync(OrderApproval approval, bool liveLinksOnly)
    {
        var approverIds = await _requestRepository.Table
            .Where(request => request.OrderApprovalId == approval.Id && (!liveLinksOnly || request.RevokedOnUtc == null))
            .Select(request => request.ApproverId)
            .Distinct()
            .ToListAsync();

        return await _approverRepository.Table
            .Where(approver => approverIds.Contains(approver.Id) && !approver.Deleted)
            .OrderBy(approver => approver.Name)
            .ToListAsync();
    }

    /// <summary>
    /// Runs a change to one approval under the order's lock
    /// </summary>
    /// <param name="orderId">Order identifier</param>
    /// <param name="action">The change; it re-reads what it acts on</param>
    protected virtual async Task<OrderApprovalResult> RunLockedAsync(int orderId, Func<Task<OrderApprovalResult>> action)
    {
        var result = OrderApprovalResult.Busy;

        var ran = await _locker.PerformActionWithLockAsync(
            string.Format(OrderApprovalDefaults.DecisionLockKeyFormat, orderId),
            OrderApprovalDefaults.DecisionLockTime,
            async () =>
            {
                result = await action();
            });

        if (!ran)
            await _logger.WarningAsync($"[Approvals] Order {orderId}: another change to its approval was running; nothing was done.");

        return ran ? result : OrderApprovalResult.Busy;
    }

    /// <summary>
    /// Decides a pending approval: accepted, declined or expired
    /// </summary>
    /// <param name="orderId">Order identifier</param>
    /// <param name="outcome">Approved, Declined or Expired</param>
    /// <param name="decidedBy">Who decided, as shown to staff and the customer</param>
    /// <param name="approverId">The approver who decided, if one did</param>
    /// <param name="adminCustomerId">The admin who decided, if one did</param>
    /// <param name="comment">The reason of a decline or an expiry</param>
    /// <param name="requestId">The link used, which must still be live</param>
    protected virtual Task<OrderApprovalResult> DecideAsync(int orderId, OrderApprovalStatus outcome, string decidedBy,
        int? approverId, int? adminCustomerId, string comment, int? requestId)
    {
        return RunLockedAsync(orderId, async () =>
        {
            var approval = await GetOrderApprovalByOrderIdAsync(orderId);

            if (approval == null)
                return OrderApprovalResult.NotFound;

            if (approval.Status != OrderApprovalStatus.Pending)
                return OrderApprovalResult.AlreadyDecided;

            if (requestId.HasValue && (await _requestRepository.GetByIdAsync(requestId.Value))?.RevokedOnUtc != null)
                return OrderApprovalResult.LinkReplaced;

            var order = await _orderService.GetOrderByIdAsync(orderId);

            if (order == null || order.Deleted || order.OrderStatus == OrderStatus.Cancelled)
            {
                await EndAsCancelledAsync(approval);
                return OrderApprovalResult.AlreadyDecided;
            }

            //the approval is decided before the order moves, so the status-change guard sees an order no longer held
            approval.Status = outcome;
            approval.DecidedBy = decidedBy;
            approval.DecidedByApproverId = approverId;
            approval.DecidedByCustomerId = adminCustomerId;
            approval.DecidedOnUtc = DateTime.UtcNow;
            approval.DecisionComment = comment;
            await _orderApprovalRepository.UpdateAsync(approval);

            if (outcome == OrderApprovalStatus.Approved)
            {
                try
                {
                    await _orderProcessingService.ReleaseApprovedOrderAsync(order);
                }
                catch (Exception exception)
                {
                    await _logger.ErrorAsync($"[Approvals] Order {orderId}: releasing the approved order failed.", exception);

                    //still Pending: nothing moved, so the order can be decided again
                    if (order.OrderStatus == OrderStatus.Pending)
                    {
                        await UndoDecisionAsync(approval);
                        return OrderApprovalResult.Failed;
                    }
                }

                await AddOrderNoteAsync(order, string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.OrderApproval.Note.Approved"), decidedBy));
            }
            else
            {
                try
                {
                    if (_orderProcessingService.CanCancelOrder(order))
                        await _orderProcessingService.CancelOrderAsync(order, false);
                }
                catch (Exception exception)
                {
                    await _logger.ErrorAsync($"[Approvals] Order {orderId}: cancelling the {outcome} order failed.", exception);

                    //not cancelled: nothing moved, so the order can be decided again
                    if (order.OrderStatus != OrderStatus.Cancelled)
                    {
                        await UndoDecisionAsync(approval);
                        return OrderApprovalResult.Failed;
                    }
                }

                await AddOrderNoteAsync(order, string.Format(await _localizationService.GetResourceAsync(outcome == OrderApprovalStatus.Expired
                    ? "Plugins.CW.Core.OrderApproval.Note.Expired"
                    : "Plugins.CW.Core.OrderApproval.Note.Declined"), decidedBy, comment));

                //the lines go back after the cancel, which gave their subsidy back; the credit this request read before
                //it is stale. The order is cancelled whatever happens here, so a failure is noted, not undone.
                _subsidyCreditService.ClearPackNameCredits();

                try
                {
                    var warnings = await _orderProcessingService.ReOrderAsync(order);

                    if (warnings.Any())
                        await AddOrderNoteAsync(order, string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.OrderApproval.Note.ReOrderWarnings"), string.Join("; ", warnings)));
                }
                catch (Exception exception)
                {
                    await _logger.ErrorAsync($"[Approvals] Order {orderId}: putting the lines back in the cart failed.", exception);
                    await AddOrderNoteAsync(order, await _localizationService.GetResourceAsync("Plugins.CW.Core.OrderApproval.Note.ReOrderFailed"));
                }
            }

            await RevokeRequestsAsync(approval);

            var templateName = outcome switch
            {
                OrderApprovalStatus.Approved => CWMessageTemplateSystemNames.OrderApprovalApprovedCustomer,
                OrderApprovalStatus.Expired => CWMessageTemplateSystemNames.OrderApprovalExpiredCustomer,
                _ => CWMessageTemplateSystemNames.OrderApprovalDeclinedCustomer
            };

            await SendCustomerNotificationAsync(order, approval, templateName, null);

            return OrderApprovalResult.Done;
        });
    }

    /// <summary>
    /// Ends a pending approval whose order is gone or cancelled
    /// </summary>
    protected virtual async Task EndAsCancelledAsync(OrderApproval approval)
    {
        approval.Status = OrderApprovalStatus.Cancelled;
        approval.DecidedBy = await _localizationService.GetResourceAsync("Plugins.CW.Core.OrderApproval.DecidedBy.OrderCancelled");
        approval.DecidedOnUtc = DateTime.UtcNow;
        await _orderApprovalRepository.UpdateAsync(approval);

        await RevokeRequestsAsync(approval);
    }

    /// <summary>
    /// A decline reason as kept: trimmed and cut to the longest kept; null when empty
    /// </summary>
    protected static string NormalizeReason(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return null;

        reason = reason.Trim();

        return reason.Length > OrderApprovalDefaults.DecisionCommentMaxLength
            ? reason[..OrderApprovalDefaults.DecisionCommentMaxLength]
            : reason;
    }

    /// <summary>
    /// How an admin deciding is named
    /// </summary>
    protected virtual async Task<string> AdminNameAsync(Customer admin)
        => string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.OrderApproval.DecidedBy.Admin"), admin.Email);

    /// <summary>
    /// Sends the reminder due: a new link to each approver whose link is still live; the old links keep working
    /// </summary>
    protected virtual Task SendReminderAsync(int orderId, int reminderNumber)
    {
        return RunLockedAsync(orderId, async () =>
        {
            var approval = await GetOrderApprovalByOrderIdAsync(orderId);
            if (approval == null || approval.Status != OrderApprovalStatus.Pending || approval.LastReminderNumber >= reminderNumber)
                return OrderApprovalResult.AlreadyDecided;

            var order = await _orderService.GetOrderByIdAsync(orderId);
            if (order == null)
                return OrderApprovalResult.NotFound;

            var approvers = await GetRequestedApproversAsync(approval, liveLinksOnly: true);

            approval.LastReminderNumber = reminderNumber;
            await _orderApprovalRepository.UpdateAsync(approval);

            if (approvers.Any())
                await SendRequestsAsync(order, approval, approvers, reminder: true);

            return OrderApprovalResult.Done;
        });
    }

    #endregion

    #region Queries

    public virtual async Task<OrderApproval> GetOrderApprovalByOrderIdAsync(int orderId)
    {
        if (orderId <= 0)
            return null;

        return await _orderApprovalRepository.Table.FirstOrDefaultAsync(approval => approval.OrderId == orderId);
    }

    public virtual async Task<bool> IsOrderHeldAsync(int orderId)
    {
        if (orderId <= 0)
            return false;

        return await _orderApprovalRepository.Table
            .AnyAsync(approval => approval.OrderId == orderId && approval.StatusId == (int)OrderApprovalStatus.Pending);
    }

    public virtual async Task<IList<OrderApprovalRequest>> GetRequestsAsync(int orderApprovalId)
    {
        return await _requestRepository.Table
            .Where(request => request.OrderApprovalId == orderApprovalId)
            .OrderByDescending(request => request.SentOnUtc)
            .ThenByDescending(request => request.Id)
            .ToListAsync();
    }

    public virtual async Task<(OrderApproval approval, OrderApprovalRequest request)> GetByTokenAsync(string token)
    {
        var hash = OrderApprovalTokenHelper.HashToken(token);

        if (hash == null)
            return (null, null);

        var request = await _requestRepository.Table.FirstOrDefaultAsync(item => item.TokenHash == hash);

        if (request == null)
            return (null, null);

        return (await _orderApprovalRepository.GetByIdAsync(request.OrderApprovalId), request);
    }

    #endregion

    #region Placement

    public virtual async Task<bool> HoldOrderAsync(Order order, OrderApprovalRequirement requirement)
    {
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(requirement);

        if (!requirement.Required)
            return false;

        if (await GetOrderApprovalByOrderIdAsync(order.Id) != null)
            return true;

        var approverIds = requirement.ApproverIds.ToArray();

        //the rules asked someone, but they were switched off between the cart and the order: nobody would be asked
        if (!await _approverRepository.Table.AnyAsync(approver => approverIds.Contains(approver.Id) && approver.Active && !approver.Deleted))
        {
            await _logger.WarningAsync($"[Approvals] Order {order.Id} needed approval, but none of its approvers is active any more; it was not held.");
            await AddOrderNoteAsync(order, await _localizationService.GetResourceAsync("Plugins.CW.Core.OrderApproval.Note.NoApprover"));
            return false;
        }

        var now = DateTime.UtcNow;

        //the row is what holds the order, so it is written before anything is sent
        await _orderApprovalRepository.InsertAsync(new OrderApproval
        {
            OrderId = order.Id,
            Status = OrderApprovalStatus.Pending,
            MeasuredAmount = requirement.Amount,
            RequestedOnUtc = now,
            LastReminderNumber = 0,
            CreatedOnUtc = now
        });

        return true;
    }

    public virtual async Task NotifyHoldAsync(Order order, OrderApprovalRequirement requirement)
    {
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(requirement);

        var approval = await GetOrderApprovalByOrderIdAsync(order.Id);
        if (approval == null || approval.Status != OrderApprovalStatus.Pending)
            return;

        var approverIds = requirement.ApproverIds.ToArray();
        var approvers = await _approverRepository.Table
            .Where(approver => approverIds.Contains(approver.Id) && approver.Active && !approver.Deleted)
            .OrderBy(approver => approver.Name)
            .ToListAsync();

        var approverNames = await SendRequestsAsync(order, approval, approvers, reminder: false);

        await AddOrderNoteAsync(order, string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.OrderApproval.Note.Held"),
            approverNames, string.Join(", ", requirement.RuleIds)));

        await SendCustomerNotificationAsync(order, approval, CWMessageTemplateSystemNames.OrderApprovalAwaitingCustomer, approverNames);
    }

    public virtual async Task MarkCancelledAsync(int orderId)
    {
        var approval = await GetOrderApprovalByOrderIdAsync(orderId);

        if (approval == null || approval.Status != OrderApprovalStatus.Pending)
            return;

        await EndAsCancelledAsync(approval);

        var order = await _orderService.GetOrderByIdAsync(orderId);
        if (order != null)
            await AddOrderNoteAsync(order, await _localizationService.GetResourceAsync("Plugins.CW.Core.OrderApproval.Note.Cancelled"));
    }

    #endregion

    #region Decisions

    public virtual async Task<OrderApprovalResult> AcceptByTokenAsync(string token)
    {
        var (approval, request) = await GetByTokenAsync(token);

        if (approval == null || request == null)
            return OrderApprovalResult.NotFound;

        return await DecideAsync(approval.OrderId, OrderApprovalStatus.Approved,
            $"{request.ApproverName} ({request.ApproverEmail})", request.ApproverId, null, null, request.Id);
    }

    public virtual async Task<OrderApprovalResult> DeclineByTokenAsync(string token, string reason)
    {
        reason = NormalizeReason(reason);

        if (reason == null)
            return OrderApprovalResult.ReasonRequired;

        var (approval, request) = await GetByTokenAsync(token);

        if (approval == null || request == null)
            return OrderApprovalResult.NotFound;

        return await DecideAsync(approval.OrderId, OrderApprovalStatus.Declined,
            $"{request.ApproverName} ({request.ApproverEmail})", request.ApproverId, null, reason, request.Id);
    }

    public virtual async Task<OrderApprovalResult> AcceptByAdminAsync(int orderId, Customer admin)
    {
        ArgumentNullException.ThrowIfNull(admin);

        return await DecideAsync(orderId, OrderApprovalStatus.Approved, await AdminNameAsync(admin), null, admin.Id, null, null);
    }

    public virtual async Task<OrderApprovalResult> DeclineByAdminAsync(int orderId, Customer admin, string reason)
    {
        ArgumentNullException.ThrowIfNull(admin);

        reason = NormalizeReason(reason);

        if (reason == null)
            return OrderApprovalResult.ReasonRequired;

        return await DecideAsync(orderId, OrderApprovalStatus.Declined, await AdminNameAsync(admin), null, admin.Id, reason, null);
    }

    public virtual Task<OrderApprovalResult> ResendAsync(int orderId, Customer admin)
    {
        ArgumentNullException.ThrowIfNull(admin);

        return RunLockedAsync(orderId, async () =>
        {
            var approval = await GetOrderApprovalByOrderIdAsync(orderId);
            if (approval == null)
                return OrderApprovalResult.NotFound;

            if (approval.Status != OrderApprovalStatus.Pending)
                return OrderApprovalResult.AlreadyDecided;

            var order = await _orderService.GetOrderByIdAsync(orderId);
            if (order == null)
                return OrderApprovalResult.NotFound;

            var approvers = await GetRequestedApproversAsync(approval, liveLinksOnly: false);
            if (approvers.Count == 0)
                return OrderApprovalResult.InvalidApprover;

            await RevokeRequestsAsync(approval);

            approval.RequestedOnUtc = DateTime.UtcNow;
            approval.LastReminderNumber = 0;
            await _orderApprovalRepository.UpdateAsync(approval);

            var approverNames = await SendRequestsAsync(order, approval, approvers, reminder: false);

            await AddOrderNoteAsync(order, string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.OrderApproval.Note.Resent"), approverNames, admin.Email));

            return OrderApprovalResult.Done;
        });
    }

    public virtual async Task<OrderApprovalResult> ChangeApproverAsync(int orderId, Customer admin, string name, string email)
    {
        ArgumentNullException.ThrowIfNull(admin);

        name = name?.Trim();
        email = email?.Trim();

        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(email) || !CommonHelper.IsValidEmail(email))
            return OrderApprovalResult.InvalidApprover;

        return await RunLockedAsync(orderId, async () =>
        {
            var approval = await GetOrderApprovalByOrderIdAsync(orderId);
            if (approval == null)
                return OrderApprovalResult.NotFound;

            if (approval.Status != OrderApprovalStatus.Pending)
                return OrderApprovalResult.AlreadyDecided;

            var order = await _orderService.GetOrderByIdAsync(orderId);
            if (order == null)
                return OrderApprovalResult.NotFound;

            //the store's approver with this address, or a new one; an approver the admin names is asked even if it was
            //switched off, and is switched back on
            var approver = (await _approverRepository.Table
                    .Where(item => item.StoreId == order.StoreId && !item.Deleted)
                    .ToListAsync())
                .FirstOrDefault(item => string.Equals(item.Email, email, StringComparison.InvariantCultureIgnoreCase));

            if (approver == null)
            {
                approver = new Approver
                {
                    StoreId = order.StoreId,
                    Name = name,
                    Email = email,
                    Active = true,
                    CreatedOnUtc = DateTime.UtcNow
                };

                await _approverRepository.InsertAsync(approver);
            }
            else if (!approver.Active)
            {
                approver.Active = true;
                await _approverRepository.UpdateAsync(approver);
            }

            await RevokeRequestsAsync(approval);

            approval.RequestedOnUtc = DateTime.UtcNow;
            approval.LastReminderNumber = 0;
            await _orderApprovalRepository.UpdateAsync(approval);

            var approverNames = await SendRequestsAsync(order, approval, [approver], reminder: false);

            await AddOrderNoteAsync(order, string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.OrderApproval.Note.ApproverChanged"), approverNames, admin.Email));

            return OrderApprovalResult.Done;
        });
    }

    #endregion

    #region Task

    public virtual async Task ProcessPendingApprovalsAsync()
    {
        var pending = await _orderApprovalRepository.Table
            .Where(approval => approval.StatusId == (int)OrderApprovalStatus.Pending)
            .OrderBy(approval => approval.Id)
            .ToListAsync();

        var now = DateTime.UtcNow;

        foreach (var approval in pending)
        {
            try
            {
                var order = await _orderService.GetOrderByIdAsync(approval.OrderId);

                if (order == null || order.Deleted || order.OrderStatus == OrderStatus.Cancelled)
                {
                    await MarkCancelledAsync(approval.OrderId);
                    continue;
                }

                //an approval already waiting is finished whatever the switch says now
                var settings = await _settingService.LoadSettingAsync<MultiStoreSettings>(order.StoreId);
                var daysWaiting = (now - approval.RequestedOnUtc).TotalDays;

                if (settings.ApprovalExpiryDays > 0 && daysWaiting >= settings.ApprovalExpiryDays)
                {
                    await DecideAsync(order.Id, OrderApprovalStatus.Expired,
                        await _localizationService.GetResourceAsync("Plugins.CW.Core.OrderApproval.DecidedBy.System"),
                        null, null,
                        string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.OrderApproval.ExpiredReason"), settings.ApprovalExpiryDays),
                        null);
                    continue;
                }

                if (settings.ApprovalSecondReminderDays > 0 && approval.LastReminderNumber < 2 && daysWaiting >= settings.ApprovalSecondReminderDays)
                {
                    await SendReminderAsync(order.Id, 2);
                    continue;
                }

                if (settings.ApprovalFirstReminderDays > 0 && approval.LastReminderNumber < 1 && daysWaiting >= settings.ApprovalFirstReminderDays)
                    await SendReminderAsync(order.Id, 1);
            }
            catch (Exception exception)
            {
                await _logger.ErrorAsync($"[Approvals] Order {approval.OrderId}: the pending approval could not be processed.", exception);
            }
        }
    }

    #endregion
}
````

### 43. ADD FILE `Plugins/Nop.Plugin.CW.Core/Services/Approvals/OrderApprovalTokenHelper.cs`

New file, 33 lines. Depends on: nothing outside this change list.

````csharp
using System.Security.Cryptography;
using System.Text;

namespace Nop.Plugin.CW.Core.Services.Approvals;

/// <summary>
/// The tokens of the approval links: random, URL-safe, stored only as a hash
/// </summary>
public static class OrderApprovalTokenHelper
{
    /// <summary>
    /// Creates a token: <see cref="OrderApprovalDefaults.TokenBytes"/> random bytes, base64url without padding
    /// </summary>
    public static string CreateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(OrderApprovalDefaults.TokenBytes);

        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    /// <summary>
    /// The hash a token is stored and looked up by: SHA-256 of its UTF-8 text, as 64 lowercase hex characters
    /// </summary>
    /// <param name="token">The token from the link</param>
    /// <returns>The hash; null for an empty token</returns>
    public static string HashToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return null;

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token.Trim()))).ToLowerInvariant();
    }
}
````

### 44. ADD FILE `Plugins/Nop.Plugin.CW.Core/Services/Approvals/ProcessOrderApprovalsTask.cs`

New file, 41 lines. Depends on: nothing outside this change list.

````csharp
using Nop.Services.ScheduleTasks;

namespace Nop.Plugin.CW.Core.Services.Approvals;

/// <summary>
/// The scheduled task that sends the approval reminders and expires the approvals not answered in time
/// </summary>
/// <remarks>
/// Installed by the plugin's install and update, removed by its uninstall. nop finds it by
/// <see cref="OrderApprovalDefaults.TaskTypeFullName"/> and builds it with its dependencies.
/// </remarks>
public class ProcessOrderApprovalsTask : IScheduleTask
{
    #region Fields

    protected readonly IOrderApprovalService _orderApprovalService;

    #endregion

    #region Ctor

    public ProcessOrderApprovalsTask(IOrderApprovalService orderApprovalService)
    {
        _orderApprovalService = orderApprovalService;
    }

    #endregion

    #region Methods

    /// <summary>
    /// Executes the task
    /// </summary>
    /// <returns>A task that represents the asynchronous operation</returns>
    public virtual async Task ExecuteAsync()
    {
        await _orderApprovalService.ProcessPendingApprovalsAsync();
    }

    #endregion
}
````

### 45. ADD FILE `Plugins/Nop.Plugin.CW.Core/Services/Messages/IOrderApprovalWorkflowMessageService.cs`

New file, 38 lines. Depends on: nothing outside this change list.

````csharp
using Nop.Core.Domain.Orders;
using Nop.Plugin.CW.Core.Domain.Approvals;

namespace Nop.Plugin.CW.Core.Services.Messages;

/// <summary>
/// The e-mails of the order approvals: the requests and reminders to the approvers, and what the ordering customer is
/// told
/// </summary>
public interface IOrderApprovalWorkflowMessageService
{
    /// <summary>
    /// Sends an approver the link to review an order
    /// </summary>
    /// <param name="order">The held order</param>
    /// <param name="approval">The order's approval</param>
    /// <param name="request">The request the link belongs to</param>
    /// <param name="token">The link's token, which only the e-mail carries</param>
    /// <param name="reminder">Whether this is a reminder rather than the first request</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the queued e-mail identifiers
    /// </returns>
    Task<IList<int>> SendApproverRequestAsync(Order order, OrderApproval approval, OrderApprovalRequest request, string token, bool reminder);

    /// <summary>
    /// Tells the customer who placed the order where its approval stands
    /// </summary>
    /// <param name="order">The order</param>
    /// <param name="approval">The order's approval</param>
    /// <param name="messageTemplateName">One of the customer templates of the order approvals</param>
    /// <param name="approverNames">The approvers asked, for the awaiting e-mail</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the queued e-mail identifiers
    /// </returns>
    Task<IList<int>> SendCustomerNotificationAsync(Order order, OrderApproval approval, string messageTemplateName, string approverNames);
}
````

### 46. ADD FILE `Plugins/Nop.Plugin.CW.Core/Services/Messages/OrderApprovalWorkflowMessageService.cs`

New file, 182 lines. Depends on: nothing outside this change list.

````csharp
using Nop.Core;
using Nop.Core.Domain.Common;
using Nop.Core.Domain.Directory;
using Nop.Core.Domain.Messages;
using Nop.Core.Domain.Orders;
using Nop.Core.Events;
using Nop.Plugin.CW.Core.Domain.Approvals;
using Nop.Plugin.CW.Core.Domain.Messages;
using Nop.Plugin.CW.Core.Services.Approvals;
using Nop.Services.Affiliates;
using Nop.Services.Catalog;
using Nop.Services.Common;
using Nop.Services.Customers;
using Nop.Services.Directory;
using Nop.Services.Localization;
using Nop.Services.Messages;
using Nop.Services.Orders;
using Nop.Services.Stores;

namespace Nop.Plugin.CW.Core.Services.Messages;

/// <summary>
/// The e-mails of the order approvals
/// </summary>
/// <remarks>
/// Built on nop's own workflow service so the template lookup, the e-mail account, the language fallback and the
/// queueing are nop's. An approver is not a customer: the request goes to the address the request recorded, in the
/// language of the order.
/// </remarks>
public class OrderApprovalWorkflowMessageService : WorkflowMessageService, IOrderApprovalWorkflowMessageService
{
    #region Fields

    protected readonly ICurrencyService _currencyService;
    protected readonly IPriceFormatter _priceFormatter;
    protected readonly CurrencySettings _currencySettings;

    #endregion

    #region Ctor

    public OrderApprovalWorkflowMessageService(CommonSettings commonSettings,
        EmailAccountSettings emailAccountSettings,
        IAddressService addressService,
        IAffiliateService affiliateService,
        ICustomerService customerService,
        IEmailAccountService emailAccountService,
        IEventPublisher eventPublisher,
        ILanguageService languageService,
        ILocalizationService localizationService,
        IMessageTemplateService messageTemplateService,
        IMessageTokenProvider messageTokenProvider,
        IOrderService orderService,
        IProductService productService,
        IQueuedEmailService queuedEmailService,
        IStoreContext storeContext,
        IStoreService storeService,
        ITokenizer tokenizer,
        MessagesSettings messagesSettings,
        ICurrencyService currencyService,
        IPriceFormatter priceFormatter,
        CurrencySettings currencySettings) : base(commonSettings,
            emailAccountSettings,
            addressService,
            affiliateService,
            customerService,
            emailAccountService,
            eventPublisher,
            languageService,
            localizationService,
            messageTemplateService,
            messageTokenProvider,
            orderService,
            productService,
            queuedEmailService,
            storeContext,
            storeService,
            tokenizer,
            messagesSettings)
    {
        _currencyService = currencyService;
        _priceFormatter = priceFormatter;
        _currencySettings = currencySettings;
    }

    #endregion

    #region Utilities

    /// <summary>
    /// The tokens every approval e-mail has: the order, the customer who placed it, and the approval
    /// </summary>
    /// <param name="order">The order</param>
    /// <param name="approval">The order's approval</param>
    /// <param name="languageId">Message language identifier</param>
    protected virtual async Task<List<Token>> PrepareCommonTokensAsync(Order order, OrderApproval approval, int languageId)
    {
        var tokens = new List<Token>();
        await _messageTokenProvider.AddOrderTokensAsync(tokens, order, languageId);
        await _messageTokenProvider.AddCustomerTokensAsync(tokens, order.CustomerId);

        //the measured amount is held in the primary store currency, as the thresholds are
        var primaryCurrency = await _currencyService.GetCurrencyByIdAsync(_currencySettings.PrimaryStoreCurrencyId);
        tokens.Add(new Token("OrderApproval.Amount", await _priceFormatter.FormatPriceAsync(approval.MeasuredAmount, true, primaryCurrency, languageId, true, false)));
        tokens.Add(new Token("OrderApproval.Reason", approval.DecisionComment ?? string.Empty));
        tokens.Add(new Token("OrderApproval.DecidedBy", approval.DecidedBy ?? string.Empty));

        return tokens;
    }

    /// <summary>
    /// Queues one template to one address
    /// </summary>
    protected virtual async Task<IList<int>> SendAsync(string messageTemplateName, Order order, int languageId, List<Token> commonTokens, string toEmail, string toName)
    {
        if (string.IsNullOrWhiteSpace(toEmail))
            return new List<int>();

        var store = await _storeService.GetStoreByIdAsync(order.StoreId) ?? await _storeContext.GetCurrentStoreAsync();

        var messageTemplates = await GetActiveMessageTemplatesAsync(messageTemplateName, store.Id);
        if (!messageTemplates.Any())
            return new List<int>();

        return await messageTemplates.SelectAwait(async messageTemplate =>
        {
            var emailAccount = await GetEmailAccountOfMessageTemplateAsync(messageTemplate, languageId);

            var tokens = new List<Token>(commonTokens);
            await _messageTokenProvider.AddStoreTokensAsync(tokens, store, emailAccount, languageId);

            await _eventPublisher.MessageTokensAddedAsync(messageTemplate, tokens);

            return await SendNotificationAsync(messageTemplate, emailAccount, languageId, tokens, toEmail, toName);
        }).ToListAsync();
    }

    #endregion

    #region Methods

    public virtual async Task<IList<int>> SendApproverRequestAsync(Order order, OrderApproval approval, OrderApprovalRequest request, string token, bool reminder)
    {
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(approval);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrEmpty(token);

        var store = await _storeService.GetStoreByIdAsync(order.StoreId) ?? await _storeContext.GetCurrentStoreAsync();
        var languageId = await EnsureLanguageIsActiveAsync(order.CustomerLanguageId, store.Id);

        var tokens = await PrepareCommonTokensAsync(order, approval, languageId);
        tokens.Add(new Token("OrderApproval.ApproverName", request.ApproverName));
        tokens.Add(new Token("OrderApproval.Link", $"{store.Url.TrimEnd('/')}/{OrderApprovalDefaults.ReviewPath}/{token}", true));

        var templateName = reminder
            ? CWMessageTemplateSystemNames.OrderApprovalApproverReminder
            : CWMessageTemplateSystemNames.OrderApprovalApproverRequest;

        return await SendAsync(templateName, order, languageId, tokens, request.ApproverEmail, request.ApproverName);
    }

    public virtual async Task<IList<int>> SendCustomerNotificationAsync(Order order, OrderApproval approval, string messageTemplateName, string approverNames)
    {
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(approval);
        ArgumentException.ThrowIfNullOrEmpty(messageTemplateName);

        var customer = await _customerService.GetCustomerByIdAsync(order.CustomerId);
        if (customer == null)
            return new List<int>();

        var languageId = await EnsureLanguageIsActiveAsync(order.CustomerLanguageId, order.StoreId);

        var tokens = await PrepareCommonTokensAsync(order, approval, languageId);
        tokens.Add(new Token("OrderApproval.Approvers", approverNames ?? string.Empty));

        return await SendAsync(messageTemplateName, order, languageId, tokens, customer.Email, await _customerService.GetCustomerFullNameAsync(customer));
    }

    #endregion
}
````

### 47. ADD FILE `Plugins/Nop.Plugin.CW.Core/Views/OrderApproval/Review.cshtml`

New file, 125 lines. Depends on: nothing outside this change list.

````cshtml
@model Nop.Plugin.CW.Core.Models.Approvals.OrderApprovalReviewModel

@using Nop.Plugin.CW.Core.Services.Approvals

@{
    Layout = "_ColumnsOne";

    NopHtml.AddTitleParts(T("Plugins.CW.Core.OrderApproval.PageTitle").Text);
    NopHtml.AppendPageCssClassParts("html-order-approval-page");
}

<div class="page order-approval-page">
    <div class="page-title">
        <h1>@T("Plugins.CW.Core.OrderApproval.PageTitle")</h1>
    </div>
    <div class="page-body">
        @if (!string.IsNullOrEmpty(Model.Message))
        {
            <div class="@(Model.MessageIsError ? "message-error" : "result")">@Model.Message</div>
        }

        @if (Model.ShowOrder)
        {
            <div class="section order-approval-info">
                <p>@T("Plugins.CW.Core.OrderApproval.Intro", Model.CustomerName, Model.OrderNumber, Model.CreatedOn)</p>
                <p><strong>@T("Plugins.CW.Core.OrderApproval.Status")</strong> @Model.Status</p>
                @if (!string.IsNullOrEmpty(Model.DecidedBy))
                {
                    <p><strong>@T("Plugins.CW.Core.OrderApproval.DecidedBy")</strong> @Model.DecidedBy</p>
                }
                @if (!string.IsNullOrEmpty(Model.DecisionComment))
                {
                    <p><strong>@T("Plugins.CW.Core.OrderApproval.DecisionComment")</strong> @Model.DecisionComment</p>
                }
            </div>

            @*what was ordered, and the decision, only while this link decides the order*@
            @if (Model.CanDecide)
            {
            <div class="section products">
                <div class="table-wrapper">
                    <table class="data-table">
                        <thead>
                            <tr>
                                <th class="name">@T("Plugins.CW.Core.OrderApproval.Column.Product")</th>
                                <th class="packname">@T("Plugins.CW.Core.OrderApproval.Column.For")</th>
                                <th class="unit-price">@T("Plugins.CW.Core.OrderApproval.Column.Price")</th>
                                <th class="quantity">@T("Plugins.CW.Core.OrderApproval.Column.Quantity")</th>
                                <th class="total">@T("Plugins.CW.Core.OrderApproval.Column.Total")</th>
                            </tr>
                        </thead>
                        <tbody>
                            @foreach (var line in Model.Lines)
                            {
                                <tr>
                                    <td class="product">
                                        <em>@line.ProductName</em>
                                        @if (!string.IsNullOrEmpty(line.AttributeInfo))
                                        {
                                            <div class="attributes">@Html.Raw(line.AttributeInfo)</div>
                                        }
                                    </td>
                                    <td class="packname">@line.PackName</td>
                                    <td class="unit-price">@line.UnitPrice</td>
                                    <td class="quantity">@line.Quantity</td>
                                    <td class="total">@line.Total</td>
                                </tr>
                            }
                        </tbody>
                    </table>
                </div>
            </div>

            <div class="section totals">
                <table class="cart-total">
                    <tbody>
                        <tr>
                            <td class="cart-total-left"><label>@T("Plugins.CW.Core.OrderApproval.Totals.Gross")</label></td>
                            <td class="cart-total-right"><span class="value-summary">@Model.Gross</span></td>
                        </tr>
                        @if (!string.IsNullOrEmpty(Model.Subsidy))
                        {
                            <tr>
                                <td class="cart-total-left"><label>@T("Plugins.CW.Core.OrderApproval.Totals.Subsidy")</label></td>
                                <td class="cart-total-right"><span class="value-summary">-@Model.Subsidy</span></td>
                            </tr>
                        }
                        @if (!string.IsNullOrEmpty(Model.StoreCredit))
                        {
                            <tr>
                                <td class="cart-total-left"><label>@T("Plugins.CW.Core.OrderApproval.Totals.StoreCredit")</label></td>
                                <td class="cart-total-right"><span class="value-summary">-@Model.StoreCredit</span></td>
                            </tr>
                        }
                        <tr class="order-total">
                            <td class="cart-total-left"><label>@T("Plugins.CW.Core.OrderApproval.Totals.Payable")</label></td>
                            <td class="cart-total-right"><span class="value-summary"><strong>@Model.Payable</strong></span></td>
                        </tr>
                    </tbody>
                </table>
            </div>

                <div class="section order-approval-decision">
                    <form asp-route="@OrderApprovalDefaults.AcceptRouteName" asp-route-token="@Model.Token" method="post">
                        <div class="buttons">
                            <button type="submit" class="button-1">@T("Plugins.CW.Core.OrderApproval.Accept")</button>
                        </div>
                    </form>

                    <form asp-route="@OrderApprovalDefaults.DeclineRouteName" asp-route-token="@Model.Token" method="post">
                        <div class="form-fields">
                            <div class="inputs">
                                <label for="declineReason">@T("Plugins.CW.Core.OrderApproval.DeclineReason")</label>
                                <textarea id="declineReason" name="declineReason" rows="4" maxlength="@OrderApprovalDefaults.DecisionCommentMaxLength" required>@Model.DeclineReason</textarea>
                            </div>
                        </div>
                        <div class="buttons">
                            <button type="submit" class="button-2">@T("Plugins.CW.Core.OrderApproval.Decline")</button>
                        </div>
                    </form>
                </div>
            }
        }
    </div>
</div>
````

### 48. `Plugins/Nop.Plugin.CW.Core/Areas/Admin/Controllers/MultiStoreController.cs`

2 blocks, listed bottom to top: apply them in this order and each FIND is at the lines it states.

**48a. REPLACE - FIND (lines 3210-3215, 6 lines):**

````csharp
            await _settingService.SaveSettingOverridablePerStoreAsync(storeSettings, x => x.BulkOrderEnableOnProductPage, model.BulkOrderEnableOnProductPage_OverrideForStore, storeId, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(storeSettings, x => x.SubsidySendUserAfterLogin, model.SubsidySendUserAfterLogin_OverrideForStore, storeId, false);

            if (clearPreSelectedShippingAddress)
            {
                ClearSelectedShippingAddresses(storeId);
````

**REPLACE WITH (13 lines; the file grows by 7):**

````csharp
            await _settingService.SaveSettingOverridablePerStoreAsync(storeSettings, x => x.BulkOrderEnableOnProductPage, model.BulkOrderEnableOnProductPage_OverrideForStore, storeId, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(storeSettings, x => x.SubsidySendUserAfterLogin, model.SubsidySendUserAfterLogin_OverrideForStore, storeId, false);

            storeSettings.ApprovalPaymentMethods = (model.ApprovalPaymentMethods ?? []).Where(name => !string.IsNullOrWhiteSpace(name)).Distinct().ToList();
            await _settingService.SaveSettingOverridablePerStoreAsync(storeSettings, x => x.ApprovalsEnable, model.ApprovalsEnable_OverrideForStore, storeId, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(storeSettings, x => x.ApprovalPaymentMethods, model.ApprovalPaymentMethods_OverrideForStore, storeId, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(storeSettings, x => x.ApprovalFirstReminderDays, model.ApprovalFirstReminderDays_OverrideForStore, storeId, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(storeSettings, x => x.ApprovalSecondReminderDays, model.ApprovalSecondReminderDays_OverrideForStore, storeId, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(storeSettings, x => x.ApprovalExpiryDays, model.ApprovalExpiryDays_OverrideForStore, storeId, false);

            if (clearPreSelectedShippingAddress)
            {
                ClearSelectedShippingAddresses(storeId);
````

**48b. REPLACE - FIND (lines 2938-2943, 6 lines):**

````csharp
            await _settingService.SaveSettingOverridablePerStoreAsync(storeSettings, x => x.BulkOrderEnableOnProductPage, model.BulkOrderEnableOnProductPage_OverrideForStore, storeId, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(storeSettings, x => x.SubsidySendUserAfterLogin, model.SubsidySendUserAfterLogin_OverrideForStore, storeId, false);

            model.DefaultPackNameCustomerRoleIds ??= [];

            storeSettings.DefaultPackNameCustomerRoleIds = model.DefaultPackNameCustomerRoleIds.ToList();
````

**REPLACE WITH (13 lines; the file grows by 7):**

````csharp
            await _settingService.SaveSettingOverridablePerStoreAsync(storeSettings, x => x.BulkOrderEnableOnProductPage, model.BulkOrderEnableOnProductPage_OverrideForStore, storeId, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(storeSettings, x => x.SubsidySendUserAfterLogin, model.SubsidySendUserAfterLogin_OverrideForStore, storeId, false);

            storeSettings.ApprovalPaymentMethods = (model.ApprovalPaymentMethods ?? []).Where(name => !string.IsNullOrWhiteSpace(name)).Distinct().ToList();
            await _settingService.SaveSettingOverridablePerStoreAsync(storeSettings, x => x.ApprovalsEnable, model.ApprovalsEnable_OverrideForStore, storeId, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(storeSettings, x => x.ApprovalPaymentMethods, model.ApprovalPaymentMethods_OverrideForStore, storeId, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(storeSettings, x => x.ApprovalFirstReminderDays, model.ApprovalFirstReminderDays_OverrideForStore, storeId, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(storeSettings, x => x.ApprovalSecondReminderDays, model.ApprovalSecondReminderDays_OverrideForStore, storeId, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(storeSettings, x => x.ApprovalExpiryDays, model.ApprovalExpiryDays_OverrideForStore, storeId, false);

            model.DefaultPackNameCustomerRoleIds ??= [];

            storeSettings.DefaultPackNameCustomerRoleIds = model.DefaultPackNameCustomerRoleIds.ToList();
````

### 49. `Plugins/Nop.Plugin.CW.Core/Areas/Admin/Factories/CWSettingModelFactory.cs`

5 blocks, listed bottom to top: apply them in this order and each FIND is at the lines it states.

**49a. REPLACE - FIND (lines 196-201, 6 lines):**

````csharp
        foreach (var shippingMethodName in await _packNameShippingMethodService.GetShippingMethodNamesAsync(storeId))
            model.AvailableShippingMethods.Add(new SelectListItem { Value = shippingMethodName, Text = shippingMethodName });

        return model;
    }

````

**REPLACE WITH (14 lines; the file grows by 8):**

````csharp
        foreach (var shippingMethodName in await _packNameShippingMethodService.GetShippingMethodNamesAsync(storeId))
            model.AvailableShippingMethods.Add(new SelectListItem { Value = shippingMethodName, Text = shippingMethodName });

        //every installed payment method, active or not, so a method can be listed before it is switched on
        foreach (var paymentMethod in await _paymentPluginManager.LoadAllPluginsAsync(storeId: storeId))
            model.AvailableApprovalPaymentMethods.Add(new SelectListItem
            {
                Value = paymentMethod.PluginDescriptor.SystemName,
                Text = paymentMethod.PluginDescriptor.FriendlyName
            });

        return model;
    }

````

**49b. REPLACE - FIND (lines 186-191, 6 lines):**

````csharp

            model.DefaultPackNameCustomerRoleIds = storeSettings.DefaultPackNameCustomerRoleIds;
            model.DefaultPackNameCustomerRoleIds_OverrideForStore = await _settingService.SettingExistsAsync(storeSettings, x => x.DefaultPackNameCustomerRoleIds, storeId);
        }

        //fill in additional values (not existing in the entity)
````

**REPLACE WITH (13 lines; the file grows by 7):**

````csharp

            model.DefaultPackNameCustomerRoleIds = storeSettings.DefaultPackNameCustomerRoleIds;
            model.DefaultPackNameCustomerRoleIds_OverrideForStore = await _settingService.SettingExistsAsync(storeSettings, x => x.DefaultPackNameCustomerRoleIds, storeId);

            model.ApprovalPaymentMethods = storeSettings.ApprovalPaymentMethods;
            model.ApprovalsEnable_OverrideForStore = await _settingService.SettingExistsAsync(storeSettings, x => x.ApprovalsEnable, storeId);
            model.ApprovalPaymentMethods_OverrideForStore = await _settingService.SettingExistsAsync(storeSettings, x => x.ApprovalPaymentMethods, storeId);
            model.ApprovalFirstReminderDays_OverrideForStore = await _settingService.SettingExistsAsync(storeSettings, x => x.ApprovalFirstReminderDays, storeId);
            model.ApprovalSecondReminderDays_OverrideForStore = await _settingService.SettingExistsAsync(storeSettings, x => x.ApprovalSecondReminderDays, storeId);
            model.ApprovalExpiryDays_OverrideForStore = await _settingService.SettingExistsAsync(storeSettings, x => x.ApprovalExpiryDays, storeId);
        }

        //fill in additional values (not existing in the entity)
````

**49c. REPLACE - FIND (lines 29-41, 13 lines):**

````csharp
        IProductAttributeService productAttributeService,
        ICustomerService customerService,
        ILocalizationService localizationService,
        IPackNameShippingMethodService packNameShippingMethodService)
    {
        _settingService = settingService;
        _productAttributeService = productAttributeService;
        _customerService = customerService;
        _localizationService = localizationService;
        _packNameShippingMethodService = packNameShippingMethodService;
    }

    #endregion
````

**REPLACE WITH (15 lines; the file grows by 2):**

````csharp
        IProductAttributeService productAttributeService,
        ICustomerService customerService,
        ILocalizationService localizationService,
        IPackNameShippingMethodService packNameShippingMethodService,
        IPaymentPluginManager paymentPluginManager)
    {
        _settingService = settingService;
        _productAttributeService = productAttributeService;
        _customerService = customerService;
        _localizationService = localizationService;
        _packNameShippingMethodService = packNameShippingMethodService;
        _paymentPluginManager = paymentPluginManager;
    }

    #endregion
````

**49d. REPLACE - FIND (lines 20-25, 6 lines):**

````csharp
    protected readonly ICustomerService _customerService;
    protected readonly ILocalizationService _localizationService;
    protected readonly IPackNameShippingMethodService _packNameShippingMethodService;

    #endregion

````

**REPLACE WITH (7 lines; the file grows by 1):**

````csharp
    protected readonly ICustomerService _customerService;
    protected readonly ILocalizationService _localizationService;
    protected readonly IPackNameShippingMethodService _packNameShippingMethodService;
    protected readonly IPaymentPluginManager _paymentPluginManager;

    #endregion

````

**49e. REPLACE - FIND (lines 7-12, 6 lines):**

````csharp
using Nop.Services.Configuration;
using Nop.Services.Customers;
using Nop.Services.Localization;
using Nop.Web.Areas.Admin.Infrastructure.Mapper.Extensions;

namespace Nop.Plugin.CW.Core.Areas.Admin.Factories;
````

**REPLACE WITH (7 lines; the file grows by 1):**

````csharp
using Nop.Services.Configuration;
using Nop.Services.Customers;
using Nop.Services.Localization;
using Nop.Services.Payments;
using Nop.Web.Areas.Admin.Infrastructure.Mapper.Extensions;

namespace Nop.Plugin.CW.Core.Areas.Admin.Factories;
````

### 50. `Plugins/Nop.Plugin.CW.Core/Areas/Admin/Models/Settings/MultiStoreSettingsModel.cs`

2 blocks, listed bottom to top: apply them in this order and each FIND is at the lines it states.

**50a. REPLACE - FIND (lines 247-252, 6 lines):**

````csharp

    #endregion

    #region Store

    [NopResourceDisplayName("Plugins.CW.Core.Admin.MultiStores.Settings.DefaultPackNameCustomerRoleIds")]
````

**REPLACE WITH (31 lines; the file grows by 25):**

````csharp

    #endregion

    #region Order approvals

    [NopResourceDisplayName("Plugins.CW.Core.Admin.MultiStores.Settings.ApprovalsEnable")]
    public bool ApprovalsEnable { get; set; }
    public bool ApprovalsEnable_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.MultiStores.Settings.ApprovalPaymentMethods")]
    public IList<string> ApprovalPaymentMethods { get; set; }
    public bool ApprovalPaymentMethods_OverrideForStore { get; set; }
    public IList<SelectListItem> AvailableApprovalPaymentMethods { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.MultiStores.Settings.ApprovalFirstReminderDays")]
    public int ApprovalFirstReminderDays { get; set; }
    public bool ApprovalFirstReminderDays_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.MultiStores.Settings.ApprovalSecondReminderDays")]
    public int ApprovalSecondReminderDays { get; set; }
    public bool ApprovalSecondReminderDays_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.MultiStores.Settings.ApprovalExpiryDays")]
    public int ApprovalExpiryDays { get; set; }
    public bool ApprovalExpiryDays_OverrideForStore { get; set; }

    #endregion

    #region Store

    [NopResourceDisplayName("Plugins.CW.Core.Admin.MultiStores.Settings.DefaultPackNameCustomerRoleIds")]
````

**50b. REPLACE - FIND (lines 15-20, 6 lines):**

````csharp
        AvailableProductAttributes = new List<SelectListItem>();
        AvailableBulkOrderViewModes = new List<SelectListItem>();
        BulkOrderAxisAttributeIds = new List<int>();
    }

    #endregion
````

**REPLACE WITH (8 lines; the file grows by 2):**

````csharp
        AvailableProductAttributes = new List<SelectListItem>();
        AvailableBulkOrderViewModes = new List<SelectListItem>();
        BulkOrderAxisAttributeIds = new List<int>();
        ApprovalPaymentMethods = new List<string>();
        AvailableApprovalPaymentMethods = new List<SelectListItem>();
    }

    #endregion
````

### 51. `Plugins/Nop.Plugin.CW.Core/Areas/Admin/Validators/Settings/MultiStoreSettingsValidator.cs`

1 block.

**51a. REPLACE - FIND (lines 18-22, 5 lines):**

````csharp
            .When(x => x.PackNameEnable && x.SubsidyEnable && x.AllowManagersToAddPackNames && x.AllowSubsidySelection && x.AllowToAddByExcel
                && (x.StoreId == 0 || x.AllowSubsidyCountByExcel_OverrideForStore))
            .WithMessageAwait(localizationService.GetResourceAsync("Plugins.CW.Core.Admin.MultiStores.Settings.AllowSubsidyCountByExcel.Range"));
    }
}
````

**REPLACE WITH (19 lines; the file grows by 14):**

````csharp
            .When(x => x.PackNameEnable && x.SubsidyEnable && x.AllowManagersToAddPackNames && x.AllowSubsidySelection && x.AllowToAddByExcel
                && (x.StoreId == 0 || x.AllowSubsidyCountByExcel_OverrideForStore))
            .WithMessageAwait(localizationService.GetResourceAsync("Plugins.CW.Core.Admin.MultiStores.Settings.AllowSubsidyCountByExcel.Range"));

        //the approval days: posted the same way, and shown while approvals are on; 0 switches each one off
        RuleFor(x => x.ApprovalFirstReminderDays)
            .GreaterThanOrEqualTo(0)
            .When(x => x.ApprovalsEnable && (x.StoreId == 0 || x.ApprovalFirstReminderDays_OverrideForStore))
            .WithMessageAwait(localizationService.GetResourceAsync("Plugins.CW.Core.Admin.MultiStores.Settings.ApprovalDays.NotNegative"));
        RuleFor(x => x.ApprovalSecondReminderDays)
            .GreaterThanOrEqualTo(0)
            .When(x => x.ApprovalsEnable && (x.StoreId == 0 || x.ApprovalSecondReminderDays_OverrideForStore))
            .WithMessageAwait(localizationService.GetResourceAsync("Plugins.CW.Core.Admin.MultiStores.Settings.ApprovalDays.NotNegative"));
        RuleFor(x => x.ApprovalExpiryDays)
            .GreaterThanOrEqualTo(0)
            .When(x => x.ApprovalsEnable && (x.StoreId == 0 || x.ApprovalExpiryDays_OverrideForStore))
            .WithMessageAwait(localizationService.GetResourceAsync("Plugins.CW.Core.Admin.MultiStores.Settings.ApprovalDays.NotNegative"));
    }
}
````

### 52. `Plugins/Nop.Plugin.CW.Core/Areas/Admin/Views/MultiStore/_StoreSettings.cshtml`

4 blocks, listed bottom to top: apply them in this order and each FIND is at the lines it states.

**52a. REPLACE - FIND (lines 672-677, 6 lines):**

````cshtml
        toggleRow('#pnlBulkOrderSettings', bulkOrder);
        toggleRow('#pnlBulkOrderProductImageSize', bulkOrder && isChecked('bulkOrderShowProductImages'));
        toggleRow('#pnlBulkOrderAttributeImageSize', bulkOrder && isChecked('bulkOrderShowAttributeImages'));
    }

    //ONE GENERATOR FOR BOTH BUTTONS. The two markup scripts were the same twenty lines with three
````

**REPLACE WITH (9 lines; the file grows by 3):**

````cshtml
        toggleRow('#pnlBulkOrderSettings', bulkOrder);
        toggleRow('#pnlBulkOrderProductImageSize', bulkOrder && isChecked('bulkOrderShowProductImages'));
        toggleRow('#pnlBulkOrderAttributeImageSize', bulkOrder && isChecked('bulkOrderShowAttributeImages'));

        //Order approvals card
        toggleRow('#pnlApprovalSettings', isChecked('approvalsEnable'));
    }

    //ONE GENERATOR FOR BOTH BUTTONS. The two markup scripts were the same twenty lines with three
````

**52b. REPLACE - FIND (lines 595-601, 7 lines):**

````cshtml
        ["allowToAddByExcel"]            = Html.IdFor(model => model.AllowToAddByExcel).ToString(),
        ["bulkOrderEnable"]              = Html.IdFor(model => model.BulkOrderEnable).ToString(),
        ["bulkOrderShowProductImages"]   = Html.IdFor(model => model.BulkOrderShowProductImages).ToString(),
        ["bulkOrderShowAttributeImages"] = Html.IdFor(model => model.BulkOrderShowAttributeImages).ToString()
    };
}

````

**REPLACE WITH (8 lines; the file grows by 1):**

````cshtml
        ["allowToAddByExcel"]            = Html.IdFor(model => model.AllowToAddByExcel).ToString(),
        ["bulkOrderEnable"]              = Html.IdFor(model => model.BulkOrderEnable).ToString(),
        ["bulkOrderShowProductImages"]   = Html.IdFor(model => model.BulkOrderShowProductImages).ToString(),
        ["bulkOrderShowAttributeImages"] = Html.IdFor(model => model.BulkOrderShowAttributeImages).ToString(),
        ["approvalsEnable"]              = Html.IdFor(model => model.ApprovalsEnable).ToString()
    };
}

````

**52c. REPLACE - FIND (lines 561-566, 6 lines):**

````cshtml
                </div>
            </div>
        </nop-card>
        <nop-card asp-name="register-defaults" asp-icon="fas fa-user" asp-title="@T("Plugins.CW.Core.Admin.MultiStores.Settings.Registration")" asp-hide-block-attribute-name="@hideRegisterBlockAttributeName" asp-hide="@hideRegisterBlock" asp-advanced="false">
            <div class="card-body">
                <div class="form-group row pnlPackNameOnly">
````

**REPLACE WITH (62 lines; the file grows by 56):**

````cshtml
                </div>
            </div>
        </nop-card>
        <nop-card asp-name="approvals-defaults" asp-icon="fas fa-user-check" asp-title="@T("Plugins.CW.Core.Admin.MultiStores.Settings.ApprovalsSection")" asp-hide-block-attribute-name="@hideApprovalsBlockAttributeName" asp-hide="@hideApprovalsBlock" asp-advanced="false">
            <div class="card-body">
                <div class="form-group row">
                    <div class="col-md-3">
                        <nop-override-store-checkbox asp-for="ApprovalsEnable_OverrideForStore" asp-input="ApprovalsEnable" asp-store-scope="@Model.ActiveStoreScopeConfiguration" />
                        <nop-label asp-for="ApprovalsEnable" />
                    </div>
                    <div class="col-md-9">
                        <nop-editor asp-for="ApprovalsEnable" />
                        <span asp-validation-for="ApprovalsEnable"></span>
                    </div>
                </div>
                <div id="pnlApprovalSettings">
                    <div class="form-group row">
                        <div class="col-md-3">
                            <nop-override-store-checkbox asp-for="ApprovalPaymentMethods_OverrideForStore" asp-input="ApprovalPaymentMethods" asp-store-scope="@Model.ActiveStoreScopeConfiguration" />
                            <nop-label asp-for="ApprovalPaymentMethods" />
                        </div>
                        <div class="col-md-9">
                            <nop-select asp-for="ApprovalPaymentMethods" asp-items="Model.AvailableApprovalPaymentMethods" asp-multiple="true" />
                            <span asp-validation-for="ApprovalPaymentMethods"></span>
                        </div>
                    </div>
                    <div class="form-group row">
                        <div class="col-md-3">
                            <nop-override-store-checkbox asp-for="ApprovalFirstReminderDays_OverrideForStore" asp-input="ApprovalFirstReminderDays" asp-store-scope="@Model.ActiveStoreScopeConfiguration" />
                            <nop-label asp-for="ApprovalFirstReminderDays" />
                        </div>
                        <div class="col-md-9">
                            <nop-editor asp-for="ApprovalFirstReminderDays" />
                            <span asp-validation-for="ApprovalFirstReminderDays"></span>
                        </div>
                    </div>
                    <div class="form-group row">
                        <div class="col-md-3">
                            <nop-override-store-checkbox asp-for="ApprovalSecondReminderDays_OverrideForStore" asp-input="ApprovalSecondReminderDays" asp-store-scope="@Model.ActiveStoreScopeConfiguration" />
                            <nop-label asp-for="ApprovalSecondReminderDays" />
                        </div>
                        <div class="col-md-9">
                            <nop-editor asp-for="ApprovalSecondReminderDays" />
                            <span asp-validation-for="ApprovalSecondReminderDays"></span>
                        </div>
                    </div>
                    <div class="form-group row">
                        <div class="col-md-3">
                            <nop-override-store-checkbox asp-for="ApprovalExpiryDays_OverrideForStore" asp-input="ApprovalExpiryDays" asp-store-scope="@Model.ActiveStoreScopeConfiguration" />
                            <nop-label asp-for="ApprovalExpiryDays" />
                        </div>
                        <div class="col-md-9">
                            <nop-editor asp-for="ApprovalExpiryDays" />
                            <span asp-validation-for="ApprovalExpiryDays"></span>
                        </div>
                    </div>
                </div>
            </div>
        </nop-card>
        <nop-card asp-name="register-defaults" asp-icon="fas fa-user" asp-title="@T("Plugins.CW.Core.Admin.MultiStores.Settings.Registration")" asp-hide-block-attribute-name="@hideRegisterBlockAttributeName" asp-hide="@hideRegisterBlock" asp-advanced="false">
            <div class="card-body">
                <div class="form-group row pnlPackNameOnly">
````

**52d. REPLACE - FIND (lines 20-25, 6 lines):**

````cshtml

    const string hideBulkOrderBlockAttributeName = "MultiStoreSettings.HideBulkOrderBlock";
    var hideBulkOrderBlock = await genericAttributeService.GetAttributeAsync<bool>(customer, hideBulkOrderBlockAttributeName);
}

<form id="multistore-settings-form" asp-controller="MultiStore" asp-action="StoreSettings" method="post">
````

**REPLACE WITH (9 lines; the file grows by 3):**

````cshtml

    const string hideBulkOrderBlockAttributeName = "MultiStoreSettings.HideBulkOrderBlock";
    var hideBulkOrderBlock = await genericAttributeService.GetAttributeAsync<bool>(customer, hideBulkOrderBlockAttributeName);

    const string hideApprovalsBlockAttributeName = "MultiStoreSettings.HideApprovalsBlock";
    var hideApprovalsBlock = await genericAttributeService.GetAttributeAsync<bool>(customer, hideApprovalsBlockAttributeName);
}

<form id="multistore-settings-form" asp-controller="MultiStore" asp-action="StoreSettings" method="post">
````

### 53. `Plugins/Nop.Plugin.CW.Core/Areas/Admin/Views/MultiStore/Settings.cshtml`

3 blocks, listed bottom to top: apply them in this order and each FIND is at the lines it states.

**53a. REPLACE - FIND (lines 492-496, 5 lines):**

````cshtml
        toggleRow('#pnlBulkOrderSettings', bulkOrder);
        toggleRow('#pnlBulkOrderProductImageSize', bulkOrder && isChecked('@Html.IdFor(model => model.BulkOrderShowProductImages)'));
        toggleRow('#pnlBulkOrderAttributeImageSize', bulkOrder && isChecked('@Html.IdFor(model => model.BulkOrderShowAttributeImages)'));
    }
</script>
````

**REPLACE WITH (7 lines; the file grows by 2):**

````cshtml
        toggleRow('#pnlBulkOrderSettings', bulkOrder);
        toggleRow('#pnlBulkOrderProductImageSize', bulkOrder && isChecked('@Html.IdFor(model => model.BulkOrderShowProductImages)'));
        toggleRow('#pnlBulkOrderAttributeImageSize', bulkOrder && isChecked('@Html.IdFor(model => model.BulkOrderShowAttributeImages)'));

        toggleRow('#pnlApprovalSettings', isChecked('@Html.IdFor(model => model.ApprovalsEnable)'));
    }
</script>
````

**53b. REPLACE - FIND (lines 436-441, 6 lines):**

````cshtml
                            </div>
                        </div>
                    </nop-card>
                </nop-cards>
            </div>
        </div>
````

**REPLACE WITH (47 lines; the file grows by 41):**

````cshtml
                            </div>
                        </div>
                    </nop-card>
                    <nop-card asp-name="approvals-defaults" asp-icon="fas fa-user-check" asp-title="@T("Plugins.CW.Core.Admin.MultiStores.Settings.ApprovalsSection")" asp-hide-block-attribute-name="@hideApprovalsBlockAttributeName" asp-hide="@hideApprovalsBlock" asp-advanced="false">
                        <div class="card-body">
                            <div class="form-group row">
                                <div class="col-md-3"><nop-label asp-for="ApprovalsEnable" /></div>
                                <div class="col-md-9">
                                    <nop-editor asp-for="ApprovalsEnable" />
                                    <span asp-validation-for="ApprovalsEnable"></span>
                                </div>
                            </div>
                            <div id="pnlApprovalSettings">
                                <div class="form-group row">
                                    <div class="col-md-3"><nop-label asp-for="ApprovalPaymentMethods" /></div>
                                    <div class="col-md-9">
                                        <nop-select asp-for="ApprovalPaymentMethods" asp-items="Model.AvailableApprovalPaymentMethods" asp-multiple="true" />
                                        <span asp-validation-for="ApprovalPaymentMethods"></span>
                                    </div>
                                </div>
                                <div class="form-group row">
                                    <div class="col-md-3"><nop-label asp-for="ApprovalFirstReminderDays" /></div>
                                    <div class="col-md-9">
                                        <nop-editor asp-for="ApprovalFirstReminderDays" />
                                        <span asp-validation-for="ApprovalFirstReminderDays"></span>
                                    </div>
                                </div>
                                <div class="form-group row">
                                    <div class="col-md-3"><nop-label asp-for="ApprovalSecondReminderDays" /></div>
                                    <div class="col-md-9">
                                        <nop-editor asp-for="ApprovalSecondReminderDays" />
                                        <span asp-validation-for="ApprovalSecondReminderDays"></span>
                                    </div>
                                </div>
                                <div class="form-group row">
                                    <div class="col-md-3"><nop-label asp-for="ApprovalExpiryDays" /></div>
                                    <div class="col-md-9">
                                        <nop-editor asp-for="ApprovalExpiryDays" />
                                        <span asp-validation-for="ApprovalExpiryDays"></span>
                                    </div>
                                </div>
                            </div>
                        </div>
                    </nop-card>
                </nop-cards>
            </div>
        </div>
````

**53c. REPLACE - FIND (lines 25-30, 6 lines):**

````cshtml

    const string hideBulkOrderBlockAttributeName = "MultiStoreSettings.HideBulkOrderBlock";
    var hideBulkOrderBlock = await genericAttributeService.GetAttributeAsync<bool>(customer, hideBulkOrderBlockAttributeName);
}

<form id="cw-default-settings-form" asp-controller="MultiStore" asp-action="Settings" method="post">
````

**REPLACE WITH (9 lines; the file grows by 3):**

````cshtml

    const string hideBulkOrderBlockAttributeName = "MultiStoreSettings.HideBulkOrderBlock";
    var hideBulkOrderBlock = await genericAttributeService.GetAttributeAsync<bool>(customer, hideBulkOrderBlockAttributeName);

    const string hideApprovalsBlockAttributeName = "MultiStoreSettings.HideApprovalsBlock";
    var hideApprovalsBlock = await genericAttributeService.GetAttributeAsync<bool>(customer, hideApprovalsBlockAttributeName);
}

<form id="cw-default-settings-form" asp-controller="MultiStore" asp-action="Settings" method="post">
````

### 54. `Plugins/Nop.Plugin.CW.Core/Areas/Admin/Views/Order/Edit.cshtml`

1 block.

**54a. REPLACE - FIND (lines 82-87, 6 lines):**

````cshtml
            <div class="form-horizontal">

                <nop-cards id="order-cards">
                    <nop-card asp-name="order-info" asp-icon="fas fa-info" asp-title="@T("Admin.Orders.Info")" asp-hide-block-attribute-name="@hideInfoBlockAttributeName" asp-hide="@hideInfoBlock" asp-advanced="false">@await Html.PartialAsync("_CWOrderDetails.Info", Model)</nop-card>

                    <nop-card asp-name="order-billing-shipping" asp-icon="fas fa-truck" asp-title="@T("Admin.Orders.BillingShippingInfo")" asp-hide-block-attribute-name="@hideBillingAndShippingBlockAttributeName" asp-hide="@hideBillingAndShippingBlock" asp-advanced="false">
````

**REPLACE WITH (8 lines; the file grows by 2):**

````cshtml
            <div class="form-horizontal">

                <nop-cards id="order-cards">
                    @await Component.InvokeAsync("OrderApprovalAdmin", new { orderId = Model.Id })

                    <nop-card asp-name="order-info" asp-icon="fas fa-info" asp-title="@T("Admin.Orders.Info")" asp-hide-block-attribute-name="@hideInfoBlockAttributeName" asp-hide="@hideInfoBlock" asp-advanced="false">@await Html.PartialAsync("_CWOrderDetails.Info", Model)</nop-card>

                    <nop-card asp-name="order-billing-shipping" asp-icon="fas fa-truck" asp-title="@T("Admin.Orders.BillingShippingInfo")" asp-hide-block-attribute-name="@hideBillingAndShippingBlockAttributeName" asp-hide="@hideBillingAndShippingBlock" asp-advanced="false">
````

### 55. `Plugins/Nop.Plugin.CW.Core/CWCorePlugin.cs`

7 blocks, listed bottom to top: apply them in this order and each FIND is at the lines it states.

**55a. REPLACE - FIND (lines 2373-2378, 6 lines):**

````csharp
        if (subsidyExpiryTask != null)
            await _scheduleTaskService.DeleteTaskAsync(subsidyExpiryTask);

        await base.UninstallAsync();
    }

````

**REPLACE WITH (8 lines; the file grows by 2):**

````csharp
        if (subsidyExpiryTask != null)
            await _scheduleTaskService.DeleteTaskAsync(subsidyExpiryTask);

        await _orderApprovalInstallService.UninstallAsync();

        await base.UninstallAsync();
    }

````

**55b. REPLACE - FIND (lines 2319-2324, 6 lines):**

````csharp
            ["Admin.Common.Actions"] = "Actions",
            ["Admin.Common.Create"] = "Create",
        });
    }

    #endregion
````

**REPLACE WITH (8 lines; the file grows by 2):**

````csharp
            ["Admin.Common.Actions"] = "Actions",
            ["Admin.Common.Create"] = "Create",
        });

        await _localizationService.AddOrUpdateLocaleResourceAsync(OrderApprovalLocaleResources.Resources);
    }

    #endregion
````

**55c. REPLACE - FIND (lines 100-105, 6 lines):**

````csharp
        await InstallCustomerRegistrationTemplateAsync();
        await InstallSubsidyExpiryReminderTemplatesAsync();
        await InstallSubsidyExpiryReminderTaskAsync();
        await AddPackNameToReturnReceiptTemplateAsync();
        await AddAdditionalFeeToPaymentTemplateAsync();
        await PointCustomerRegistrationTemplateAtSubsidyDetailsAsync();
````

**REPLACE WITH (7 lines; the file grows by 1):**

````csharp
        await InstallCustomerRegistrationTemplateAsync();
        await InstallSubsidyExpiryReminderTemplatesAsync();
        await InstallSubsidyExpiryReminderTaskAsync();
        await _orderApprovalInstallService.InstallOrUpdateAsync();
        await AddPackNameToReturnReceiptTemplateAsync();
        await AddAdditionalFeeToPaymentTemplateAsync();
        await PointCustomerRegistrationTemplateAtSubsidyDetailsAsync();
````

**55d. REPLACE - FIND (lines 85-90, 6 lines):**

````csharp
        _emailAccountService = emailAccountService;
        _emailAccountSettings = emailAccountSettings;
        _scheduleTaskService = scheduleTaskService;
    }

    #endregion
````

**REPLACE WITH (7 lines; the file grows by 1):**

````csharp
        _emailAccountService = emailAccountService;
        _emailAccountSettings = emailAccountSettings;
        _scheduleTaskService = scheduleTaskService;
        _orderApprovalInstallService = orderApprovalInstallService;
    }

    #endregion
````

**55e. REPLACE - FIND (lines 74-80, 7 lines):**

````csharp
        IMessageTemplateService messageTemplateService,
        IEmailAccountService emailAccountService,
        EmailAccountSettings emailAccountSettings,
        IScheduleTaskService scheduleTaskService)
    {
        _settingService = settingService;
        _widgetSettings = widgetSettings;
````

**REPLACE WITH (8 lines; the file grows by 1):**

````csharp
        IMessageTemplateService messageTemplateService,
        IEmailAccountService emailAccountService,
        EmailAccountSettings emailAccountSettings,
        IScheduleTaskService scheduleTaskService,
        IOrderApprovalInstallService orderApprovalInstallService)
    {
        _settingService = settingService;
        _widgetSettings = widgetSettings;
````

**55f. REPLACE - FIND (lines 60-65, 6 lines):**

````csharp
    private readonly IEmailAccountService _emailAccountService;
    private readonly EmailAccountSettings _emailAccountSettings;
    private readonly IScheduleTaskService _scheduleTaskService;

    #endregion

````

**REPLACE WITH (7 lines; the file grows by 1):**

````csharp
    private readonly IEmailAccountService _emailAccountService;
    private readonly EmailAccountSettings _emailAccountSettings;
    private readonly IScheduleTaskService _scheduleTaskService;
    private readonly IOrderApprovalInstallService _orderApprovalInstallService;

    #endregion

````

**55g. REPLACE - FIND (lines 8-13, 6 lines):**

````csharp
using Nop.Plugin.CW.Core.Constants;
using Nop.Plugin.CW.Core.Domain.Messages;
using Nop.Plugin.CW.Core.Domain.Settings;
using Nop.Plugin.CW.Core.Services.DataBase;
using Nop.Plugin.CW.Core.Services.Subsidies;
using Nop.Services.Cms;
````

**REPLACE WITH (7 lines; the file grows by 1):**

````csharp
using Nop.Plugin.CW.Core.Constants;
using Nop.Plugin.CW.Core.Domain.Messages;
using Nop.Plugin.CW.Core.Domain.Settings;
using Nop.Plugin.CW.Core.Services.Approvals;
using Nop.Plugin.CW.Core.Services.DataBase;
using Nop.Plugin.CW.Core.Services.Subsidies;
using Nop.Services.Cms;
````

### 56. `Plugins/Nop.Plugin.CW.Core/Data/Mapping/CWNameCompatibility.cs`

2 blocks, listed bottom to top: apply them in this order and each FIND is at the lines it states.

**56a. REPLACE - FIND (lines 99-104, 6 lines):**

````csharp
        { typeof(SubsidyInOrderingPeriod), "vw_CORE_SUB_SubsidyInOrderingPeriod" },
        { typeof(SubsidyVariationInOrderingPeriod), "vw_CORE_SUB_SubsidyVariationInOrderingPeriod" },
        { typeof(StoreSubsidyVariationSetting), "vw_CORE_SUB_StoreSubsidyVariationSetting" },
    };

    public Dictionary<(Type, string), string> ColumnName => new()
````

**REPLACE WITH (13 lines; the file grows by 7):**

````csharp
        { typeof(SubsidyInOrderingPeriod), "vw_CORE_SUB_SubsidyInOrderingPeriod" },
        { typeof(SubsidyVariationInOrderingPeriod), "vw_CORE_SUB_SubsidyVariationInOrderingPeriod" },
        { typeof(StoreSubsidyVariationSetting), "vw_CORE_SUB_StoreSubsidyVariationSetting" },

        //Order approvals
        { typeof(Approver), "CORE_APR_Approver" },
        { typeof(ApprovalRule), "CORE_APR_ApprovalRule" },
        { typeof(ApprovalRuleApprover), "CORE_APR_ApprovalRuleApprover" },
        { typeof(OrderApproval), "CORE_APR_OrderApproval" },
        { typeof(OrderApprovalRequest), "CORE_APR_OrderApprovalRequest" },
    };

    public Dictionary<(Type, string), string> ColumnName => new()
````

**56b. REPLACE - FIND (lines 1-4, 4 lines):**

````csharp
using Nop.Data.Mapping;
using Nop.Plugin.CW.Core.Domain.Branches;
using Nop.Plugin.CW.Core.Domain.CreditNotes;
using Nop.Plugin.CW.Core.Domain.CreditNotes.Views;
````

**REPLACE WITH (5 lines; the file grows by 1):**

````csharp
using Nop.Data.Mapping;
using Nop.Plugin.CW.Core.Domain.Approvals;
using Nop.Plugin.CW.Core.Domain.Branches;
using Nop.Plugin.CW.Core.Domain.CreditNotes;
using Nop.Plugin.CW.Core.Domain.CreditNotes.Views;
````

### 57. `Plugins/Nop.Plugin.CW.Core/Data/Migrations/SchemaMigration.cs`

2 blocks, listed bottom to top: apply them in this order and each FIND is at the lines it states.

**57a. REPLACE - FIND (lines 75-81, 7 lines):**

````csharp
        typeof(SubsidyTopup),
        typeof(SubsidyTopupHistory),
        typeof(SubsidyVariation),
        typeof(SubsidyVariationHistory)
    ];

    /// <summary>
````

**REPLACE WITH (12 lines; the file grows by 5):**

````csharp
        typeof(SubsidyTopup),
        typeof(SubsidyTopupHistory),
        typeof(SubsidyVariation),
        typeof(SubsidyVariationHistory),
        typeof(Approver),
        typeof(ApprovalRule),
        typeof(ApprovalRuleApprover),
        typeof(OrderApproval),
        typeof(OrderApprovalRequest)
    ];

    /// <summary>
````

**57b. REPLACE - FIND (lines 2-7, 6 lines):**

````csharp
using Nop.Data.Extensions;
using Nop.Data.Mapping;
using Nop.Data.Migrations;
using Nop.Plugin.CW.Core.Domain.Branches;
using Nop.Plugin.CW.Core.Domain.CreditNotes;
using Nop.Plugin.CW.Core.Domain.Customers;
````

**REPLACE WITH (7 lines; the file grows by 1):**

````csharp
using Nop.Data.Extensions;
using Nop.Data.Mapping;
using Nop.Data.Migrations;
using Nop.Plugin.CW.Core.Domain.Approvals;
using Nop.Plugin.CW.Core.Domain.Branches;
using Nop.Plugin.CW.Core.Domain.CreditNotes;
using Nop.Plugin.CW.Core.Domain.Customers;
````

### 58. `Plugins/Nop.Plugin.CW.Core/Data/MigrationVersions.cs`

1 block.

**58a. REPLACE - FIND (lines 19-22, 4 lines):**

````csharp
    /// CORE_SUB_SubsidyReminder, on a database installed before the table was in the schema
    /// </summary>
    public const string SubsidyReminder = "2026/11/09 12:00:00";
}
````

**REPLACE WITH (9 lines; the file grows by 5):**

````csharp
    /// CORE_SUB_SubsidyReminder, on a database installed before the table was in the schema
    /// </summary>
    public const string SubsidyReminder = "2026/11/09 12:00:00";

    /// <summary>
    /// The order approval tables (CORE_APR_*), on a database installed before they were in the schema
    /// </summary>
    public const string OrderApproval = "2026/11/17 12:00:00";
}
````

### 59. `Plugins/Nop.Plugin.CW.Core/Domain/Settings/CWFeature.cs`

1 block.

**59a. REPLACE - FIND (lines 18-22, 5 lines):**

````csharp
    CreditNotes = 4,

    /// <summary>Bulk ordering (MultiStoreSettings.BulkOrderEnable)</summary>
    BulkOrder = 5
}
````

**REPLACE WITH (8 lines; the file grows by 3):**

````csharp
    CreditNotes = 4,

    /// <summary>Bulk ordering (MultiStoreSettings.BulkOrderEnable)</summary>
    BulkOrder = 5,

    /// <summary>Order approvals (MultiStoreSettings.ApprovalsEnable)</summary>
    Approvals = 6
}
````

### 60. `Plugins/Nop.Plugin.CW.Core/Domain/Settings/CWFeatureSet.cs`

3 blocks, listed bottom to top: apply them in this order and each FIND is at the lines it states.

**60a. REPLACE - FIND (lines 28-33, 6 lines):**

````csharp
            Subsidy: settings.SubsidyEnable && settings.PackNameEnable,
            Exchanges: settings.ExchangesEnable,
            CreditNotes: settings.CreditNotesEnable,
            BulkOrder: settings.BulkOrderEnable);
    }
}
````

**REPLACE WITH (7 lines; the file grows by 1):**

````csharp
            Subsidy: settings.SubsidyEnable && settings.PackNameEnable,
            Exchanges: settings.ExchangesEnable,
            CreditNotes: settings.CreditNotesEnable,
            BulkOrder: settings.BulkOrderEnable,
            Approvals: settings.ApprovalsEnable);
    }
}
````

**60b. REPLACE - FIND (lines 13-18, 6 lines):**

````csharp
        CWFeature.Exchanges => Exchanges,
        CWFeature.CreditNotes => CreditNotes,
        CWFeature.BulkOrder => BulkOrder,
        _ => false
    };

````

**REPLACE WITH (7 lines; the file grows by 1):**

````csharp
        CWFeature.Exchanges => Exchanges,
        CWFeature.CreditNotes => CreditNotes,
        CWFeature.BulkOrder => BulkOrder,
        CWFeature.Approvals => Approvals,
        _ => false
    };

````

**60c. REPLACE - FIND (lines 3-9, 7 lines):**

````csharp
/// <summary>
/// Represents the enabled state of every CW feature for a single store
/// </summary>
public readonly record struct CWFeatureSet(bool PackName, bool Subsidy, bool Exchanges, bool CreditNotes, bool BulkOrder)
{
    /// <summary>Gets a value indicating whether the specified feature is enabled</summary>
    public bool this[CWFeature feature] => feature switch
````

**REPLACE WITH (7 lines; the file grows by 0):**

````csharp
/// <summary>
/// Represents the enabled state of every CW feature for a single store
/// </summary>
public readonly record struct CWFeatureSet(bool PackName, bool Subsidy, bool Exchanges, bool CreditNotes, bool BulkOrder, bool Approvals)
{
    /// <summary>Gets a value indicating whether the specified feature is enabled</summary>
    public bool this[CWFeature feature] => feature switch
````

### 61. `Plugins/Nop.Plugin.CW.Core/Domain/Settings/MultiStoreSettings.cs`

1 block.

**61a. REPLACE - FIND (lines 147-150, 4 lines):**

````csharp
    public List<string> OwnPlacementPaymentMethods { get; set; } = ["Payments.PayPalCommerce"];

    #endregion
}
````

**REPLACE WITH (30 lines; the file grows by 26):**

````csharp
    public List<string> OwnPlacementPaymentMethods { get; set; } = ["Payments.PayPalCommerce"];

    #endregion

    #region Order approvals

    /// <summary>Master switch. Backs CWFeature.Approvals.</summary>
    public bool ApprovalsEnable { get; set; }

    /// <summary>
    /// System names of the payment methods an order waiting for approval may be paid with: methods that do not take
    /// the money at checkout. When an order needs approval the checkout offers only these, and placement refuses any
    /// other.
    /// </summary>
    public List<string> ApprovalPaymentMethods { get; set; } = ["Payments.CheckMoneyOrder"];

    /// <summary>Days after the request before the approvers are reminded; 0 sends no first reminder.</summary>
    public int ApprovalFirstReminderDays { get; set; } = 3;

    /// <summary>Days after the request before the approvers are reminded again; 0 sends no second reminder.</summary>
    public int ApprovalSecondReminderDays { get; set; } = 7;

    /// <summary>
    /// Days after the request before an unanswered order is cancelled and its lines go back to the cart; 0 never
    /// expires an order.
    /// </summary>
    public int ApprovalExpiryDays { get; set; } = 14;

    #endregion
}
````

### 62. `Plugins/Nop.Plugin.CW.Core/Infrastructure/NopStartup.cs`

5 blocks, listed bottom to top: apply them in this order and each FIND is at the lines it states.

**62a. REPLACE - FIND (lines 216-221, 6 lines):**

````csharp
            options.Filters.Add<ReturnRequestSubmitFilter>(1);
            options.Filters.Add<ReturnReasonActionDeleteGuardFilter>(1);
            options.Filters.Add<SubsidyRemindersSaveFilter>(1);
        });
    }

````

**REPLACE WITH (7 lines; the file grows by 1):**

````csharp
            options.Filters.Add<ReturnRequestSubmitFilter>(1);
            options.Filters.Add<ReturnReasonActionDeleteGuardFilter>(1);
            options.Filters.Add<SubsidyRemindersSaveFilter>(1);
            options.Filters.Add<OrderApprovalAdminGuardFilter>(1);
        });
    }

````

**62b. REPLACE - FIND (lines 188-193, 6 lines):**

````csharp
        services.AddScoped<Web.Factories.IReturnRequestModelFactory, Factories.ReturnRequestOverrideModelFactory>();
        services.AddScoped<Factories.IReturnRequestOverrideModelFactory, Factories.ReturnRequestOverrideModelFactory>();
        services.AddScoped<Factories.IBulkOrderModelFactory, Factories.BulkOrderModelFactory>();

        // - Business Logic Layer classes (global - not specific to any plugin)
        services.AddScoped<ICustomersLogic, CustomersLogic>();
````

**REPLACE WITH (9 lines; the file grows by 3):**

````csharp
        services.AddScoped<Web.Factories.IReturnRequestModelFactory, Factories.ReturnRequestOverrideModelFactory>();
        services.AddScoped<Factories.IReturnRequestOverrideModelFactory, Factories.ReturnRequestOverrideModelFactory>();
        services.AddScoped<Factories.IBulkOrderModelFactory, Factories.BulkOrderModelFactory>();
        services.AddScoped<Factories.IOrderApprovalModelFactory, Factories.OrderApprovalModelFactory>();
        //an order that needs approval is offered only the payment methods that can wait for it
        services.AddScoped<Web.Factories.ICheckoutModelFactory, Factories.CheckoutOverrideModelFactory>();

        // - Business Logic Layer classes (global - not specific to any plugin)
        services.AddScoped<ICustomersLogic, CustomersLogic>();
````

**62c. REPLACE - FIND (lines 173-178, 6 lines):**

````csharp

        services.AddScoped<IPackNameOrderModelFactory, PackNameOrderModelFactory>();
        services.AddScoped<IReturnRequestExchangeModelFactory, ReturnRequestExchangeModelFactory>();

        //public store factories
        services.AddScoped<Factories.IPackNameModelFactory, Factories.PackNameModelFactory>();
````

**REPLACE WITH (7 lines; the file grows by 1):**

````csharp

        services.AddScoped<IPackNameOrderModelFactory, PackNameOrderModelFactory>();
        services.AddScoped<IReturnRequestExchangeModelFactory, ReturnRequestExchangeModelFactory>();
        services.AddScoped<IOrderApprovalAdminModelFactory, OrderApprovalAdminModelFactory>();

        //public store factories
        services.AddScoped<Factories.IPackNameModelFactory, Factories.PackNameModelFactory>();
````

**62d. REPLACE - FIND (lines 150-155, 6 lines):**

````csharp

        services.AddScoped<ICustomerRegistrationService, CustomerRegistrationOverrideService>();

        //scoped == per request, which is the whole point: the consumer resolves it, the head decides on the
        //stylesheet from it, and the widget zone renders from it, all in one request and without rebuilding
        services.AddScoped<BulkOrderProductPageContext>();
````

**REPLACE WITH (14 lines; the file grows by 8):**

````csharp

        services.AddScoped<ICustomerRegistrationService, CustomerRegistrationOverrideService>();

        //order approvals: the rules ask nothing of placement, so placement can ask them before taking a payment;
        //the placement context carries placement's decision to the OrderPlacedEvent that holds the order
        services.AddScoped<IOrderApprovalRuleService, OrderApprovalRuleService>();
        services.AddScoped<IOrderApprovalService, OrderApprovalService>();
        services.AddScoped<IOrderApprovalWorkflowMessageService, OrderApprovalWorkflowMessageService>();
        services.AddScoped<IOrderApprovalInstallService, OrderApprovalInstallService>();
        services.AddScoped<OrderApprovalPlacementContext>();

        //scoped == per request, which is the whole point: the consumer resolves it, the head decides on the
        //stylesheet from it, and the widget zone renders from it, all in one request and without rebuilding
        services.AddScoped<BulkOrderProductPageContext>();
````

**62e. REPLACE - FIND (lines 8-13, 6 lines):**

````csharp
using Nop.Plugin.CW.Core.Background;
using Nop.Plugin.CW.Core.BusinessLogic;
using Nop.Plugin.CW.Core.Filters;
using Nop.Plugin.CW.Core.Services.Branches;
using Nop.Plugin.CW.Core.Services.BulkOrder;
using Nop.Plugin.CW.Core.Services.Catalog;
````

**REPLACE WITH (7 lines; the file grows by 1):**

````csharp
using Nop.Plugin.CW.Core.Background;
using Nop.Plugin.CW.Core.BusinessLogic;
using Nop.Plugin.CW.Core.Filters;
using Nop.Plugin.CW.Core.Services.Approvals;
using Nop.Plugin.CW.Core.Services.Branches;
using Nop.Plugin.CW.Core.Services.BulkOrder;
using Nop.Plugin.CW.Core.Services.Catalog;
````

### 63. `Plugins/Nop.Plugin.CW.Core/Nop.Plugin.CW.Core.csproj`

4 blocks, listed bottom to top: apply them in this order and each FIND is at the lines it states.

**63a. REPLACE - FIND (lines 686-691, 6 lines):**

````xml
    <None Update="Views\SubsidyCatalog\ChangeTopupProductGroup.cshtml">
      <CopyToOutputDirectory>Always</CopyToOutputDirectory>
    </None>
    <None Update="Views\SubsidyCatalog\ChangeTopupSubsidy.cshtml">
      <CopyToOutputDirectory>Always</CopyToOutputDirectory>
    </None>
````

**REPLACE WITH (9 lines; the file grows by 3):**

````xml
    <None Update="Views\SubsidyCatalog\ChangeTopupProductGroup.cshtml">
      <CopyToOutputDirectory>Always</CopyToOutputDirectory>
    </None>
    <None Update="Views\OrderApproval\Review.cshtml">
      <CopyToOutputDirectory>Always</CopyToOutputDirectory>
    </None>
    <None Update="Views\SubsidyCatalog\ChangeTopupSubsidy.cshtml">
      <CopyToOutputDirectory>Always</CopyToOutputDirectory>
    </None>
````

**63b. REPLACE - FIND (lines 320-325, 6 lines):**

````xml
    <None Update="Areas\Admin\Views\Shared\Components\FeeConcept\ReShippingFee.cshtml">
      <CopyToOutputDirectory>Always</CopyToOutputDirectory>
    </None>
    <None Update="Areas\Admin\Views\Shared\Components\OrderListBadges\Default.cshtml">
      <CopyToOutputDirectory>Always</CopyToOutputDirectory>
    </None>
````

**REPLACE WITH (9 lines; the file grows by 3):**

````xml
    <None Update="Areas\Admin\Views\Shared\Components\FeeConcept\ReShippingFee.cshtml">
      <CopyToOutputDirectory>Always</CopyToOutputDirectory>
    </None>
    <None Update="Areas\Admin\Views\Shared\Components\OrderApprovalAdmin\Default.cshtml">
      <CopyToOutputDirectory>Always</CopyToOutputDirectory>
    </None>
    <None Update="Areas\Admin\Views\Shared\Components\OrderListBadges\Default.cshtml">
      <CopyToOutputDirectory>Always</CopyToOutputDirectory>
    </None>
````

**63c. REPLACE - FIND (lines 31-36, 6 lines):**

````xml
    <None Remove="plugin.json" />
  </ItemGroup>
  <ItemGroup>
    <Content Include="Content\Admin\MultiStore\admin.packname.bulk.js">
      <CopyToOutputDirectory>Always</CopyToOutputDirectory>
    </Content>
````

**REPLACE WITH (9 lines; the file grows by 3):**

````xml
    <None Remove="plugin.json" />
  </ItemGroup>
  <ItemGroup>
    <Content Include="Content\Admin\Approvals\admin.order.approval.js">
      <CopyToOutputDirectory>Always</CopyToOutputDirectory>
    </Content>
    <Content Include="Content\Admin\MultiStore\admin.packname.bulk.js">
      <CopyToOutputDirectory>Always</CopyToOutputDirectory>
    </Content>
````

**63d. REPLACE - FIND (lines 21-26, 6 lines):**

````xml
    <InternalsVisibleTo Include="CW.Tests" />
  </ItemGroup>
  <ItemGroup>
    <None Remove="Content\Admin\MultiStore\admin.packname.bulk.js" />
    <None Remove="Content\Admin\MultiStore\admin.store.navigation.js" />
    <None Remove="Content\Admin\Orders\admin.order.badges.js" />
````

**REPLACE WITH (7 lines; the file grows by 1):**

````xml
    <InternalsVisibleTo Include="CW.Tests" />
  </ItemGroup>
  <ItemGroup>
    <None Remove="Content\Admin\Approvals\admin.order.approval.js" />
    <None Remove="Content\Admin\MultiStore\admin.packname.bulk.js" />
    <None Remove="Content\Admin\MultiStore\admin.store.navigation.js" />
    <None Remove="Content\Admin\Orders\admin.order.badges.js" />
````

### 64. `Plugins/Nop.Plugin.CW.Core/plugin.json`

1 block.

**64a. REPLACE - FIND (lines 2-8, 7 lines):**

````json
  "Group": "CW",
  "FriendlyName": "CW Core Plugin",
  "SystemName": "CW.Core",
  "Version": "2026_11_17_DATA_0.1",
  "SupportedVersions": [ "5.00" ],
  "Author": "CW Team",
  "DisplayOrder": 1,
````

**REPLACE WITH (7 lines; the file grows by 0):**

````json
  "Group": "CW",
  "FriendlyName": "CW Core Plugin",
  "SystemName": "CW.Core",
  "Version": "2026_11_17_DATA_0.2",
  "SupportedVersions": [ "5.00" ],
  "Author": "CW Team",
  "DisplayOrder": 1,
````

### 65. `Plugins/Nop.Plugin.CW.Core/Services/Messages/CWTokenGroupNames.cs`

1 block.

**65a. REPLACE - FIND (lines 35-38, 4 lines):**

````csharp
    /// given back
    /// </summary>
    public const string ReturnCreditTokens = "Return credit tokens";
}
````

**REPLACE WITH (9 lines; the file grows by 5):**

````csharp
    /// given back
    /// </summary>
    public const string ReturnCreditTokens = "Return credit tokens";

    /// <summary>
    /// Represents the tokens of the order approval e-mails: the approver, the link, the amount, the decision
    /// </summary>
    public const string OrderApprovalTokens = "Order approval tokens";
}
````

### 66. `Plugins/Nop.Plugin.CW.Core/Services/Messages/MessageTokenProviderOverride.cs`

1 block.

**66a. REPLACE - FIND (lines 386-391, 6 lines):**

````csharp
                    TokenGroupNames.CustomerTokens,
                    CWTokenGroupNames.SubsidyExpiryTokens
                };
            default:
                return base.GetTokenGroups(messageTemplate);
        }
````

**REPLACE WITH (19 lines; the file grows by 13):**

````csharp
                    TokenGroupNames.CustomerTokens,
                    CWTokenGroupNames.SubsidyExpiryTokens
                };
            case CWMessageTemplateSystemNames.OrderApprovalApproverRequest:
            case CWMessageTemplateSystemNames.OrderApprovalApproverReminder:
            case CWMessageTemplateSystemNames.OrderApprovalAwaitingCustomer:
            case CWMessageTemplateSystemNames.OrderApprovalApprovedCustomer:
            case CWMessageTemplateSystemNames.OrderApprovalDeclinedCustomer:
            case CWMessageTemplateSystemNames.OrderApprovalExpiredCustomer:
                return new[]
                {
                    TokenGroupNames.StoreTokens,
                    TokenGroupNames.OrderTokens,
                    TokenGroupNames.CustomerTokens,
                    CWTokenGroupNames.OrderApprovalTokens
                };
            default:
                return base.GetTokenGroups(messageTemplate);
        }
````

### 67. `Plugins/Nop.Plugin.CW.Core/Services/Orders/IOrderProcessingOverrideService.cs`

1 block.

**67a. REPLACE - FIND (lines 44-47, 4 lines):**

````csharp
    /// <param name="packNameCart">The packname cart being paid</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    Task PrepareCustomerPaymentAddressesAsync(Customer customer, int storeId, IList<PackNameShoppingCartItem> packNameCart);
}
````

**REPLACE WITH (12 lines; the file grows by 8):**

````csharp
    /// <param name="packNameCart">The packname cart being paid</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    Task PrepareCustomerPaymentAddressesAsync(Customer customer, int storeId, IList<PackNameShoppingCartItem> packNameCart);

    /// <summary>
    /// Releases an order its approver accepted: it moves from Pending to Processing through nop's own status change,
    /// without nop's "order processing" e-mail, and is then checked as any order is
    /// </summary>
    /// <param name="order">The order</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    Task ReleaseApprovedOrderAsync(Order order);
}
````

### 68. `Plugins/Nop.Plugin.CW.Core/Services/Orders/OrderProcessingOverrideService.cs`

10 blocks, listed bottom to top: apply them in this order and each FIND is at the lines it states.

**68a. REPLACE - FIND (lines 2152-2157, 6 lines):**

````csharp

    #endregion

    #endregion

    #region Nested classes
````

**REPLACE WITH (27 lines; the file grows by 21):**

````csharp

    #endregion

    #region Order approvals

    /// <summary>
    /// Releases an order its approver accepted: a held order is Pending, and moves to Processing through nop's own
    /// status change, then nop checks it as it checks any order (paid and shipped orders move on)
    /// </summary>
    /// <param name="order">The order</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public virtual async Task ReleaseApprovedOrderAsync(Order order)
    {
        ArgumentNullException.ThrowIfNull(order);

        //the customer is told by the approval e-mail, not by nop's "order processing" one
        if (order.OrderStatus == OrderStatus.Pending)
            await SetOrderStatusAsync(order, OrderStatus.Processing, false);

        await CheckOrderStatusAsync(order);
    }

    #endregion

    #endregion

    #region Nested classes
````

**68b. REPLACE - FIND (lines 1982-1987, 6 lines):**

````csharp
        //prepare order details
        var details = await PreparePlacePackNameOrderDetailsAsync(processPaymentRequest);

        async Task<PlaceOrderResult> placeOrder(PlacePackNameOrderContainer placeOrderContainer)
        {
            var result = new PlaceOrderResult();
````

**REPLACE WITH (12 lines; the file grows by 6):**

````csharp
        //prepare order details
        var details = await PreparePlacePackNameOrderDetailsAsync(processPaymentRequest);

        var approvalError = await PrepareOrderApprovalAsync(details.Customer, processPaymentRequest.StoreId, details.Cart,
            details.OrderTotal > decimal.Zero, processPaymentRequest.PaymentMethodSystemName, notifyAfterSettlement: false);

        if (approvalError != null)
            return new PlaceOrderResult { Errors = [approvalError] };

        async Task<PlaceOrderResult> placeOrder(PlacePackNameOrderContainer placeOrderContainer)
        {
            var result = new PlaceOrderResult();
````

**68c. REPLACE - FIND (lines 1680-1685, 6 lines):**

````csharp
    {
        ArgumentNullException.ThrowIfNull(order);

        var customer = await _customerService.GetCustomerByIdAsync(order.CustomerId);

        //With packnames switched off for the order's store, a re-order is an ordinary nop re-order: the old
````

**REPLACE WITH (11 lines; the file grows by 5):**

````csharp
    {
        ArgumentNullException.ThrowIfNull(order);

        //An order waiting for approval is not re-ordered: if it is then declined, its lines go back to the cart a second
        //time.
        if (await IsOrderHeldForApprovalAsync(order.Id))
            return [await _localizationService.GetResourceAsync("Plugins.CW.Core.OrderApproval.ReOrderHeld")];

        var customer = await _customerService.GetCustomerByIdAsync(order.CustomerId);

        //With packnames switched off for the order's store, a re-order is an ordinary nop re-order: the old
````

**68d. REPLACE - FIND (lines 1631-1636, 6 lines):**

````csharp
        if (settleableTotal > decimal.Zero)
            await SettleOrderAsync(result.PlacedOrder, settleableTotal, packNameCart: [], orderItemIdByCartItemId: new Dictionary<int, int>());

        return result;
    }

````

**REPLACE WITH (9 lines; the file grows by 3):**

````csharp
        if (settleableTotal > decimal.Zero)
            await SettleOrderAsync(result.PlacedOrder, settleableTotal, packNameCart: [], orderItemIdByCartItemId: new Dictionary<int, int>());

        //what waits for the settled order: the approval e-mails of a held order
        await _eventPublisher.PublishAsync(new PlainOrderSettledEvent(result.PlacedOrder));

        return result;
    }

````

**68e. REPLACE - FIND (lines 1614-1619, 6 lines):**

````csharp

            var cart = await _shoppingCartService.GetShoppingCartAsync(customer, ShoppingCartType.ShoppingCart, processPaymentRequest.StoreId);
            settleableTotal = (await _orderTotalCalculationService.QuoteCartSettlementAsync(cart)).SettleableAmount;
        }

        var result = await base.PlaceOrderAsync(processPaymentRequest);
````

**REPLACE WITH (12 lines; the file grows by 6):**

````csharp

            var cart = await _shoppingCartService.GetShoppingCartAsync(customer, ShoppingCartType.ShoppingCart, processPaymentRequest.StoreId);
            settleableTotal = (await _orderTotalCalculationService.QuoteCartSettlementAsync(cart)).SettleableAmount;

            var approvalError = await PrepareOrderApprovalAsync(customer, processPaymentRequest.StoreId, cart, null,
                processPaymentRequest.PaymentMethodSystemName, notifyAfterSettlement: true);

            if (approvalError != null)
                return new PlaceOrderResult { Errors = [approvalError] };
        }

        var result = await base.PlaceOrderAsync(processPaymentRequest);
````

**68f. REPLACE - FIND (lines 1539-1544, 6 lines):**

````csharp
        return await PlacePackNameOrderAsync(processPaymentRequest);
    }

    #endregion

    #region Overrides
````

**REPLACE WITH (50 lines; the file grows by 44):**

````csharp
        return await PlacePackNameOrderAsync(processPaymentRequest);
    }

    /// <summary>
    /// Decides from the cart whether the order will wait for an approver, before any payment is taken. A payment method
    /// that cannot wait (MultiStoreSettings.ApprovalPaymentMethods) is refused; otherwise the decision is kept for the
    /// OrderPlacedEvent that holds the order.
    /// </summary>
    /// <param name="customer">The customer placing the order</param>
    /// <param name="storeId">Store identifier</param>
    /// <param name="cart">The cart being placed</param>
    /// <param name="paymentRequired">Whether the order leaves something to pay; null to ask the cart</param>
    /// <param name="paymentMethodSystemName">The payment method chosen</param>
    /// <param name="notifyAfterSettlement">
    /// Whether the order is settled after OrderPlacedEvent (nop's own placement), so its e-mails wait for
    /// PlainOrderSettledEvent
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the error that refuses the order; null when it may be placed
    /// </returns>
    protected virtual async Task<string> PrepareOrderApprovalAsync(Customer customer, int storeId, IList<ShoppingCartItem> cart,
        bool? paymentRequired, string paymentMethodSystemName, bool notifyAfterSettlement)
    {
        var requirement = await _orderApprovalRuleService.EvaluateCartAsync(customer, storeId, cart);

        if (!requirement.Required)
            return null;

        //an order with nothing left to pay takes no payment, so it can always wait
        if ((paymentRequired ?? await IsPaymentWorkflowRequiredAsync(cart))
            && !await _orderApprovalRuleService.IsPaymentMethodAllowedAsync(storeId, paymentMethodSystemName))
            return await _localizationService.GetResourceAsync("Plugins.CW.Core.OrderApproval.PaymentMethodNotAllowed");

        _orderApprovalPlacementContext.Set(customer.Id, requirement with { NotifyAfterSettlement = notifyAfterSettlement });

        return null;
    }

    /// <summary>
    /// Gets a value indicating whether an order is waiting for its approver
    /// </summary>
    /// <param name="orderId">Order identifier</param>
    protected virtual async Task<bool> IsOrderHeldForApprovalAsync(int orderId)
        => await _orderApprovalRepository.Table
            .AnyAsync(approval => approval.OrderId == orderId && approval.StatusId == (int)OrderApprovalStatus.Pending);

    #endregion

    #region Overrides
````

**68g. REPLACE - FIND (lines 209-214, 6 lines):**

````csharp
        _settlementPipeline = settlementPipeline;
        _cwFeatureService = cwFeatureService;
        _multiStoreService = multiStoreService;
    }

    #endregion
````

**REPLACE WITH (9 lines; the file grows by 3):**

````csharp
        _settlementPipeline = settlementPipeline;
        _cwFeatureService = cwFeatureService;
        _multiStoreService = multiStoreService;
        _orderApprovalRuleService = orderApprovalRuleService;
        _orderApprovalPlacementContext = orderApprovalPlacementContext;
        _orderApprovalRepository = orderApprovalRepository;
    }

    #endregion
````

**68h. REPLACE - FIND (lines 142-148, 7 lines):**

````csharp
        ILocker locker,
        ISettlementPipeline settlementPipeline,
        ICWFeatureService cwFeatureService,
        IMultiStoreService multiStoreService) : base(currencySettings,
            addressService,
            affiliateService,
            checkoutAttributeFormatter,
````

**REPLACE WITH (10 lines; the file grows by 3):**

````csharp
        ILocker locker,
        ISettlementPipeline settlementPipeline,
        ICWFeatureService cwFeatureService,
        IMultiStoreService multiStoreService,
        IOrderApprovalRuleService orderApprovalRuleService,
        OrderApprovalPlacementContext orderApprovalPlacementContext,
        IRepository<OrderApproval> orderApprovalRepository) : base(currencySettings,
            addressService,
            affiliateService,
            checkoutAttributeFormatter,
````

**68i. REPLACE - FIND (lines 73-78, 6 lines):**

````csharp
    protected readonly ISettlementPipeline _settlementPipeline;
    protected readonly ICWFeatureService _cwFeatureService;
    protected readonly IMultiStoreService _multiStoreService;

    #endregion

````

**REPLACE WITH (9 lines; the file grows by 3):**

````csharp
    protected readonly ISettlementPipeline _settlementPipeline;
    protected readonly ICWFeatureService _cwFeatureService;
    protected readonly IMultiStoreService _multiStoreService;
    protected readonly IOrderApprovalRuleService _orderApprovalRuleService;
    protected readonly OrderApprovalPlacementContext _orderApprovalPlacementContext;
    protected readonly IRepository<OrderApproval> _orderApprovalRepository;

    #endregion

````

**68j. REPLACE - FIND (lines 11-21, 11 lines):**

````csharp
using Nop.Core.Events;
using Nop.Data;
using Nop.Plugin.CW.Core.Constants;
using Nop.Plugin.CW.Core.Domain.Exchanges;
using Nop.Plugin.CW.Core.Domain.Orders;
using Nop.Plugin.CW.Core.Domain.Orders.Views;
using Nop.Plugin.CW.Core.Domain.Settings;
using Nop.Plugin.CW.Core.Helpers;
using Nop.Plugin.CW.Core.Services.Common;
using Nop.Plugin.CW.Core.Services.CreditNotes;
using Nop.Plugin.CW.Core.Services.Customers;
````

**REPLACE WITH (13 lines; the file grows by 2):**

````csharp
using Nop.Core.Events;
using Nop.Data;
using Nop.Plugin.CW.Core.Constants;
using Nop.Plugin.CW.Core.Domain.Approvals;
using Nop.Plugin.CW.Core.Domain.Exchanges;
using Nop.Plugin.CW.Core.Domain.Orders;
using Nop.Plugin.CW.Core.Domain.Orders.Views;
using Nop.Plugin.CW.Core.Domain.Settings;
using Nop.Plugin.CW.Core.Helpers;
using Nop.Plugin.CW.Core.Services.Approvals;
using Nop.Plugin.CW.Core.Services.Common;
using Nop.Plugin.CW.Core.Services.CreditNotes;
using Nop.Plugin.CW.Core.Services.Customers;
````

### 69. `Plugins/Nop.Plugin.CW.Core/Services/Security/CWPermissionConfigManager.cs`

2 blocks, listed bottom to top: apply them in this order and each FIND is at the lines it states.

**69a. REPLACE - FIND (lines 30-34, 5 lines):**

````csharp
            new("Admin area. CW Plugin. Exchanges and Returns.", ADMIN_ACCESS_EXCHANGES , nameof(StandardPermission.Orders), NopCustomerDefaults.AdministratorsRoleName),
            new("Admin area. CW Plugin. Credit Notes.", ADMIN_ACCESS_CREDITNOTES , nameof(StandardPermission.Customers), NopCustomerDefaults.AdministratorsRoleName),
            new("Public store. CW Plugin. Access bulk ordering.", PUBLIC_ACCESS_BULKORDERING, nameof(StandardPermission.PublicStore), NopCustomerDefaults.RegisteredRoleName, NopCustomerDefaults.AdministratorsRoleName),
        };
}
````

**REPLACE WITH (6 lines; the file grows by 1):**

````csharp
            new("Admin area. CW Plugin. Exchanges and Returns.", ADMIN_ACCESS_EXCHANGES , nameof(StandardPermission.Orders), NopCustomerDefaults.AdministratorsRoleName),
            new("Admin area. CW Plugin. Credit Notes.", ADMIN_ACCESS_CREDITNOTES , nameof(StandardPermission.Customers), NopCustomerDefaults.AdministratorsRoleName),
            new("Public store. CW Plugin. Access bulk ordering.", PUBLIC_ACCESS_BULKORDERING, nameof(StandardPermission.PublicStore), NopCustomerDefaults.RegisteredRoleName, NopCustomerDefaults.AdministratorsRoleName),
            new("Admin area. CW Plugin. Approve, decline and re-send order approvals.", ADMIN_MANAGE_ORDER_APPROVALS, nameof(StandardPermission.Orders), NopCustomerDefaults.AdministratorsRoleName),
        };
}
````

**69b. REPLACE - FIND (lines 14-19, 6 lines):**

````csharp
    public const string ADMIN_ACCESS_CREDITNOTES = "CW.CORE.Admin.AccessCreditNotes";
    public const string ADMIN_ACCESS_EXCHANGES = "CW.CORE.Admin.AccessExchanges";
    public const string PUBLIC_ACCESS_BULKORDERING = "CW.CORE.PublicStore.AccessBulkOrdering";

    /// <summary>
    /// Gets all permission configurations
````

**REPLACE WITH (7 lines; the file grows by 1):**

````csharp
    public const string ADMIN_ACCESS_CREDITNOTES = "CW.CORE.Admin.AccessCreditNotes";
    public const string ADMIN_ACCESS_EXCHANGES = "CW.CORE.Admin.AccessExchanges";
    public const string PUBLIC_ACCESS_BULKORDERING = "CW.CORE.PublicStore.AccessBulkOrdering";
    public const string ADMIN_MANAGE_ORDER_APPROVALS = "CW.CORE.Admin.ManageOrderApprovals";

    /// <summary>
    /// Gets all permission configurations
````

### 70. `Plugins/Nop.Plugin.CW.Core/Services/Settings/CWFeatureService.cs`

3 blocks, listed bottom to top: apply them in this order and each FIND is at the lines it states.

**70a. REPLACE - FIND (lines 120-129, 10 lines):**

````csharp
                    result.Subsidy || features.Subsidy,
                    result.Exchanges || features.Exchanges,
                    result.CreditNotes || features.CreditNotes,
                    result.BulkOrder || features.BulkOrder);

                //every feature is already enabled somewhere - the remaining stores cannot change the answer
                if (result.PackName && result.Subsidy && result.Exchanges && result.CreditNotes && result.BulkOrder)
                    break;
            }

````

**REPLACE WITH (11 lines; the file grows by 1):**

````csharp
                    result.Subsidy || features.Subsidy,
                    result.Exchanges || features.Exchanges,
                    result.CreditNotes || features.CreditNotes,
                    result.BulkOrder || features.BulkOrder,
                    result.Approvals || features.Approvals);

                //every feature is already enabled somewhere - the remaining stores cannot change the answer
                if (result.PackName && result.Subsidy && result.Exchanges && result.CreditNotes && result.BulkOrder && result.Approvals)
                    break;
            }

````

**70b. REPLACE - FIND (lines 109-115, 7 lines):**

````csharp
                    SubsidyEnable = await GetFlagForStoreAsync(subsidyKey, store.Id),
                    ExchangesEnable = await GetFlagForStoreAsync(exchangesKey, store.Id),
                    CreditNotesEnable = await GetFlagForStoreAsync(creditNotesKey, store.Id),
                    BulkOrderEnable = await GetFlagForStoreAsync(bulkOrderKey, store.Id)
                };

                //feature implications live only in CWFeatureSet.From - never re-derive them here
````

**REPLACE WITH (8 lines; the file grows by 1):**

````csharp
                    SubsidyEnable = await GetFlagForStoreAsync(subsidyKey, store.Id),
                    ExchangesEnable = await GetFlagForStoreAsync(exchangesKey, store.Id),
                    CreditNotesEnable = await GetFlagForStoreAsync(creditNotesKey, store.Id),
                    BulkOrderEnable = await GetFlagForStoreAsync(bulkOrderKey, store.Id),
                    ApprovalsEnable = await GetFlagForStoreAsync(approvalsKey, store.Id)
                };

                //feature implications live only in CWFeatureSet.From - never re-derive them here
````

**70c. REPLACE - FIND (lines 98-105, 8 lines):**

````csharp
            var exchangesKey = _settingService.GetSettingKey(probe, x => x.ExchangesEnable);
            var creditNotesKey = _settingService.GetSettingKey(probe, x => x.CreditNotesEnable);
            var bulkOrderKey = _settingService.GetSettingKey(probe, x => x.BulkOrderEnable);

            var result = new CWFeatureSet(false, false, false, false, false);

            foreach (var store in await _storeService.GetAllStoresAsync())
            {
````

**REPLACE WITH (9 lines; the file grows by 1):**

````csharp
            var exchangesKey = _settingService.GetSettingKey(probe, x => x.ExchangesEnable);
            var creditNotesKey = _settingService.GetSettingKey(probe, x => x.CreditNotesEnable);
            var bulkOrderKey = _settingService.GetSettingKey(probe, x => x.BulkOrderEnable);
            var approvalsKey = _settingService.GetSettingKey(probe, x => x.ApprovalsEnable);

            var result = new CWFeatureSet(false, false, false, false, false, false);

            foreach (var store in await _storeService.GetAllStoresAsync())
            {
````

