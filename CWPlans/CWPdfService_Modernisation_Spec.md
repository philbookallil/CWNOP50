# CWPdfService — migrate to the nop 4.60+ PdfDocument model

Migrated to nopCommerce 5.00 on 2026-10-31: checked against the 5.00 code; the 4.9 copy is in `NOP49/CWPlans/`.

> **Status (2026-10-31): not done — this is a live spec.** `Services/Common/CWPdfService.cs` came across
> to 5.00 unchanged (the file is identical to the 4.9 copy) and still builds the return note by hand with
> iTextSharp `Document` / `PdfPTable` / `PdfPCell`. No `ReturnRequestDocument` exists. Of §4, issue #6 and the
> §5 fix were done before the port; issue #1 is half done (the header no longer vanishes, the logo is still not
> drawn); #2, #3, #4, #5 and #9 are open. The 5.00 return note was compared with 4.9's and matched
> (`CWPlans/Deprecated/CWCore_Final500_Phase6_Plan.md` §5.3, P5), so the port changed nothing here.
> The invoice and packing slip (`PdfOverrideService`, `CWInvoiceDocument`, `CWShipmentDocument`) are already on
> nop's document model and are not part of this spec.

`CWPdfService` is the last part of `CW.Core` still written against a framework paradigm nopCommerce
abandoned. It works, but it is drifting further from core with every nop upgrade.

---

## 1. What changed in nop

Up to 4.50, nop built PDFs **imperatively**: create an iText `Document`, hand-build `PdfPTable` /
`PdfPCell`, call `doc.Add(...)` in sequence. `CWPdfService` is written in that style, inherited from the
4.1 `TIGPdfService`.

From **4.60** nop replaced it with a **declarative document model**:

- `PdfDocument<TItem>` (`Libraries/Nop.Services/Common/Pdf/PdfDocument.cs`) — abstract base supplying
  `Language`, `PageSize` (a `PdfPageSize`), `FontName` and `FontSize`, `ImageTargetSize`,
  `GetResourceAsync`, the computed `DocumentRunDirection`, and cell builders (`BuildPdfPCell`,
  `BuildHyperLinkCell`, `BuildTextCell`, `BuildAddressTable`, `LabelField`). *nop 5.00 replaced the
  4.x `Font` property with `FontName` / `FontSize`; the service gets them from
  `PdfService.GetFontInfo(pdfSettings, isRtl)`, as `PdfOverrideService` does.*
- Concrete documents are **models**: `InvoiceDocument`, `ShipmentDocument`, `CatalogDocument`. The service
  populates properties, then calls `document.Generate(stream)`.
- `PdfDocumentHelper` supplies `GetFont` (by name, or derived from a `Font`), `BuildPdfGrid`, `BuildPdfPCell`,
  `RELATIVE_LEADING`.
- Labels come from **resource keys via property annotations**, resolved by `LabelField(...)`.

See `PdfService.PrintOrderToPdfAsync` (`Libraries/Nop.Services/Common/PdfService.cs`, 5.00) for the
canonical shape: gather data → construct `InvoiceDocument { … }` → `document.Generate(pdfStream)`.

CW already extends two of these documents for the order PDFs (`Services/Common/Pdf/`): `CWInvoiceDocument`
(several delivery addresses; the settlement rows in `CreateSummary`, from `CWInvoiceTotals`) and `CWShipmentDocument`
(attribute lines under the product name, by overriding the `protected virtual` `ConfigureProductColumn`). They are the
pattern to follow here — subclass and override a hook rather than copy `Generate`.

## 2. Why this matters beyond tidiness

- **Localisation.** Every label in `CWPdfService` is a hardcoded English literal — `"Returned Item:"`,
  `"Exchange Type:"`, `"Replacement Product:"`. The nop model resolves labels from locale resources. This
  is a functional gap, not a style one, and it will bite the moment a store needs a second language.
- **Layout consistency.** `RELATIVE_LEADING`, padding and border conventions are applied centrally by the
  base class. The hand-built cells set these ad hoc or not at all — which is why the current output looks
  unlike a nop invoice.
- **Upgrade exposure.** The imperative path depends on `iTextSharp` types directly. nop's own code has
  moved behind `PdfDocument`; if nop swaps the rendering engine, the plugin breaks alone. (5.00 still
  renders through PdfRpt.Core and iTextSharp, with SkiaSharp 4 and Svg.Skia 5 for images —
  `Nop.Services.csproj` — so this has not happened yet.)
- **The font bug that started this** (six aliased `Font` references) is impossible under the base class,
  which derives variants through `PdfDocumentHelper.GetFont(Font, size, style)`.

## 3. Target shape

Two documents, mirroring how nop splits `InvoiceDocument` / `ShipmentDocument`:

```
Services/Common/Pdf/
├── ReturnRequestDocument.cs      : PdfDocument<ReturnRequestItemLine>
└── ReturnRequestItemLine.cs      — one returned line + its replacement
```

`ReturnRequestDocument` carries the properties the current method assembles inline: store name/url, logo
bytes, customer name, order number, created-on, the returned item, attributes, exchange mode and type,
replacement product and attributes, and the parsed receipt-message HTML.

`CWPdfService` reduces to: resolve store → load `PdfSettings` for **that** store → resolve language →
build the document model → `Generate(stream)`.

## 4. Known issues to fix during the migration

These exist today and should be carried into the new implementation rather than reproduced:

