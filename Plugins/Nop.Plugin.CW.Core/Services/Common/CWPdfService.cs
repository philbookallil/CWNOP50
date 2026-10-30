using iTextSharp.text;
using iTextSharp.text.html.simpleparser;
using iTextSharp.text.pdf;
using Nop.Core;
using Nop.Core.Domain.Common;
using Nop.Core.Domain.Localization;
using Nop.Core.Domain.Orders;
using Nop.Core.Infrastructure;
using Nop.Plugin.CW.Core.Domain.Exchanges;
using Nop.Plugin.CW.Core.Helpers;
using Nop.Plugin.CW.Core.Services.Exchanges;
using Nop.Plugin.CW.Core.Services.Messages;
using Nop.Services.Catalog;
using Nop.Services.Common;
using Nop.Services.Common.Pdf;
using Nop.Services.Configuration;
using Nop.Services.Helpers;
using Nop.Services.Localization;
using Nop.Services.Media;
using Nop.Services.Orders;
using Nop.Services.Stores;
using PdfRpt.Core.Contracts;
using System.Globalization;
using System.util;

namespace Nop.Plugin.CW.Core.Services.Common
{
    public class CWPdfService : ICWPdfService
    {
        #region Constants

        protected const int FULL_BORDER = PdfPCell.TOP_BORDER | PdfPCell.RIGHT_BORDER |
                                            PdfPCell.BOTTOM_BORDER | PdfPCell.LEFT_BORDER;

        #endregion

        #region Fields

        protected readonly IExchangeService _exchangeService;
        protected readonly ILanguageService _languageService;
        protected readonly IWorkContext _workContext;
        protected readonly INopFileProvider _fileProvider;
        protected readonly IOrderService _orderService;
        protected readonly IDateTimeHelper _dateTimeHelper;
        protected readonly IPictureService _pictureService;
        protected readonly IStoreService _storeService;
        protected readonly IStoreContext _storeContext;
        protected readonly ISettingService _settingService;
        protected readonly IProductService _productService;
        protected readonly PdfSettings _pdfSettings;
        protected readonly IExchangeMessageService _exchangeMessageService;
        protected readonly IExchangesDisplayService _exchangesDisplayService;

        #endregion

        #region Ctor

        public CWPdfService(ILanguageService languageService,
            IWorkContext workContext,
            INopFileProvider fileProvider,
            IOrderService orderService,
            IDateTimeHelper dateTimeHelper,
            IPictureService pictureService,
            IStoreService storeService,
            IStoreContext storeContext,
            ISettingService settingService,
            IProductService productService,
            IExchangeService exchangeService,
            PdfSettings pdfSettings,
            IExchangeMessageService exchangeMessageService,
            IExchangesDisplayService exchangesDisplayService)
        {
            _exchangeService = exchangeService;
            _languageService = languageService;
            _workContext = workContext;
            _fileProvider = fileProvider;
            _orderService = orderService;
            _dateTimeHelper = dateTimeHelper;
            _pictureService = pictureService;
            _storeService = storeService;
            _storeContext = storeContext;
            _settingService = settingService;
            _pdfSettings = pdfSettings;
            _productService = productService;
            _exchangeMessageService = exchangeMessageService;
            _exchangesDisplayService = exchangesDisplayService;
        }

        #endregion

        #region Utilities

        /// <summary>
        /// Get font
        /// </summary>
        /// <returns>Font</returns>
        protected virtual Font GetFont(Language lang, PdfSettings pdfSettings)
        {
            var fontName = lang?.Rtl == true
                ? !string.IsNullOrEmpty(pdfSettings.RtlFontName) ? pdfSettings.RtlFontName : NopCommonDefaults.PdfRtlFontName
                : !string.IsNullOrEmpty(pdfSettings.LtrFontName) ? pdfSettings.LtrFontName : NopCommonDefaults.PdfLtrFontName;

            var fontSize = pdfSettings.BaseFontSize >= 0 ? pdfSettings.BaseFontSize : 10;

            return PdfDocumentHelper.GetFont(fontName, fontSize);
        }

        /// <summary>
        /// Get font direction
        /// </summary>
        /// <param name="lang">Language</param>
        /// <returns>Font direction</returns>
        protected virtual int GetDirection(Language lang)
        {
            return lang.Rtl ? PdfWriter.RUN_DIRECTION_RTL : PdfWriter.RUN_DIRECTION_LTR;
        }

