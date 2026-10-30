using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Core;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Directory;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Stores;
using Nop.Plugin.CW.Core.Domain.BulkOrder;
using Nop.Plugin.CW.Core.Domain.Settings;
using Nop.Plugin.CW.Core.Models.BulkOrder;
using Nop.Plugin.CW.Core.Services.BulkOrder;
using Nop.Services.Catalog;
using Nop.Services.Configuration;
using Nop.Services.Directory;
using Nop.Services.Helpers;
using Nop.Services.Localization;
using Nop.Services.Media;
using Nop.Services.Security;
using Nop.Services.Seo;
using Nop.Services.Tax;
using Nop.Web.Models.Catalog;

namespace Nop.Plugin.CW.Core.Factories;

/// <summary>
/// Represents the bulk ordering model factory
/// </summary>
public class BulkOrderModelFactory : IBulkOrderModelFactory
{
    #region Fields

    protected readonly CatalogSettings _catalogSettings;
    protected readonly IBulkOrderService _bulkOrderService;
    protected readonly ICategoryService _categoryService;
    protected readonly ICurrencyService _currencyService;
    protected readonly IDateTimeHelper _dateTimeHelper;
    protected readonly ILocalizationService _localizationService;
    protected readonly IPermissionService _permissionService;
    protected readonly IPictureService _pictureService;
    protected readonly IPriceCalculationService _priceCalculationService;
    protected readonly IPriceFormatter _priceFormatter;
    protected readonly ISettingService _settingService;
    protected readonly IStoreContext _storeContext;
    protected readonly ITaxService _taxService;
    protected readonly IUrlRecordService _urlRecordService;
    protected readonly IWorkContext _workContext;
    protected readonly OrderSettings _orderSettings;
    protected readonly IBulkOrderGridBuilder _bulkOrderGridBuilder;
    protected readonly IBulkOrderPricingService _bulkOrderPricingService;
    protected readonly IProductService _productService;

    #endregion

    #region Ctor

    public BulkOrderModelFactory(CatalogSettings catalogSettings,
        IBulkOrderService bulkOrderService,
        ICategoryService categoryService,
        ICurrencyService currencyService,
        IDateTimeHelper dateTimeHelper,
        ILocalizationService localizationService,
        IPermissionService permissionService,
        IPictureService pictureService,
        IPriceCalculationService priceCalculationService,
        IPriceFormatter priceFormatter,
        ISettingService settingService,
        IStoreContext storeContext,
        ITaxService taxService,
        IUrlRecordService urlRecordService,
        IWorkContext workContext,
        OrderSettings orderSettings,
        IBulkOrderGridBuilder bulkOrderGridBuilder,
        IBulkOrderPricingService bulkOrderPricingService,
        IProductService productService)
    {
        _catalogSettings = catalogSettings;
        _bulkOrderService = bulkOrderService;
        _categoryService = categoryService;
        _currencyService = currencyService;
        _dateTimeHelper = dateTimeHelper;
        _localizationService = localizationService;
        _permissionService = permissionService;
        _pictureService = pictureService;
        _priceCalculationService = priceCalculationService;
        _priceFormatter = priceFormatter;
        _settingService = settingService;
        _storeContext = storeContext;
        _taxService = taxService;
        _urlRecordService = urlRecordService;
        _workContext = workContext;
        _orderSettings = orderSettings;
        _bulkOrderGridBuilder = bulkOrderGridBuilder;
        _bulkOrderPricingService = bulkOrderPricingService;
        _productService = productService;
    }

    #endregion

    #region Utilities