| # | Issue | Detail |
|---|---|---|
| 1 | **Logo never drawn** *(was: header vanishes when a PDF logo is set)* | **Half fixed.** The header table is now always single-column (`PrintExchangeReturnToPdfAsync`, with a comment saying why), so a configured logo no longer drops the header. The logo itself is still not drawn: the logo block is commented out. Fixed when the logo is passed to the document model (#2). |
| 2 | **Dead logo code** | The commented-out logo block in `PrintExchangeReturnToPdfAsync` uses `_pictureService.GetThumbLocalPath`, which does not exist in 4.9 or 5.00. nop loads logo **bytes** (`LoadPictureBinaryAsync`, plus `ConvertSvgToPngAsync` for SVG) and passes them as `InvoiceDocument.LogoData`. |
| 3 | **`GetAlignment` is unreachable** | Its only call site is inside the commented-out logo block. |
| 4 | **Page size ignores the document's store** | `PrintReturnExchangeRequestsToPdfAsync` and `PrintReturnExchangeRequestToPdfAsync` read `_pdfSettings.LetterPageSizeEnabled` from the *current* store, while the font comes from the return request's store. `PrintErrorToPdfAsync` also takes its font from `_pdfSettings`. Same per-store defect the font fix addressed. |
| 5 | **Hardcoded English labels** | All of them (`"Return Request #{0}"`, `"Returned Item:"`, `"Return Attributes:"`, `"Return Method:"`, `"Exchange Type:"`, `"Replacement Product:"`, `"Replacement Attributes:"`, the error texts). Move to locale resources under `Plugins.CW.Core.Pdf.*` — the prefix the invoice rows already use (`CWCorePlugin.cs`, e.g. `Plugins.CW.Core.Pdf.GrossTotal`). |
| ~~6~~ | ~~**`LoadSetting` called synchronously**~~ | **Resolved** — `PrintExchangeReturnToPdfAsync` calls `await _settingService.LoadSettingAsync<PdfSettings>(store.Id)`. |
| ~~7~~ | ~~**`"Exact Exchange"` hardcoded**~~ | **Resolved** — the `else` branch went away with the §5 fix. |
| ~~8~~ | ~~**Label/value mismatch**~~ | **Resolved** — §5 applied. |
| 9 | **Replacement block not gated on mode** | The replacement block in `PrintExchangeReturnToPdfAsync` tests only `ExchangeType != Exact`, so a Credit Note return still issues a `GetReplacementItemAsync` query that can only return null. The confirmation view gates the whole exchange section on `ExchangeMode != CreditNote`; the PDF should match. Harmless, but a wasted query and an inconsistency. |

## 5. Exchange mode vs exchange type

These are two different fields and the PDF conflates them:

| | Enum | Values | Question answered |
|---|---|---|---|
| **Mode** | `ExchangeReturnMode` | Exchange, Credit Note | What does the customer get back? |
| **Type** | `ExchangeType` | Same Style, Different Style, Exact Exchange | If exchanging, for what? |

**Applied before the port** — `PrintExchangeReturnToPdfAsync` now writes both rows, as below. What it fixed:
the PDF labelled a row `"Exchange Type:"` but filled it with
`DetermineExchangeModeName(exchangeItem.ExchangeMode)` — so it printed "Exchange" where the confirmation
screen, reading `DetermineExchangeTypeName(item.ExchangeType)` under the same label, printed "Same Style".
Two documents for the same return disagreed.

4.1 had the identical bug, so this is inherited, not introduced by the port.

**Fix — show both rows:**

```csharp
                // Return method — Exchange or Credit Note
                bodyTable.AddCell(new PdfPCell(new Phrase("Return Method:", labelFont))
                {
                    HorizontalAlignment = Element.ALIGN_LEFT,
                    Border = Rectangle.NO_BORDER
                });
                bodyTable.AddCell(new PdfPCell(new Phrase(ExchangesHelper.DetermineExchangeModeName(exchangeItem.ExchangeMode), font))
                {
                    HorizontalAlignment = Element.ALIGN_LEFT,
                    Border = Rectangle.NO_BORDER
                });

                // Exchange type — only meaningful when this is an exchange
                if (exchangeItem.ExchangeMode != ExchangeReturnMode.CreditNote)
                {
                    bodyTable.AddCell(new PdfPCell(new Phrase("Exchange Type:", labelFont))
                    {
                        HorizontalAlignment = Element.ALIGN_LEFT,
                        Border = Rectangle.NO_BORDER
                    });
                    bodyTable.AddCell(new PdfPCell(new Phrase(ExchangesHelper.DetermineExchangeTypeName(exchangeItem.ExchangeType), font))
                    {
                        HorizontalAlignment = Element.ALIGN_LEFT,
                        Border = Rectangle.NO_BORDER
                    });
                }
```

With that, `"Exact Exchange"` no longer needs to be forced into the Replacement Product row — the type row
already says it, so the `else` branch went and the replacement row is omitted for
`ExchangeType.Exact`, matching the confirmation screen.

When the document model is built, carry both rows into it.

## 6. Sequencing

Not part of the exchanges redesign critical path. Do it **after** the wizard repair and the
replacement-item persistence fix land and the output is verified correct — migrating a renderer whose
inputs are still wrong makes it impossible to tell which layer broke.

Suggested order: §5 label fix (done) → finish exchanges repair → migrate to `PdfDocument` → move labels to
locale resources. The current return note's output was confirmed against 4.9's in the 5.00 comparison
(phase 6, P5), which gives the migration a known-good baseline to compare with.

## 7. Verification

1. PDF renders with a store logo configured, and the logo is drawn (issue #1 — today the header shows
   without it).
2. Header, body and receipt message all present, matching 4.1's proportions.
3. `BaseFontSize` changed in store settings scales the whole document.
4. Exchange, Credit Note, and all three exchange types each render the correct rows.
5. A second language renders localised labels (only after §4 #5).
6. Multi-store: printing a return belonging to store B while browsing store A uses **B's** font, logo and
   page size.