        /// <summary>
        /// Get element alignment
        /// </summary>
        /// <param name="lang">Language</param>
        /// <param name="isOpposite">Is opposite?</param>
        /// <returns>Element alignment</returns>
        protected virtual int GetAlignment(Language lang, bool isOpposite = false)
        {
            //if we need the element to be opposite, like logo etc`.
            if (!isOpposite)
                return lang.Rtl ? Element.ALIGN_RIGHT : Element.ALIGN_LEFT;

            return lang.Rtl ? Element.ALIGN_LEFT : Element.ALIGN_RIGHT;
        }

        /// <summary>
        /// Parses the given HTML content and wraps it with a <see cref="Paragraph"/> element
        /// </summary>
        /// <param name="htmlText">The HTML text to convert/parse into PDF content (Paragraph)</param>
        /// <returns></returns>
        protected virtual Paragraph ParseHtmlIntoParagraph(string htmlText)
        {
            var p = new Paragraph();
            // This will help in locating images uploaded with the rich text editor used on message templates
            var props = new NullValueDictionary<string, object>();
            props["img_baseurl"] = _fileProvider.MapPath("~/wwwroot");

            using (StringReader sr = new StringReader(htmlText))
            {
                var elements = HtmlWorker.ParseToList(sr, null, props);
                foreach (IElement e in elements)
                {
                    p.Add(e);
                }
            }

            return p;
        }

        /// <summary>
        /// Converts an HTML attribute description into PDF-friendly plain text
        /// </summary>
        /// <param name="attributesInfo">Attribute description; may be null when the product has no attributes</param>
        /// <returns>Plain text with line breaks, or an empty string</returns>
        protected static string FormatAttributesForPdf(string attributesInfo)
        {
            if (string.IsNullOrEmpty(attributesInfo))
                return string.Empty;

            return attributesInfo
                .Replace("<br />", Environment.NewLine)
                .Replace("<br/>", Environment.NewLine);
        }