    /// <summary>
    /// Gets the page sizes the store offers, always including the configured default
    /// </summary>
    /// <param name="settings">Store settings</param>
    /// <returns>Page sizes, ascending</returns>
    protected virtual IList<int> GetAllowedPageSizes(MultiStoreSettings settings)
    {
        var sizes = (settings.BulkOrderPageSizeOptions ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(value => int.TryParse(value, out var size) ? size : 0)
            .Where(size => size > 0)
            .Distinct()
            .OrderBy(size => size)
            .ToList();

        var defaultSize = settings.BulkOrderDefaultPageSize > 0 ? settings.BulkOrderDefaultPageSize : 15;

        if (!sizes.Contains(defaultSize))
        {
            sizes.Add(defaultSize);
            sizes.Sort();
        }

        return sizes;
    }

    /// <summary>
    /// Resolves the page size actually used
    /// </summary>
    /// <remarks>
    /// PageSize arrives on the query string. A value the store does not offer is discarded rather than
    /// clamped, because "?PageSize=100000" is otherwise a one-line way to make the server render the
    /// whole catalogue - grid and all.
    /// </remarks>
    protected virtual int ResolvePageSize(BulkOrderCommand command, MultiStoreSettings settings)
    {
        var allowed = GetAllowedPageSizes(settings);

        return allowed.Contains(command.PageSize)
            ? command.PageSize
            : settings.BulkOrderDefaultPageSize > 0 ? settings.BulkOrderDefaultPageSize : allowed.First();
    }

    /// <summary>
    /// Resolves the view mode: the request wins, the store setting decides when the request is silent
    /// </summary>
    protected virtual string ResolveViewMode(BulkOrderCommand command, MultiStoreSettings settings)
    {
        if (string.Equals(command.ViewMode, BulkOrderDefaults.VIEW_MODE_LIST, StringComparison.OrdinalIgnoreCase))
            return BulkOrderDefaults.VIEW_MODE_LIST;

        if (string.Equals(command.ViewMode, BulkOrderDefaults.VIEW_MODE_TILED, StringComparison.OrdinalIgnoreCase))
            return BulkOrderDefaults.VIEW_MODE_TILED;

        //not supplied, or not a value we render - the admin dropdown's values are 0 = grid/tiled, 1 = list
        return settings.BulkOrderDefaultViewMode == 1
            ? BulkOrderDefaults.VIEW_MODE_LIST
            : BulkOrderDefaults.VIEW_MODE_TILED;
    }

    /// <summary>
    /// Prepares the filter dropdown
    /// </summary>
    /// <param name="model">Bulk ordering model</param>
    /// <param name="selected">Selected filter</param>
    /// <param name="hasOrders">Whether the customer has any order to filter by</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    protected virtual async Task PrepareFiltersAsync(BulkOrderingModel model, BulkOrderFilterType selected, bool hasOrders)
    {
        foreach (var filterType in Enum.GetValues<BulkOrderFilterType>())
        {
            //nothing to pick from, so the filter would only produce an empty list
            if (filterType == BulkOrderFilterType.LatestOrdered && !hasOrders)
                continue;

            model.AvailableFilters.Add(new SelectListItem
            {
                Text = await _localizationService.GetResourceAsync($"Plugins.CW.Core.BulkOrder.Filter.{filterType}"),
                Value = filterType.ToString(),
                Selected = filterType == selected
            });
        }
    }

    /// <summary>
    /// Prepares the "from order" dropdown
    /// </summary>
    /// <param name="model">Bulk ordering model</param>
    /// <param name="orders">The customer's orders, newest first</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    protected virtual async Task PrepareOrdersAsync(BulkOrderingModel model, IList<Order> orders)
    {
        model.AvailableOrders.Add(new SelectListItem
        {
            Text = await _localizationService.GetResourceAsync("Plugins.CW.Core.BulkOrder.Order.Choose"),
            Value = "0",
            Selected = model.OrderId == 0
        });

        foreach (var order in orders)
        {
            //"d" is the short date pattern of the current culture; a hardcoded "MM/dd/yy"
            //reads as the wrong date for every non-US store
            var createdOn = (await _dateTimeHelper.ConvertToUserTimeAsync(order.CreatedOnUtc, DateTimeKind.Utc)).ToString("d");

            var number = !string.IsNullOrEmpty(order.CustomOrderNumber) ? order.CustomOrderNumber : order.Id.ToString();

            model.AvailableOrders.Add(new SelectListItem
            {
                Text = $"{number} - {createdOn}",
                Value = order.Id.ToString(),
                Selected = model.OrderId == order.Id
            });
        }
    }

    /// <summary>
    /// Prepares the sort dropdown
    /// </summary>
    /// <param name="model">Bulk ordering model</param>
    /// <param name="selected">Selected sorting</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    protected virtual async Task PrepareSortOptionsAsync(BulkOrderingModel model, BulkOrderSorting selected)
    {
        //an option the merchant disabled for the catalogue is disabled here too.
        //SkuAsc/SkuDesc (7/8) have no counterpart in nop's ProductSortingEnum, so they can never
        //appear in ProductSortingEnumDisabled and are never filtered out by accident
        var options = Enum.GetValues<BulkOrderSorting>()
            .Where(option => !_catalogSettings.ProductSortingEnumDisabled.Contains((int)option))
            .ToList();

        //never render a dropdown that cannot show the current state as selected
        if (!options.Contains(selected))
            options.Insert(0, selected);

        foreach (var option in options)
            model.AvailableSortOptions.Add(new SelectListItem
            {
                Text = await _localizationService.GetLocalizedEnumAsync(option),
                Value = ((int)option).ToString(),
                Selected = option == selected
            });
    }

    /// <summary>
    /// Prepares the page-size dropdown
    /// </summary>
    /// <param name="model">Bulk ordering model</param>
    /// <param name="settings">Store settings</param>
    protected virtual void PreparePageSizes(BulkOrderingModel model, MultiStoreSettings settings)
    {
        foreach (var size in GetAllowedPageSizes(settings))
            model.AvailablePageSizes.Add(new SelectListItem
            {
                Text = size.ToString(),
                Value = size.ToString(),
                Selected = size == model.PageSize
            });
    }

    /// <summary>
    /// Prepares the view-mode dropdown
    /// </summary>
    /// <param name="model">Bulk ordering model</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    protected virtual async Task PrepareViewModesAsync(BulkOrderingModel model)
    {
        model.AvailableViewModes.Add(new SelectListItem
        {
            Text = await _localizationService.GetResourceAsync("Catalog.ViewMode.Grid"),
            Value = BulkOrderDefaults.VIEW_MODE_TILED,
            Selected = model.ViewMode == BulkOrderDefaults.VIEW_MODE_TILED
        });

        model.AvailableViewModes.Add(new SelectListItem
        {
            Text = await _localizationService.GetResourceAsync("Catalog.ViewMode.List"),
            Value = BulkOrderDefaults.VIEW_MODE_LIST,
            Selected = model.ViewMode == BulkOrderDefaults.VIEW_MODE_LIST
        });
    }

    /// <summary>
    /// Formats the product price for the list
    /// </summary>
    /// <remarks>
    /// One string, and it is the price the customer would actually pay (nop's product box splits this into
    /// Price and PriceWithDiscount and lets the theme render both). This is
    /// the product-level figure shown beside the name.
    /// </remarks>
    protected virtual async Task<string> PreparePriceAsync(Product product, Customer customer, Store store, Currency currency, bool displayPrices)
    {
        if (!displayPrices || product.CustomerEntersPrice)
            return null;

        if (product.CallForPrice &&
            (!_orderSettings.AllowAdminsToBuyCallForPriceProducts || _workContext.OriginalCustomerIfImpersonated == null))
            return await _localizationService.GetResourceAsync("Products.CallForPrice");

        var (_, finalPrice, _, _) = await _priceCalculationService.GetFinalPriceAsync(product, customer, store);
        var (finalPriceBase, _) = await _taxService.GetProductPriceAsync(product, finalPrice);

        return await _priceFormatter.FormatPriceAsync(
            await _currencyService.ConvertFromPrimaryStoreCurrencyAsync(finalPriceBase, currency));
    }

    /// <summary>
    /// Builds one product's grid model, and reports the price span the product line needs from it.
    /// </summary>
    /// <remarks>
    /// Both the full page and the per-card AJAX reload come through here, which is what keeps them agreeing:
    /// the decision to show per-cell prices is made ONCE, in this method, rather than by
    /// each caller. A reloaded card that priced its cells differently from the page around it would be a
    /// bug nobody would think to look for.
    /// </remarks>
    protected virtual async Task<BulkOrderGridResult> PrepareGridAsync(Product product,
        Customer customer,
        Store store,
        Currency currency,
        MultiStoreSettings settings,
        bool displayPrices,
        IDictionary<int, IList<int>> pinSelections,
        IDictionary<int, int> tierQuantities = null,
        IDictionary<(int PictureId, int TargetSize), string> pictureUrls = null)
    {
        //Deduped per PAGE when the caller supplies the dictionary: GetPictureByIdAsync is uncached
        //(cache => default, PictureService.cs:687), so two products sharing a swatch would otherwise query
        //it twice. Keyed by size as well as id - the same picture is asked for at two sizes.
        pictureUrls ??= new Dictionary<(int PictureId, int TargetSize), string>();

        //same shape, same reason: supplied by a page build, resolved here for the single-card Grid endpoint.
        //One cart read either way - what this replaces was one read per card.
        tierQuantities ??= await _bulkOrderPricingService.GetTierQuantitiesAsync(customer);

        var grid = await _bulkOrderGridBuilder.BuildAsync(product, pinSelections,
            settings.BulkOrderAxisAttributeIds, settings.BulkOrderMaxCellsPerProduct,
            settings.BulkOrderShowAttributeImages);

        //this product cannot be ordered here at all, so stop before anything customer-specific
        //is resolved. BuildAsync already returned without loading attributes or combinations; PrepareAsync
        //does not know that and would price a grid with no cells - reading every combination, asking the
        //availability provider for stock, walking every mapping's values and calling GetFinalPriceAsync once
        //for an input nothing consumes. All of it discarded. The card still shows its picture, name, SKU and
        //price, because the product line falls back to PreparePriceAsync when no cell produced one.
        if (grid.UnsupportedReason != BulkOrderUnsupportedReason.None)
        {
            var reason = await _localizationService.GetResourceAsync(
                $"Plugins.CW.Core.BulkOrder.Grid.Unsupported.{grid.UnsupportedReason}");

            return new BulkOrderGridResult(new BulkOrderGridModel
            {
                ProductId = product.Id,
                ProductSeName = await _urlRecordService.GetSeNameAsync(product),

                //only RequiredTextAttribute carries a detail, and its resource is the only one with a
                //placeholder - string.Format on a string with none returns it unchanged, so one call serves
                //all five and adding a sixth reason stays a resource-only change
                UnsupportedReason = string.Format(reason, grid.UnsupportedDetail),

                //The pins come WITH the card. A product-level refusal that dropped them took the strip with
                //it, so the checkbox that caused the refusal was no longer on screen and only a
                //page reload brought it back. The builder carries them on this return for exactly
                //this reason; leaving them out here threw that away one layer above it.
                Pins = [.. grid.Pins.Select(ToPinModel)]
            }, null, null);
        }

        var pricing = await _bulkOrderPricingService.PrepareAsync(product, grid, customer, store, displayPrices,
            tierQuantities.TryGetValue(product.Id, out var tierQuantity) ? tierQuantity : 1);

        var cellPrices = grid.Rows
            .SelectMany(row => row.Cells)
            .Where(cell => cell.Exists)
            .Select(cell => pricing.StateFor(cell).Price)
            .Where(price => price.HasValue)
            .Select(price => price.Value)
            .ToList();

        //one formatted string per DISTINCT price, not per cell: FormatPriceAsync and
        //ConvertFromPrimaryStoreCurrencyAsync both load the working currency, and a 400-cell product would
        //otherwise do it 400 times to produce at most a handful of different strings.
        var priceStrings = new Dictionary<decimal, string>();

        foreach (var price in cellPrices.Distinct())
            priceStrings[price] = await _priceFormatter.FormatPriceAsync(
                await _currencyService.ConvertFromPrimaryStoreCurrencyAsync(price, currency), true, false);

        //A per-cell price column earns its place only when the cells DISAGREE - and on the
        //measured catalogue almost no product has a price-adjusting value, so for most of them this is off.
        var samePrice = cellPrices.Count == 0 || cellPrices.Min() == cellPrices.Max();
        var showCellPrices = settings.BulkOrderShowAttributePrices && !samePrice;

        //the accessible name for a single-cell product, which has no coordinates to be named by. Resolved here
        //rather than carried on the model: one cell on one kind of product needs it, and GetLocalizedAsync is
        //a cached localized-property read, not a query.
        var productName = await _localizationService.GetLocalizedAsync(product, x => x.Name);

        var squaresSize = settings.BulkOrderAttributeImageSquaresSize > 0
            ? settings.BulkOrderAttributeImageSquaresSize
            : 100;

        var imageSize = settings.BulkOrderAttributeImageSize > 0
            ? settings.BulkOrderAttributeImageSize
            : 100;

        async Task<string> pictureUrlAsync(int pictureId, int targetSize)
        {
            if (pictureId <= 0)
                return null;

            if (pictureUrls.TryGetValue((pictureId, targetSize), out var cached))
                return cached;

            var picture = await _pictureService.GetPictureByIdAsync(pictureId);
            var (url, _) = await _pictureService.GetPictureUrlAsync(picture, targetSize);

            return pictureUrls[(pictureId, targetSize)] = url;
        }

        async Task<BulkOrderGridValueModel> toValueModelAsync(BulkOrderAxisValue value)
        {
            return new BulkOrderGridValueModel
            {
                Id = value.Id,
                Name = value.Name,
                ColorSquaresRgb = value.ColorSquaresRgb,
                ImageSquaresUrl = await pictureUrlAsync(value.ImageSquaresPictureId, squaresSize),
                ImageUrl = await pictureUrlAsync(value.ValuePictureId, imageSize)
            };
        }

        //Axis names resolved ONCE. Inside toCellModel they would be rebuilt per cell, and a 400-cell product
        //would walk grid.Axes four hundred times to produce the same two strings.
        var columnAxisName = grid.Axes.FirstOrDefault()?.Name;
        var rowAxisNames = grid.Axes.Skip(1).Select(axis => axis.Name).ToList();

        BulkOrderGridCellModel toCellModel(BulkOrderRow row, BulkOrderCell cell)
        {
            var state = pricing.StateFor(cell);

            //Axis 1 FIRST. It supplies the columns and is the primary attribute, so it is what the customer
            //reads across the top of the grid and the first thing they need in order to find the cell -
            //"Size S, Color Navy", never "Color Navy, Size S". row.Labels is in axis order, the same order
            //grid.Axes.Skip(1) produces, so the two zip by index.
            //A single cell has no coordinates at all and falls back to the product's name: a screen reader
            //announcing an empty string is worse than no label.
            var labels = new List<string>();

            if (cell.Column != null)
                labels.Add(string.IsNullOrEmpty(columnAxisName)
                    ? cell.Column.Name
                    : $"{columnAxisName} {cell.Column.Name}");

            labels.AddRange(row.Labels.Select((label, index) => index < rowAxisNames.Count
                ? $"{rowAxisNames[index]} {label.Name}"
                : label.Name));

            var axisKey = string.Join(",", cell.AxisSelection
                .SelectMany(part => part.ValueIds.Select(valueId => $"{part.MappingId}:{valueId}")));

            return new BulkOrderGridCellModel
            {
                Exists = cell.Exists,
                Sku = cell.Sku,
                Selection = string.Join(",", cell.Selection
                    .SelectMany(part => part.ValueIds.Select(valueId => $"{part.MappingId}:{valueId}"))),

                //a grid with no axes has no coordinates, so its one cell gets a constant identity - the same
                //constant the view used to hard-code. reloadGrid matches on this and must find it again.
                AxisKey = string.IsNullOrEmpty(axisKey) ? "product" : axisKey,

                Label = labels.Count > 0 ? string.Join(", ", labels) : productName,
                Disabled = state.Disabled,
                CallForPrice = state.CallForPrice,

                //Comes straight from the builder rather than from the pricing service:
                //it is a fact about the product's attributes, not about the customer, their cart or stock -
                //which is also why it rides on the cacheable skeleton.
                RequiresTextAttribute = cell.RequiresTextAttribute,

                //same provenance as RequiresTextAttribute: a fact about the pins, from
                //the builder, on the skeleton
                RequiresPinChoice = cell.RequiresPinChoice,

                MaxAvailable = state.MaxAvailable,
                StockMessage = state.StockMessage,
                Price = showCellPrices && state.Price.HasValue
                    ? priceStrings[state.Price.Value]
                    : null
            };
        }

        var model = new BulkOrderGridModel
        {
            ProductId = product.Id,
            IsSingleCell = grid.Axes.Count == 0,
            MinQuantity = product.OrderMinimumQuantity,
            AllowedQuantities = _productService.ParseAllowedQuantities(product),
            HasNoMatchingCombinations = grid.Axes.Count > 0 && grid.Rows.Count == 0 && grid.HasCombinations,
            IsNotOrderable = grid.Axes.Count > 0 && grid.Rows.Count == 0 && !grid.HasCombinations,
            DemotedAxisNames = grid.DemotedAxisNames,
            ExceedsBudget = grid.ExceedsBudget,
            HideQtyArrows = settings.BulkOrderHideQtyArrows,
            ShowAttributeImages = settings.BulkOrderShowAttributeImages,
            AttributeImageSize = $"{imageSize}px",
            AttributeImageSquaresSize = $"{squaresSize}px",
            ColumnAxisName = grid.Axes.FirstOrDefault()?.Name,
            RowAxisNames = [.. grid.Axes.Skip(1).Select(axis => axis.Name)],
            ShowCellPrices = showCellPrices,
            ShowCellStock = settings.BulkOrderShowProductStock,
            //UnsupportedReason is deliberately absent: the early return above is the only path that sets it,
            //so anything reaching here is orderable and leaving it null is the honest answer rather than a
            //condition that can only ever be false.
            ProductSeName = await _urlRecordService.GetSeNameAsync(product),
            ProductName = await _localizationService.GetLocalizedAsync(product, entity => entity.Name),
            RequiredTextAttributeName = grid.RequiredTextAttributeName,
            RequiredChoiceAttributeName = grid.RequiredChoiceAttributeName,
        };

        foreach (var column in grid.Columns)
            model.Columns.Add(await toValueModelAsync(column));

        foreach (var pin in grid.Pins)
            model.Pins.Add(ToPinModel(pin));

        foreach (var row in grid.Rows)
        {
            //a single cell is not a table row - it is rendered on its own, so it goes to
            //SingleCell and no BulkOrderGridRowModel is built for it
            if (model.IsSingleCell)
            {
                model.SingleCell = row.Cells.Count > 0
                    ? toCellModel(row, row.Cells[0])
                    : null;

                continue;
            }

            var rowModel = new BulkOrderGridRowModel();

            foreach (var label in row.Labels)
                rowModel.Labels.Add(await toValueModelAsync(label));

            foreach (var cell in row.Cells)
                rowModel.Cells.Add(toCellModel(row, cell));

            model.Rows.Add(rowModel);
        }

        return new BulkOrderGridResult(model,
            cellPrices.Count > 0 ? cellPrices.Min() : null,
            cellPrices.Count > 0 ? cellPrices.Max() : null);
    }

    /// <summary>
    /// Prepares one product row
    /// </summary>
    /// <returns>A task that represents the asynchronous operation</returns>
    protected virtual async Task<BulkOrderProductModel> PrepareProductModelAsync(Product product,
        Customer customer,
        Store store,
        Currency currency,
        MultiStoreSettings settings,
        bool displayPrices,
        IDictionary<int, IDictionary<int, IList<int>>> pinSelections,
        IDictionary<int, int> quantityInCartByProductId,
        IDictionary<int, int> tierQuantities,
        IDictionary<(int PictureId, int TargetSize), string> pictureUrls)
    {
        //the grid first: the product-level figure is DERIVED from the cells. Computing the two
        //independently makes them disagree on discounted products - one answer, one place.
        var built = await PrepareGridAsync(product, customer, store, currency, settings, displayPrices,
            pinSelections.TryGetValue(product.Id, out var productPins) ? productPins : null, tierQuantities, pictureUrls);

        var samePrice = !built.MinCellPrice.HasValue || built.MinCellPrice == built.MaxCellPrice;

        var model = new BulkOrderProductModel
        {
            Id = product.Id,
            Sku = product.Sku,
            Name = await _localizationService.GetLocalizedAsync(product, x => x.Name),
            SeName = built.Model.ProductSeName,
            SameAttributesPrice = samePrice,
            QuantityInCart = quantityInCartByProductId.TryGetValue(product.Id, out var quantity) ? quantity : 0,
            Grid = built.Model
        };

        //the headline figure: the one cell price when every cell agrees, otherwise the product's own price
        model.Price = built.MinCellPrice.HasValue && samePrice
            ? await FormatAsync(built.MinCellPrice.Value)
            : await PreparePriceAsync(product, customer, store, currency, displayPrices);

        //...and a range when the cells disagree but the per-cell column is off, so the customer is never left
        //with one number that half the grid contradicts
        if (!samePrice && !built.Model.ShowCellPrices)
            model.PriceRange = string.Format(
                await _localizationService.GetResourceAsync("Plugins.CW.Core.BulkOrder.PriceRange"),
                await FormatAsync(built.MinCellPrice.Value), await FormatAsync(built.MaxCellPrice.Value));

        if (!settings.BulkOrderShowProductImages)
            return model;

        var picture = (await _pictureService.GetPicturesByProductIdAsync(product.Id, 1)).FirstOrDefault();

        //GetPictureUrlAsync handles a null picture by returning the store's default image
        (model.ImageUrl, picture) = await _pictureService.GetPictureUrlAsync(picture, settings.BulkOrderProductImageSize);

        model.ImageAlternateText = !string.IsNullOrEmpty(picture?.AltAttribute)
            ? picture.AltAttribute
            : string.Format(await _localizationService.GetResourceAsync("Media.Product.ImageAlternateTextFormat"), model.Name);

        return model;

        //One formatter for every figure on the PRODUCT LINE - the single price and both ends of the range.
        //The one-argument overload is the same one PreparePriceAsync uses, so it honours the store's tax-suffix
        //setting (PriceFormatter.cs:138-141) instead of forcing the suffix on. Forcing it made a product whose
        //cells agreed read "incl. VAT" while the product below it, which fell back to PreparePriceAsync, did
        //not - on one page, for no reason a customer could see.
        //Cells are deliberately NOT formatted with this: they pass showTax false, because repeating the suffix
        //in 400 boxes is unreadable. Each surface is consistent with itself, which is what matters.
        async Task<string> FormatAsync(decimal price)
            => await _priceFormatter.FormatPriceAsync(
                await _currencyService.ConvertFromPrimaryStoreCurrencyAsync(price, currency));
    }

    /// <summary>
    /// Builds the category tree under one parent, keeping only categories that have visible products
    /// </summary>
    /// <param name="allCategories">Every category of the store</param>
    /// <param name="visibleCategoryIds">Ids that survived the visibility walk, plus their ancestors</param>
    /// <param name="parentCategoryId">Parent category identifier</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    protected virtual async Task<List<CategorySimpleModel>> PrepareCategoryTreeAsync(IList<Category> allCategories,
        IReadOnlySet<int> visibleCategoryIds,
        int parentCategoryId)
    {
        var result = new List<CategorySimpleModel>();

        var children = allCategories
            .Where(category => category.ParentCategoryId == parentCategoryId && visibleCategoryIds.Contains(category.Id))
            .OrderBy(category => category.DisplayOrder)
            .ThenBy(category => category.Id);

        foreach (var category in children)
        {
            result.Add(new CategorySimpleModel
            {
                Id = category.Id,
                Name = await _localizationService.GetLocalizedAsync(category, x => x.Name),
                SubCategories = await PrepareCategoryTreeAsync(allCategories, visibleCategoryIds, category.Id)
            });
        }

        return result;
    }

