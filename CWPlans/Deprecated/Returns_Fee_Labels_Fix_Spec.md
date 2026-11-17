# Return page fee labels — fix spec

**Defect.** On the admin return page, the *Fees and Charges* rows show the property names *StaffShipQty*,
*StaffRepackQty*, *AdditionalFeeName* and *AdditionalFeeAmount* instead of their labels.

**Cause.** The labels moved from `[DisplayName("…")]` to `[NopResourceDisplayName("…")]` on `CWReturnRequestModel`.
In nopCommerce 5.00 `NopResourceDisplayNameAttribute` is a plain `Attribute`, not a `DisplayNameAttribute`: only the
`nop-label` and `<label asp-for>` tag helpers read it. `Html.LabelFor(m => m.X)` reads the model metadata, finds no
display name and prints the property name. The four fee rows are the only places in CW.Core that use `Html.LabelFor`.

**Fix.** Pass the resource text to `Html.LabelFor`. The resources already exist (`CWCorePlugin.cs`, keys
`Plugins.CW.Core.Admin.Returns.Fields.*`), so no plugin version change is needed. The hard-coded *Enable Additional
Fee* label is moved to its existing resource at the same time.

Three files, all under `Plugins/Nop.Plugin.CW.Core/Areas/Admin/Views/Shared/Components/FeeConcept/`.

## Change 1 — `ReShippingFee.cshtml`, line 20

Replace:

```
        Label = Html.LabelFor(m => m.StaffShipQty),
```

with:

```
        Label = Html.LabelFor(m => m.StaffShipQty, T("Plugins.CW.Core.Admin.Returns.Fields.StaffShipQty").Text),
```

## Change 2 — `RePackagingFee.cshtml`, line 18

Replace:

```
        Label = Html.LabelFor(m => m.StaffRepackQty),
```

with:

```
        Label = Html.LabelFor(m => m.StaffRepackQty, T("Plugins.CW.Core.Admin.Returns.Fields.StaffRepackQty").Text),
```

## Change 3 — `AdditionalFee.cshtml`, lines 52, 46, 34, 28 and 22

Replace **every** occurrence (twice: lines 34 and 52) of:

```
            @Html.LabelFor(m => m.AdditionalFeeAmount)
```

with:

```
            @Html.LabelFor(m => m.AdditionalFeeAmount, T("Plugins.CW.Core.Admin.Returns.Fields.AdditionalFeeAmount").Text)
```

Replace **every** occurrence (twice: lines 28 and 46) of:

```
            @Html.LabelFor(m => m.AdditionalFeeName)
```

with:

```
            @Html.LabelFor(m => m.AdditionalFeeName, T("Plugins.CW.Core.Admin.Returns.Fields.AdditionalFeeName").Text)
```

Line 22 — replace:

```
                <label class="form-check-label" for="enable-additional-fee">Enable Additional Fee</label>
```

with:

```
                <label class="form-check-label" for="enable-additional-fee">@T("Plugins.CW.Core.Admin.Returns.Fields.EnableAdditionalFee")</label>
```

## Test

Views compile at run time, so a restart is not needed; reload the page. Open *Sales › Return requests*, return
**#3003** (authorised exchange with fees). *Fees and Charges* must read *Re-Shipped Quantity*, *Repackaged Quantity*,
*Enable Additional Fee*, *Additional Fee Name* and *Fee Amount*. Then open a **Pending** return of a reason with fees
and tick *Enable Additional Fee*: the editable row shows the same two labels.