        protected async Task PrintExchangeReturnToPdfAsync(Document doc, ReturnRequest returnRequest, OrderItem orderItem, Language lang)
        {
            var store = await _storeService.GetStoreByIdAsync(returnRequest.StoreId) ?? await _storeContext.GetCurrentStoreAsync();
            var pdfSettingsByStore = await _settingService.LoadSettingAsync<PdfSettings>(store.Id);

            #region Define Fonts

            var font = GetFont(lang, pdfSettingsByStore);

            var titleFont = PdfDocumentHelper.GetFont(font, font.Size * 2.0f, DocumentFontStyle.Bold);
            var subTitleFont = PdfDocumentHelper.GetFont(font, font.Size * 1.6f, DocumentFontStyle.Bold);
            var headingFont = PdfDocumentHelper.GetFont(font, font.Size * 1.4f, DocumentFontStyle.Bold);
            var labelFont = PdfDocumentHelper.GetFont(font, font.Size, DocumentFontStyle.Bold);

            #endregion

            var returnRequestExtraInfo = await _exchangesDisplayService.GetReturnRequestExtraInfoAsync(returnRequest.Id);

            #region Header

            //NOTE: the logo is not rendered yet (see CWPdfService_Modernisation_Spec.md, issue #2).
            //header
            //This table must stay single-column until it is - a 2-column table with one cell
            //produces an incomplete row, which iText discards, taking the whole header with it.
            var headerTable = new PdfPTable(1)
            {
                RunDirection = GetDirection(lang)
            };
            headerTable.DefaultCell.Border = Rectangle.NO_BORDER;

            //store info
            var anchor = new Anchor(store.Name, subTitleFont)
            {
                Reference = store.Url
            };

            var cellHeader = new PdfPCell(new Phrase(String.Format("Return Request #{0}", returnRequest.CustomNumber), titleFont));
            cellHeader.Phrase.Add(new Phrase(Environment.NewLine));
            cellHeader.Phrase.Add(new Phrase(Environment.NewLine));
            cellHeader.Phrase.Add(new Phrase(anchor));
            cellHeader.Phrase.Add(new Phrase(Environment.NewLine));
            cellHeader.Phrase.Add(new Phrase(Environment.NewLine));
            if (returnRequestExtraInfo != null)
            {
                cellHeader.Phrase.Add(new Phrase(returnRequestExtraInfo.CustomerName, headingFont));
                cellHeader.Phrase.Add(new Phrase(Environment.NewLine));
                cellHeader.Phrase.Add(new Phrase(Environment.NewLine));
                cellHeader.Phrase.Add(new Phrase(string.Format("Order #{0}", returnRequestExtraInfo.OrderId), headingFont));
                cellHeader.Phrase.Add(new Phrase(Environment.NewLine));
                cellHeader.Phrase.Add(new Phrase(Environment.NewLine));
            }
            cellHeader.Phrase.Add(new Phrase(String.Format("Created On: {0}", (await _dateTimeHelper.ConvertToUserTimeAsync(returnRequest.CreatedOnUtc, DateTimeKind.Utc)).ToString("D", new CultureInfo(lang.LanguageCulture))), font));
            cellHeader.Phrase.Add(new Phrase(Environment.NewLine));
            cellHeader.Phrase.Add(new Phrase(Environment.NewLine));
            cellHeader.HorizontalAlignment = Element.ALIGN_LEFT;
            cellHeader.Border = Rectangle.NO_BORDER;

            headerTable.AddCell(cellHeader);

            headerTable.WidthPercentage = 100f;
            /*
            //logo               
            if (logoExists)
            {
                var logoFilePath = _pictureService.GetThumbLocalPath(logoPicture, 0, false);
                var logo = Image.GetInstance(logoFilePath);
                logo.Alignment = GetAlignment(lang, true);
                logo.ScaleToFit(65f, 65f);

                var cellLogo = new PdfPCell();
                cellLogo.Border = Rectangle.NO_BORDER;
                cellLogo.AddElement(logo);
                headerTable.AddCell(cellLogo);
            }*/

            doc.Add(headerTable);

            #endregion

            #region PDF Body

            var bodyTable = new PdfPTable(2)
            {
                RunDirection = GetDirection(lang)
            };
            bodyTable.DefaultCell.Border = Rectangle.NO_BORDER;
            bodyTable.WidthPercentage = 100f;
            bodyTable.SetWidths(new[] { 25, 75 });

            var product = await _productService.GetProductByIdAsync(orderItem.ProductId);

            //product name
            var productNameStr = returnRequestExtraInfo != null ?
                                    string.Format("{0} - {1} x {2}", returnRequestExtraInfo.ReturnedItemSku, product.Name,
                                                                        returnRequest.Quantity)

                                    : string.Format("{0} x {1}", product.Name, returnRequest.Quantity);

            bodyTable.AddCell(new PdfPCell(new Phrase("Returned Item:", labelFont))
            {
                HorizontalAlignment = Element.ALIGN_LEFT,
                Border = Rectangle.NO_BORDER
            });
            bodyTable.AddCell(new PdfPCell(new Phrase(productNameStr, font))
            {
                HorizontalAlignment = Element.ALIGN_LEFT,
                Border = Rectangle.NO_BORDER
            });

            if (returnRequestExtraInfo != null)
            {
                var returnAttributes = FormatAttributesForPdf(returnRequestExtraInfo.ReturnedItemAttributes);

                if (!string.IsNullOrEmpty(returnAttributes))
                {
                    // Replacement item info
                    bodyTable.AddCell(new PdfPCell(new Phrase("Return Attributes:", labelFont))
                    {
                        HorizontalAlignment = Element.ALIGN_LEFT,
                        Border = Rectangle.NO_BORDER
                    });
                    bodyTable.AddCell(new PdfPCell(new Phrase(returnAttributes, font))
                    {
                        HorizontalAlignment = Element.ALIGN_LEFT,
                        Border = Rectangle.NO_BORDER
                    });
                }
            }

            //exchange data
            var exchangeItem = await _exchangeService.GetReturnRequestItemAsync(returnRequest.Id);
            if (exchangeItem != null)
            {
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

                var replacementProduct = string.Empty;
                var replacementAttributes = string.Empty;

                if (exchangeItem.ExchangeType != ExchangeType.Exact)
                {
                    var replacementItem = await _exchangeService.GetReplacementItemAsync(exchangeItem.Id);
                    if (replacementItem != null)
                    {
                        var exchangeProduct = await _productService.GetProductByIdAsync(replacementItem.ProductId);
                        // Replacement item info
                        replacementProduct = exchangeProduct != null ?
                                                string.Format("{0} - {1} x {2}", exchangeProduct.Sku, replacementItem.ProductName, replacementItem.Quantity)
                                                : string.Format("{0} x {1}", replacementItem.ProductName, replacementItem.Quantity);

                        replacementAttributes = FormatAttributesForPdf(replacementItem.AttributesInfo);
                    }
                }

                if (!string.IsNullOrEmpty(replacementProduct))
                {
                    // Replacement item info
                    bodyTable.AddCell(new PdfPCell(new Phrase("Replacement Product:", labelFont))
                    {
                        HorizontalAlignment = Element.ALIGN_LEFT,
                        Border = Rectangle.NO_BORDER
                    });
                    bodyTable.AddCell(new PdfPCell(new Phrase(replacementProduct, font))
                    {
                        HorizontalAlignment = Element.ALIGN_LEFT,
                        Border = Rectangle.NO_BORDER
                    });

                    if (!string.IsNullOrEmpty(replacementAttributes))
                    {
                        // Replacement item info
                        bodyTable.AddCell(new PdfPCell(new Phrase("Replacement Attributes:", labelFont))
                        {
                            HorizontalAlignment = Element.ALIGN_LEFT,
                            Border = Rectangle.NO_BORDER
                        });
                        bodyTable.AddCell(new PdfPCell(new Phrase(replacementAttributes, font))
                        {
                            HorizontalAlignment = Element.ALIGN_LEFT,
                            Border = Rectangle.NO_BORDER
                        });
                    }
                }
            }

            doc.Add(bodyTable);

            // Message Template
            var messageTemplate = await _exchangeMessageService.GetExchangesReturnReceiptMessageAsync(returnRequest);
            if (!String.IsNullOrEmpty(messageTemplate))
            {
                var messageTemplateTable = new PdfPTable(1);
                messageTemplateTable.DefaultCell.Border = Rectangle.NO_BORDER;
                messageTemplateTable.WidthPercentage = 100f;
                messageTemplateTable.RunDirection = GetDirection(lang);

                var messageTemplateCell = new PdfPCell
                {
                    Border = Rectangle.NO_BORDER,
                    HorizontalAlignment = Element.ALIGN_LEFT,
                    CellEvent = new DottedBorder(FULL_BORDER),
                    Padding = 10f
                };
                messageTemplateCell.AddElement(ParseHtmlIntoParagraph(messageTemplate));
                messageTemplateTable.AddCell(messageTemplateCell);

                doc.Add(new Phrase(Environment.NewLine));

                doc.Add(messageTemplateTable);
            }

            #endregion
        }