    /// <summary>
    /// Windows the page numbers around the current page, using nop's own arithmetic
    /// (Presentation/Nop.Web.Framework/UI/Paging/Pager.cs:456-481) so the pager behaves exactly like the
    /// catalogue's. Ported rather than referenced: nop's Pager renders links and navigates, which would
    /// discard every typed quantity on the page.
    /// </summary>
    /// <param name="pageNumber">Current page, 1-based</param>
    /// <param name="totalPages">Total pages</param>
    /// <returns>Page numbers to render, 1-based; empty when there is nothing to page through</returns>
    protected virtual IList<int> BuildPageNumbers(int pageNumber, int totalPages)
    {
        if (totalPages <= 1)
            return [];

        var window = BulkOrderDefaults.PAGER_WINDOW;

        //nop works in 0-based indexes here; keep its arithmetic identical and convert at the end
        var pageIndex = pageNumber - 1;
        var half = window / 2;

        var first = totalPages < window || pageIndex - half < 0
            ? 0
            : pageIndex + half >= totalPages
                ? totalPages - window
                : pageIndex - half;

        //nop's GetLastIndividualPageIndex subtracts one more when the window is even
        var back = window % 2 == 0 ? half - 1 : half;

        var last = totalPages < window || pageIndex + back >= totalPages
            ? totalPages - 1
            : pageIndex - half < 0
                ? window - 1
                : pageIndex + back;

        var numbers = new List<int>();

        for (var i = Math.Max(first, 0); i <= Math.Min(last, totalPages - 1); i++)
            numbers.Add(i + 1);

        return numbers;
    }

    /// <summary>
    /// One pinned attribute, for the strip. Shared by BOTH places that build a grid model — the early
    /// return for an unsupported product and the main path — because they drifted the moment one gained a field.
    /// </summary>
    /// <remarks>
    /// Values carry Id, Name and ColorSquaresRgb only. `_PinnedStrip.cshtml` renders a value's NAME and
    /// nothing else, so resolving ImageSquaresUrl and ImageUrl for every pin value was picture-service work
    /// for output nobody reads — twenty lookups on a twenty-value checkbox attribute. ColorSquaresRgb stays
    /// because it comes straight off the domain record and costs nothing, and a demoted ColorSquares
    /// attribute is a plausible pin.
    /// </remarks>
    /// <param name="pin">The pinned attribute</param>
    /// <returns>The pin model</returns>
    protected virtual BulkOrderPinModel ToPinModel(BulkOrderPin pin)
    {
        var model = new BulkOrderPinModel
        {
            Id = pin.ProductAttributeMappingId,
            Name = pin.Name,
            TextPrompt = pin.TextPrompt,
            IsRequired = pin.IsRequired,
            AttributeControlTypeId = pin.AttributeControlTypeId,
            SelectedValueIds = pin.SelectedValueIds,
            DefaultValueIds = pin.DefaultValueIds,
            WasDemoted = pin.WasDemoted,
            IsConditional = pin.IsConditional,
            AppliesToValueName = pin.AppliesToValueName,
            IsBlockedByCondition = pin.IsBlockedByCondition,
            DependsOnName = pin.DependsOnName
        };

        foreach (var value in pin.Values)
            model.Values.Add(new BulkOrderGridValueModel
            {
                Id = value.Id,
                Name = value.Name,
                ColorSquaresRgb = value.ColorSquaresRgb
            });

        return model;
    }