        private async Task PrintErrorToPdfAsync(Document doc, string error, Language lang)
        {
            var errorFont = GetFont(lang, _pdfSettings);
            errorFont.SetStyle(Font.BOLD);

            var bodyTable = new PdfPTable(1)
            {
                RunDirection = GetDirection(lang)
            };
            bodyTable.DefaultCell.Border = Rectangle.NO_BORDER;
            bodyTable.WidthPercentage = 100f;
            bodyTable.SetWidths(new[] { 100 });

            var cellErrorMessage = new PdfPCell(new Phrase(error, errorFont))
            {
                HorizontalAlignment = Element.ALIGN_LEFT,
                Border = Rectangle.NO_BORDER
            };
            bodyTable.AddCell(cellErrorMessage);

            doc.Add(bodyTable);
        }

        /// <summary>
        /// Abstract class to allow implementation of custom borders for use on PDF rendering
        /// </summary>
        protected abstract class CustomBorder : IPdfPCellEvent
        {
            #region Fields

            private int _border;

            #endregion

            #region Ctor

            public CustomBorder(int border)
            {
                _border = border;
            }

            #endregion

            #region Methods

            public abstract void SetLineDash(PdfContentByte canvas);

            public void CellLayout(PdfPCell cell, Rectangle position, PdfContentByte[] canvases)
            {
                PdfContentByte canvas = canvases[PdfPTable.LINECANVAS];
                canvas.SaveState();
                SetLineDash(canvas);
                if ((_border & PdfPCell.TOP_BORDER) == PdfPCell.TOP_BORDER)
                {
                    canvas.MoveTo(position.Right, position.Top);
                    canvas.LineTo(position.Left, position.Top);
                }
                if ((_border & PdfPCell.BOTTOM_BORDER) == PdfPCell.BOTTOM_BORDER)
                {
                    canvas.MoveTo(position.Right, position.Bottom);
                    canvas.LineTo(position.Left, position.Bottom);
                }
                if ((_border & PdfPCell.RIGHT_BORDER) == PdfPCell.RIGHT_BORDER)
                {
                    canvas.MoveTo(position.Right, position.Top);
                    canvas.LineTo(position.Right, position.Bottom);
                }
                if ((_border & PdfPCell.LEFT_BORDER) == PdfPCell.LEFT_BORDER)
                {
                    canvas.MoveTo(position.Left, position.Top);
                    canvas.LineTo(position.Left, position.Bottom);
                }
                canvas.Stroke();
                canvas.RestoreState();
            }