    #endregion

    #region Methods

    /// <summary>
    /// Prepares the page shell and the first page of products, rendered server-side.
    /// </summary>
    /// <param name="command">Bulk ordering command</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public async Task<BulkOrderingModel> PrepareShellModelAsync(BulkOrderCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        var store = await _storeContext.GetCurrentStoreAsync();
        var settings = await _settingService.LoadSettingAsync<MultiStoreSettings>(store.Id);

        var orders = await _bulkOrderService.GetCustomerOrdersAsync(store.Id, BulkOrderDefaults.MAX_ORDER_PICKER_ITEMS);

        //do not offer - or honour - a filter whose picker would be empty
        var filterType = command.GetFilterType();
        if (filterType == BulkOrderFilterType.LatestOrdered && !orders.Any())
            filterType = BulkOrderFilterType.AllProducts;

        //normalise before the list is built from it, so the first page and the dropdowns cannot disagree.
        //An order id that is not this customer's is dropped here and rejected again in the service.
        var normalised = command with
        {
            FilterMode = filterType.ToString(),
            OrderId = orders.Any(order => order.Id == command.OrderId) ? command.OrderId : 0,
            PageNumber = command.PageNumber > 0 ? command.PageNumber : 1
        };

        //the first page is rendered server-side: one request instead of two, no empty first paint,
        //the page number in the URL survives a refresh, and the page degrades without JavaScript
        var model = await PrepareItemsModelAsync(normalised);

        await PrepareFiltersAsync(model, filterType, orders.Any());
        await PrepareOrdersAsync(model, orders);
        await PrepareSortOptionsAsync(model, normalised.GetSorting());
        PreparePageSizes(model, settings);
        await PrepareViewModesAsync(model);

        return model;
    }

    /// <summary>
    /// Prepares the paged product list
    /// </summary>
    /// <param name="command">Bulk ordering command</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public async Task<BulkOrderingModel> PrepareItemsModelAsync(BulkOrderCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        var store = await _storeContext.GetCurrentStoreAsync();
        var customer = await _workContext.GetCurrentCustomerAsync();
        var currency = await _workContext.GetWorkingCurrencyAsync();
        var settings = await _settingService.LoadSettingAsync<MultiStoreSettings>(store.Id);

        var filterType = command.GetFilterType();
        var sorting = command.GetSorting();

        //an order that is not one of this customer's latest is not honoured (as on the page shell), so a made-up
        //order id cannot make the service read and cache another tree
        if (filterType == BulkOrderFilterType.LatestOrdered && command.OrderId > 0
            && !(await _bulkOrderService.GetCustomerOrdersAsync(store.Id, BulkOrderDefaults.MAX_ORDER_PICKER_ITEMS)).Any(order => order.Id == command.OrderId))
            command = command with { OrderId = 0 };

        //normalise the category alongside everything else: one that is not in the tree
        //cannot be picked in the UI, so honouring it - from a stale URL, or because a filter change
        //dropped it from the tree - only produces an empty list with nothing explaining why.
        //GetVisibleCategoryIdsAsync is cached and the navigation in this same response reads the same
        //key, so this is a cache read rather than a second query.
        var categoryId = command.CategoryId;
        if (categoryId > 0)
        {
            var visibleCategoryIds = await _bulkOrderService.GetVisibleCategoryIdsAsync(filterType, store.Id, command.OrderId);

            if (!visibleCategoryIds.Contains(categoryId))
                categoryId = 0;
        }

        var model = new BulkOrderingModel
        {
            FilterMode = filterType.ToString(),
            OrderId = command.OrderId,
            OrderBy = (int)sorting,
            CategoryId = categoryId,
            ViewMode = ResolveViewMode(command, settings),
            ShowImages = settings.BulkOrderShowProductImages,
            ImageSize = $"{settings.BulkOrderProductImageSize}px"
        };

        var query = new BulkOrderQuery
        {
            StoreId = store.Id,
            FilterType = filterType,
            OrderId = command.OrderId,
            CategoryId = categoryId,
            OrderBy = sorting,
            PageIndex = Math.Max(command.PageNumber - 1, 0),
            PageSize = ResolvePageSize(command, settings)
        };

        var products = await _bulkOrderService.SearchProductsAsync(query);

        //a page past the end is an empty list with no pager - and now that the first page is rendered
        //server-side, a stale URL lands the customer there with nothing to click. Show the last page.
        if (products.TotalCount > 0 && query.PageIndex >= products.TotalPages)
            products = await _bulkOrderService.SearchProductsAsync(query with { PageIndex = products.TotalPages - 1 });

        model.PageNumber = products.PageIndex + 1;
        model.PageSize = products.PageSize;
        model.TotalItems = products.TotalCount;
        model.TotalPages = products.TotalPages;
        model.PageNumbers = BuildPageNumbers(model.PageNumber, model.TotalPages);

        //ONE cart resolution, ONE permission check and ONE picture cache for the whole page
        var quantityInCartByProductId = await _bulkOrderService.GetCartQuantitiesAsync(customer, store.Id);
        var displayPrices = await _permissionService.AuthorizeAsync(StandardPermission.PublicStore.DISPLAY_PRICES);
        var pinSelections = command.GetPinSelections();
        var pictureUrls = new Dictionary<(int PictureId, int TargetSize), string>();

        //and ONE tier read. A SECOND cart read, deliberately: the line above is
        //packname-scoped for the "N in cart" badge, this one counts the whole cart because nop's tier lookup
        //does. Merging them would price some carts at the wrong tier.
        var tierQuantities = await _bulkOrderPricingService.GetTierQuantitiesAsync(customer);

        foreach (var product in products)
            model.Items.Add(await PrepareProductModelAsync(product, customer, store, currency, settings, displayPrices, pinSelections, quantityInCartByProductId, tierQuantities, pictureUrls));

        return model;
    }

    /// <summary>
    /// Prepares the bulk ordering category navigation
    /// </summary>
    /// <param name="bulkOrdering">The page model, already normalised</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public async Task<BulkOrderCategoryNavigationModel> PrepareCategoryNavigationModelAsync(BulkOrderingModel bulkOrdering)
    {
        ArgumentNullException.ThrowIfNull(bulkOrdering);

        var store = await _storeContext.GetCurrentStoreAsync();
        var filterType = bulkOrdering.FilterMode.ToBulkOrderFilterType();

        var model = new BulkOrderCategoryNavigationModel
        {
            CategoryId = bulkOrdering.CategoryId,

            //normalised, so a junk FilterMode cannot be copied into every category link
            FilterMode = filterType.ToString(),
            OrderId = bulkOrdering.OrderId,

            //carried so that opening a category in a new tab, or following the link without JavaScript,
            //keeps the customer's sort, page size and view mode. PageNumber deliberately is not carried:
            //a different category starts at page 1.
            OrderBy = bulkOrdering.OrderBy,
            PageSize = bulkOrdering.PageSize,
            ViewMode = bulkOrdering.ViewMode
        };

        var visibleCategoryIds = await _bulkOrderService.GetVisibleCategoryIdsAsync(filterType, store.Id, bulkOrdering.OrderId);

        if (!visibleCategoryIds.Any())
            return model;

        //GetAllCategoriesAsync is cached and already applies store mapping and ACL; the visible-id set
        //is what narrows it to categories this customer can actually order from
        var allCategories = await _categoryService.GetAllCategoriesAsync(storeId: store.Id);

        model.Categories = await PrepareCategoryTreeAsync(allCategories, visibleCategoryIds.ToHashSet(), 0);

        return model;
    }

    public async Task<BulkOrderGridModel> PrepareGridModelAsync(int productId, string pins)
    {
        var store = await _storeContext.GetCurrentStoreAsync();
        var product = await _bulkOrderService.GetVisibleProductByIdAsync(productId, store.Id);

        if (product == null)
            return null;

        var settings = await _settingService.LoadSettingAsync<MultiStoreSettings>(store.Id);
        var selections = new BulkOrderCommand { Pins = pins }.GetPinSelections();

        //one card, so these are resolved here rather than being passed down from a page build. Same three
        //values the list path resolves once for the whole page (:674-677) - the reload must not price or
        //permission-check differently from the page it is replacing part of.
        var customer = await _workContext.GetCurrentCustomerAsync();
        var currency = await _workContext.GetWorkingCurrencyAsync();
        var displayPrices = await _permissionService.AuthorizeAsync(StandardPermission.PublicStore.DISPLAY_PRICES);

        var built = await PrepareGridAsync(product, customer, store, currency, settings, displayPrices,
            selections.TryGetValue(productId, out var forProduct) ? forProduct : null);

        return built.Model;
    }

    #endregion
}