            #endregion
        }

        /// <summary>
        /// Implements a solid border
        /// </summary>
        protected sealed class SolidBorder : CustomBorder
        {
            public SolidBorder(int border) : base(border) { }

            public override void SetLineDash(PdfContentByte canvas)
            {
                // Do nothing as we need to keep the border as solid line
            }
        }

        /// <summary>
        /// Implements a dotted border
        /// </summary>
        protected sealed class DottedBorder : CustomBorder
        {
            public DottedBorder(int border) : base(border) { }

            public override void SetLineDash(PdfContentByte canvas)
            {
                canvas.SetLineCap(PdfContentByte.LINE_CAP_ROUND);
                canvas.SetLineDash(0, 4, 2);
            }
        }

        /// <summary>
        /// Implements a dotted border
        /// </summary>
        protected sealed class DashedBorder : CustomBorder
        {
            public DashedBorder(int border) : base(border) { }

            public override void SetLineDash(PdfContentByte canvas)
            {
                canvas.SetLineDash(3, 3);
            }
        }

        #endregion

        #region Methods

        public async Task PrintReturnExchangeRequestsToPdfAsync(Stream stream, IList<ReturnRequest> returnRequests, int languageId = 0)
        {
            ArgumentNullException.ThrowIfNull(stream);

            ArgumentNullException.ThrowIfNull(returnRequests);

            if (returnRequests.Count < 1)
                throw new ArgumentException("No available Return Requests to process");

            var pageSize = PageSize.A4;

            if (_pdfSettings.LetterPageSizeEnabled)
            {
                pageSize = PageSize.Letter;
            }

            var lang = languageId == 0 ? await _workContext.GetWorkingLanguageAsync() : await _languageService.GetLanguageByIdAsync(languageId);
            var returnsCount = returnRequests.Count;
            var returnsIndex = 0;
            var doc = new Document(pageSize);
            var pdfWriter = PdfWriter.GetInstance(doc, stream);

            doc.Open();

            foreach (var returnRequest in returnRequests)
            {
                var hasErrors = false;

                if (returnRequest == null)
                {
                    hasErrors = true;
                    await PrintErrorToPdfAsync(doc, string.Format("Return Request was null"), lang);
                }

                var orderItem = await _orderService.GetOrderItemByIdAsync(returnRequest.OrderItemId);
                if (orderItem == null)
                {
                    hasErrors = true;
                    await PrintErrorToPdfAsync(doc, string.Format("Order Item linked to Return Request #{0} has been deleted", returnRequest.Id), lang);
                }

                if (!hasErrors)
                {
                    await PrintExchangeReturnToPdfAsync(doc, returnRequest, orderItem, lang);
                }

                ++returnsIndex;
                if (returnsIndex < returnsCount)
                {
                    doc.NewPage(); // start a new page on the PDF document
                }
            }

            doc.Close();
        }

        public async Task PrintReturnExchangeRequestToPdfAsync(Stream stream, ReturnRequest returnRequest, int languageId = 0)
        {
            ArgumentNullException.ThrowIfNull(stream);

            ArgumentNullException.ThrowIfNull(returnRequest);

            var orderItem = await _orderService.GetOrderItemByIdAsync(returnRequest.OrderItemId) ?? throw new NopException(string.Format("Order Item linked to Return Request #{0} has been deleted", returnRequest.Id));
            var pageSize = PageSize.A4;

            if (_pdfSettings.LetterPageSizeEnabled)
            {
                pageSize = PageSize.Letter;
            }

            var lang = languageId == 0 ? await _workContext.GetWorkingLanguageAsync() : await _languageService.GetLanguageByIdAsync(languageId);
            var doc = new Document(pageSize);
            _ = PdfWriter.GetInstance(doc, stream);

            doc.Open();

            await PrintExchangeReturnToPdfAsync(doc, returnRequest, orderItem, lang);

            doc.Close();
        }

        #endregion
    }
}
