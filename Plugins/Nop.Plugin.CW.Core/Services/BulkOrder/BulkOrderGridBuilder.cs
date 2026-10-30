using Nop.Core;
using Nop.Core.Caching;
using Nop.Core.Domain.Catalog;
using Nop.Plugin.CW.Core.Domain.BulkOrder;
using Nop.Services.Catalog;
using Nop.Services.Localization;
using System.Collections.Immutable;
using System.Xml;

namespace Nop.Plugin.CW.Core.Services.BulkOrder;

/// <summary>
/// Represents the bulk ordering grid builder
/// </summary>
public class BulkOrderGridBuilder : IBulkOrderGridBuilder
{
    #region Fields

    protected readonly ILocalizationService _localizationService;
    protected readonly IProductAttributeService _productAttributeService;
    protected readonly IStaticCacheManager _staticCacheManager;
    protected readonly IWorkContext _workContext;

    #endregion

    #region Ctor

    public BulkOrderGridBuilder(ILocalizationService localizationService,
        IProductAttributeService productAttributeService,
        IStaticCacheManager staticCacheManager,
        IWorkContext workContext)
    {
        _localizationService = localizationService;
        _productAttributeService = productAttributeService;
        _staticCacheManager = staticCacheManager;
        _workContext = workContext;
    }

    #endregion

    #region Nested

    /// <summary>Everything the pure part needs, read once.</summary>
    protected class GridData
    {
        public Product Product { get; init; }

        public IList<ProductAttributeMapping> Mappings { get; init; } = [];

        /// <summary>Localized attribute name per mapping id.</summary>
        public IDictionary<int, string> Names { get; } = new Dictionary<int, string>();

        /// <summary>Localized text prompt per mapping id.</summary>
        public IDictionary<int, string> Prompts { get; } = new Dictionary<int, string>();

        /// <summary>Values per mapping id, ordered by DisplayOrder then Id, localized.</summary>
        public IDictionary<int, IList<BulkOrderAxisValue>> Values { get; } = new Dictionary<int, IList<BulkOrderAxisValue>>();

        /// <summary>IsPreSelected value ids per mapping id. A checkbox attribute can preselect several.</summary>
        public IDictionary<int, IList<int>> PreSelectedIds { get; } = new Dictionary<int, IList<int>>();

        public IList<ProductAttributeCombination> Combinations { get; init; } = [];

        /// <summary>The combination index: exact-set-equality lookup, O(1) per cell.</summary>
        public IDictionary<CombinationKey, ProductAttributeCombination> Index { get; } = new Dictionary<CombinationKey, ProductAttributeCombination>();

        /// <summary>Every mapping id that appears in at least one signature.</summary>
        public ISet<int> MappingIdsInAnySignature { get; } = new HashSet<int>();

        /// <summary>
        /// True when nop would reject a selection with no combination row (ShoppingCartService.cs:496-517).
        /// Read once here so BuildAsync's budget projection and Compose's cells cannot disagree about it.
        /// </summary>
        public bool RestrictsToExistingCombinations { get; init; }

        /// <summary>
        /// Parsed conditions, keyed by the mapping that CARRIES the condition. Only mappings that
        /// have one appear, so Count == 0 is the fast path and the overwhelmingly common one.
        /// </summary>
        public IDictionary<int, AttributeCondition> Conditions { get; } = new Dictionary<int, AttributeCondition>();

        /// <summary>The localized name of an optional axis's "(none)" value.</summary>
        public string NoneName { get; init; }
    }

    /// <summary>One pinned attribute's resolved state, computed once per build attempt.</summary>
    protected class PinState
    {
        public ProductAttributeMapping Mapping { get; init; }

        /// <summary>What is selected now. Empty for a text/date/file pin, or when nothing is chosen.</summary>
        public IList<int> Selected { get; init; } = [];

        /// <summary>What would be selected with no customer input, so the client can send only differences.</summary>
        public IList<int> Default { get; init; } = [];
    }

    #endregion

    #region Utilities - loading

    /// <summary>
    /// Reads everything the build needs. The ONLY method in this class that touches a service.
    /// </summary>
    /// <remarks>
    /// Two halves with different lifetimes. The DERIVED half - localized strings, ordered
    /// values with picture ids, every combination's XML parsed into a key, the parsed conditions - is the
    /// same for every customer and is cached as BulkOrderSkeleton. The ENTITY half - the mappings and
    /// combinations themselves - is re-read on every request from IProductAttributeService, whose accessors
    /// are already IStaticCacheManager-backed, so re-reading costs a cache hit and keeps entities out of a
    /// serialized DTO.
    ///
    /// RestrictsToExistingCombinations is taken from the Product ARGUMENT, never from the skeleton: a store
    /// owner flipping ManageInventoryMethod or AllowAddingOnlyExistingAttributeCombinations must be honoured
    /// on the next request, and it is not worth an invalidation to do that.
    /// </remarks>
    protected virtual async Task<GridData> LoadAsync(Product product, bool showAttributeImages)
    {
        var language = await _workContext.GetWorkingLanguageAsync();

        var key = _staticCacheManager.PrepareKeyForDefaultCache(
            BulkOrderServicesDefaults.SkeletonCacheKey, product.Id, language.Id, showAttributeImages);

        var skeleton = await _staticCacheManager.GetAsync(key,
            async () => await BuildSkeletonAsync(product, showAttributeImages));

        return await HydrateAsync(product, skeleton);
    }

    /// <summary>
    /// The cached half: everything that costs work to derive, for one product in one language.
    /// </summary>
    /// <remarks>
    /// This is the old LoadAsync with the entity reads and the two Product-dependent fields taken out. It
    /// runs on a cache miss only, so the XmlDocument parse per combination, the localization lookups
    /// per mapping and per value, and the ProductAttributeValuePicture read per value happen once per
    /// product per language rather than once per request.
    /// </remarks>
    protected virtual async Task<BulkOrderSkeleton> BuildSkeletonAsync(Product product, bool showAttributeImages)
    {
        var mappings = await _productAttributeService.GetProductAttributeMappingsByProductIdAsync(product.Id);
        var combinations = await _productAttributeService.GetAllProductAttributeCombinationsAsync(product.Id);

        var skeleton = new BulkOrderSkeleton();

        var productMappingIds = mappings.Select(mapping => mapping.Id).ToHashSet();

        foreach (var mapping in mappings)
        {
            var attribute = await _productAttributeService.GetProductAttributeByIdAsync(mapping.ProductAttributeId);

            var entry = new BulkOrderSkeletonMapping
            {
                MappingId = mapping.Id,
                Name = attribute == null
                    ? string.Empty
                    : await _localizationService.GetLocalizedAsync(attribute, x => x.Name),
                Prompt = await _localizationService.GetLocalizedAsync(mapping, x => x.TextPrompt)
            };

            //Parse every condition ONCE per product. Not IsConditionMetAsync: that takes an
            //attributes-XML string, so evaluating it per pin state and per axis value would mean building one
            //string per evaluation. ConditionAttributeXml is already on the mapping, so this costs no query.
            var condition = ParseCondition(mapping.Id, mapping.ConditionAttributeXml, productMappingIds);

            if (condition.HasValue)
                entry.Condition = new BulkOrderSkeletonCondition
                {
                    DependsOnMappingId = condition.Value.DependsOnMappingId,
                    RequiredValueIds = [.. condition.Value.RequiredValueIds],
                    Satisfiable = condition.Value.Satisfiable
                };

            //a non-combinable mapping has no values to read (ProductAttributeExtensions.cs:75)
            if (mapping.IsNonCombinable())
            {
                skeleton.Mappings.Add(entry);
                continue;
            }

            var values = await _productAttributeService.GetProductAttributeValuesAsync(mapping.Id);

            //"authorised values" is just the product's values - nop has no ACL on ProductAttributeValue
            //and CW adds none. Ordering is DisplayOrder then Id.
            foreach (var value in values.OrderBy(value => value.DisplayOrder).ThenBy(value => value.Id))
            {
                //The value's picture lives in the ProductAttributeValuePicture mapping since nop 4.70 -
                //ProductAttributeValue.PictureId is [Obsolete] and returns null on any store created since
                //that release. Same route nop's own ProductModelFactory:1007 takes.
                //Only read when the setting is on: it is one service call per value, and the setting is off
                //by default, so the common case pays nothing. The setting is IN THE CACHE KEY, so a store
                //turning it on gets a rebuild rather than a skeleton with no picture ids.
                var valuePictureId = 0;

                if (showAttributeImages)
                {
                    valuePictureId = (await _productAttributeService.GetProductAttributeValuePicturesAsync(value.Id))
                        .FirstOrDefault()?.PictureId ?? 0;
                }

                entry.Values.Add(new BulkOrderAxisValue
                {
                    Id = value.Id,
                    Name = await _localizationService.GetLocalizedAsync(value, x => x.Name),
                    ColorSquaresRgb = value.ColorSquaresRgb,
                    ImageSquaresPictureId = value.ImageSquaresPictureId,
                    ValuePictureId = valuePictureId,
                    DisplayOrder = value.DisplayOrder
                });
            }

            entry.PreSelectedIds = [.. values.Where(value => value.IsPreSelected).Select(value => value.Id)];

            skeleton.Mappings.Add(entry);
        }

        //the index, parsed once. Stored flat and rebuilt by Hydrate: CombinationKey is a
        //record struct over ImmutableArray and cannot be a serialized dictionary key.
        var signature = new HashSet<int>();

        foreach (var combination in combinations)
        {
            var parsed = ParseCombinationKey(combination.AttributesXml);

            if (parsed.Parts.Length == 0)
                continue;

            skeleton.Combinations.Add(new BulkOrderSkeletonCombination
            {
                CombinationId = combination.Id,
                Parts = [.. parsed.Parts.Select(part => new BulkOrderSkeletonKeyPart
                {
                    MappingId = part.MappingId,
                    ValueIds = [.. part.ValueIds]
                })]
            });

            foreach (var mappingId in parsed.MappingIds)
                signature.Add(mappingId);
        }

        skeleton.MappingIdsInAnySignature = [.. signature];

        return skeleton;
    }

    /// <summary>
    /// Turns a cached skeleton back into the GridData the pure part expects. No service call derives
    /// anything here; the two reads are nop's own cached accessors.
    /// </summary>
    /// <remarks>
    /// Every current mapping gets an entry in Values and PreSelectedIds even when the skeleton has none for
    /// it - BuildAsync indexes those dictionaries directly, so a mapping added since the skeleton was built
    /// would otherwise throw KeyNotFoundException rather than render. The consumers make that window
    /// vanishingly small; this makes it harmless.
    /// </remarks>
    protected virtual async Task<GridData> HydrateAsync(Product product, BulkOrderSkeleton skeleton)
    {
        var data = new GridData
        {
            Product = product,
            Mappings = await _productAttributeService.GetProductAttributeMappingsByProductIdAsync(product.Id),
            Combinations = await _productAttributeService.GetAllProductAttributeCombinationsAsync(product.Id),

            //ShoppingCartService.cs:496-517 - BOTH conditions, or nop sells the selection regardless of
            //whether a combination row exists. Anything less than both here makes the grid stricter than
            //the store it is a front end for. Read from the PRODUCT, never from the skeleton.
            RestrictsToExistingCombinations =
                product.ManageInventoryMethod == ManageInventoryMethod.ManageStockByAttributes
                && product.AllowAddingOnlyExistingAttributeCombinations,

            //not from the skeleton: a locale resource costs nothing to derive, and this read is a
            //cache hit either way - LoadAllLocaleRecordsOnStartup makes it a dictionary lookup
            NoneName = await _localizationService.GetResourceAsync("Plugins.CW.Core.BulkOrder.Grid.AxisNone")
        };

        var byMappingId = skeleton.Mappings.ToDictionary(entry => entry.MappingId);

        foreach (var mapping in data.Mappings)
        {
            if (!byMappingId.TryGetValue(mapping.Id, out var entry))
            {
                data.Names[mapping.Id] = string.Empty;
                data.Prompts[mapping.Id] = string.Empty;
                data.Values[mapping.Id] = [];
                data.PreSelectedIds[mapping.Id] = [];
                continue;
            }

            data.Names[mapping.Id] = entry.Name;
            data.Prompts[mapping.Id] = entry.Prompt;
            data.Values[mapping.Id] = [.. entry.Values];
            data.PreSelectedIds[mapping.Id] = [.. entry.PreSelectedIds];

            if (entry.Condition != null)
                data.Conditions[mapping.Id] = new AttributeCondition(mapping.Id,
                    entry.Condition.DependsOnMappingId,
                    [.. entry.Condition.RequiredValueIds],
                    entry.Condition.Satisfiable);
        }

        var combinationsById = data.Combinations.ToDictionary(combination => combination.Id);

        foreach (var entry in skeleton.Combinations)
        {
            //a combination deleted since the skeleton was built: skip it rather than index a null. The
            //consumer clears on delete, so this is the window between the two, not a steady state.
            if (!combinationsById.TryGetValue(entry.CombinationId, out var combination))
                continue;

            //FromNormalized, not Create: these parts came OUT of a CombinationKey when BuildSkeletonAsync
            //ran, so they are already merged, distinct and ordered, and Create's LINQ pipeline would sort
            //what is already sorted once per combination on every request. BuildSkeletonAsync is the only
            //writer of skeleton.Combinations, which is what makes that safe.
            var key = CombinationKey.FromNormalized(entry.Parts
                .Select(part => new CombinationKeyPart(part.MappingId, [.. part.ValueIds])));

            if (key.Parts.Length == 0)
                continue;

            //a duplicate key means two combination rows with the same selection - a data fault. nop's lookup
            //returns the FIRST match, so keep the first and ignore the rest rather than throwing.
            if (!data.Index.ContainsKey(key))
                data.Index[key] = combination;
        }

        foreach (var mappingId in skeleton.MappingIdsInAnySignature)
            data.MappingIdsInAnySignature.Add(mappingId);

        return data;
    }

    /// <summary>
    /// One pass over one combination's XML. Reads the same shape ProductAttributeParser does
    /// (//Attributes/ProductAttribute[@ID] -> ProductAttributeValue/Value) and swallows malformed XML the
    /// same way (:542) - one bad row must not take the page down.
    /// </summary>
    protected virtual CombinationKey ParseCombinationKey(string attributesXml)
    {
        if (string.IsNullOrEmpty(attributesXml))
            return CombinationKey.Create([]);

        var parts = new List<(int MappingId, IEnumerable<int> ValueIds)>();

        try
        {
            var document = new XmlDocument();
            document.LoadXml(attributesXml);

            foreach (XmlNode attributeNode in document.SelectNodes(@"//Attributes/ProductAttribute"))
            {
                if (attributeNode.Attributes?["ID"] == null)
                    continue;

                if (!int.TryParse(attributeNode.Attributes["ID"].InnerText.Trim(), out var mappingId))
                    continue;

                var valueIds = new List<int>();

                foreach (XmlNode valueNode in attributeNode.SelectNodes(@"ProductAttributeValue/Value"))
                {
                    if (int.TryParse(valueNode.InnerText.Trim(), out var valueId))
                        valueIds.Add(valueId);
                }

                if (valueIds.Count > 0)
                    parts.Add((mappingId, valueIds));
            }
        }
        catch
        {
            //ignore - same as ProductAttributeParser.ParseValues (:542)
        }

        return CombinationKey.Create(parts);
    }

    /// <summary>
    /// Parses one ConditionAttributeXml into an AttributeCondition, or null for "no condition". Single pass
    /// over the string, same approach and the same justification as ParseCombinationKey:
    /// IProductAttributeParser.ParseValues loads a fresh XmlDocument per call (ProductAttributeParser.cs:518).
    /// </summary>
    /// <remarks>
    /// Mirrors IsConditionMetAsync's resolution exactly (ProductAttributeParser.cs:724-745). Two points that
    /// are easy to get wrong and are the reason this is not a one-liner:
    ///
    ///  - nop takes the first element whose id LOADS, not the first element. It walks every ProductAttribute
    ///    element and skips ids that no longer resolve.
    ///  - when NO id resolves, nop returns true - the attribute shows, unconditionally. That is a live state,
    ///    not corruption: DeleteProductAttributeMappingAsync (ProductAttributeService.cs:183) is a bare row
    ///    delete and nothing scrubs the sibling conditions that referenced it.
    ///
    /// "Resolves" here means "is one of this product's mappings". The condition editor only ever offers this
    /// product's other mappings (ProductModelFactory.cs:322-327), so an id outside that set is a deleted
    /// attribute, which is precisely the case nop answers true for. Returning null for it records "no
    /// condition", which renders identically and carries one less state.
    /// </remarks>
    protected virtual AttributeCondition? ParseCondition(int mappingId, string conditionXml, ISet<int> productMappingIds)
    {
        if (string.IsNullOrWhiteSpace(conditionXml))
            return null;

        var cursor = 0;

        while (true)
        {
            var elementStart = conditionXml.IndexOf("<ProductAttribute ", cursor, StringComparison.OrdinalIgnoreCase);
            if (elementStart < 0)
                return null;

            var idStart = conditionXml.IndexOf("ID=\"", elementStart, StringComparison.OrdinalIgnoreCase);
            if (idStart < 0)
                return null;

            idStart += 4;
            var idEnd = conditionXml.IndexOf('"', idStart);
            if (idEnd < 0)
                return null;

            //this element's values end where the next element begins
            var elementEnd = conditionXml.IndexOf("<ProductAttribute ", idEnd, StringComparison.OrdinalIgnoreCase);
            if (elementEnd < 0)
                elementEnd = conditionXml.Length;

            cursor = elementEnd;

            //an id that is not one of this product's mappings is a DELETED attribute - skip the element, as
            //nop's FirstOrDefault over loaded mappings does
            if (!int.TryParse(conditionXml[idStart..idEnd], out var dependsOn) || !productMappingIds.Contains(dependsOn))
                continue;

            var required = ImmutableArray.CreateBuilder<int>();
            var satisfiable = true;
            var valueCursor = idEnd;

            while (true)
            {
                var open = conditionXml.IndexOf("<Value>", valueCursor, StringComparison.OrdinalIgnoreCase);
                if (open < 0 || open >= elementEnd)
                    break;

                open += 7;
                var close = conditionXml.IndexOf("</Value>", open, StringComparison.OrdinalIgnoreCase);
                if (close < 0 || close > elementEnd)
                    break;

                var raw = conditionXml[open..close];
                valueCursor = close + 8;

                //nop discards EMPTY entries and keeps everything else. An empty list is a real condition -
                //"A holds nothing" - so this is not the same as having no condition.
                if (raw.Length == 0)
                    continue;

                if (int.TryParse(raw, out var valueId))
                    required.Add(valueId);
                else
                    satisfiable = false;
            }

            return new AttributeCondition(mappingId, dependsOn, required.ToImmutable(), satisfiable);
        }
    }

    /// <summary>
    /// Which of the depended-on AXIS attribute's values satisfy the condition. A cell
    /// contributes exactly one value for an axis and nop compares the required set by count first, so only a
    /// one-value condition can be met by a real value - and only an EMPTY-required condition (admin's "nothing
    /// selected") by the optional axis's "(none)" value, reported as id 0. A set rather
    /// than an int because the per-cell test in Compose reads as a membership test and costs nothing.
    /// </summary>
    protected virtual ISet<int> SatisfyingAxisValueIds(GridData data, AttributeCondition condition)
    {
        var satisfying = new HashSet<int>();

        if (!data.Values.TryGetValue(condition.DependsOnMappingId, out var values))
            return satisfying;

        foreach (var value in values)
            if (condition.IsMetBy(new[] { value.Id }))
                satisfying.Add(value.Id);

        var dependsOn = data.Mappings.FirstOrDefault(mapping => mapping.Id == condition.DependsOnMappingId);

        if (dependsOn != null && !dependsOn.IsRequired && condition.IsMetBy(Array.Empty<int>()))
            satisfying.Add(0);

        return satisfying;
    }

    /// <summary>
    /// What the customer sees this attribute called: its TextPrompt when the admin set one, its name
    /// otherwise — the same fallback _PinnedStrip.cshtml:20 renders.
    /// </summary>
    /// <remarks>
    /// A notice that names the attribute differently from the control it is about sends the customer looking
    /// for something that is not on the card. Axis names are deliberately NOT routed through this: the grid
    /// header renders data.Names, so the demoted-axis notice has to match that instead.
    /// </remarks>
    protected virtual string LabelOf(GridData data, int mappingId) =>
        !string.IsNullOrEmpty(data.Prompts[mappingId]) ? data.Prompts[mappingId] : data.Names[mappingId];

    #endregion

    #region Utilities - classification

    /// <summary>A mapping that cannot be laid out on an axis.</summary>
    protected virtual bool IsPinOnly(GridData data, ProductAttributeMapping mapping)
    {
        //1. no discrete values to lay out (ProductAttributeExtensions.cs:75)
        if (mapping.IsNonCombinable())
            return true;

        //2. a cell is ONE value; a checkbox selection is a SET. This is what keeps a 20-checkbox attribute
        //   out of the grid instead of exploding it into 2^20 cells.
        if (mapping.AttributeControlType is AttributeControlType.Checkboxes or AttributeControlType.ReadonlyCheckboxes)
            return true;

        //3. the product RESTRICTS to existing combinations, and this mapping appears in no signature - on an
        //   axis every cell's lookup would miss, and on a restricted product a miss is a dash. On an
        //   unrestricted product a miss costs nothing: Compose looks up with the signature's own mappings and
        //   drops the rest, so the attribute can be laid out even though it appears in no signature.
        //   Skipped when there are no combinations at all, otherwise everything would be pin-only and
        //   no attribute could ever reach an axis.
        if (data.RestrictsToExistingCombinations && data.Index.Count > 0
            && !data.MappingIdsInAnySignature.Contains(mapping.Id))
            return true;

        //4. nothing to render
        return data.Values[mapping.Id].Count == 0;
    }

    /// <summary>
    /// Whether this product cannot be ordered from a grid of quantity boxes at all - because
    /// add-to-cart needs data the grid has nowhere to collect, or because nop refuses the product outright and
    /// its own catalogue answers with a link rather than a greyed control. Ordered so the answer names the most
    /// concrete obstacle first when a product has several.
    /// </summary>
    /// <remarks>
    /// Downloadable products and products with RequireOtherProducts are absent on purpose: neither needs
    /// anything at add-to-cart. A download's user agreement is accepted at checkout, and nop adds required
    /// products itself (ShoppingCartService.cs:224-246).
    /// Grouped and BuyDisabled belong here, not in the per-cell refusal rule: that rule answers "will
    /// nop sell this variant?", and for these two the answer is about the whole product, with a page that can
    /// say more than a box can.
    /// </remarks>
    protected virtual BulkOrderUnsupportedReason UnsupportedReasonFor(Product product)
    {
        //a grouped product is a list of other products; nop's cart refuses it (:368) and nop's catalogue
        //links to the page that lists the children
        if (product.ProductType != ProductType.SimpleProduct)
            return BulkOrderUnsupportedReason.Grouped;

        if (product.IsGiftCard)
            return BulkOrderUnsupportedReason.GiftCard;

        if (product.IsRental)
            return BulkOrderUnsupportedReason.Rental;

        if (product.CustomerEntersPrice)
            return BulkOrderUnsupportedReason.CustomerEntersPrice;

        if (product.IsRecurring)
            return BulkOrderUnsupportedReason.Recurring;

        //nop's cart refuses it (:353); nop's catalogue still shows it, without a button
        if (product.DisableBuyButton)
            return BulkOrderUnsupportedReason.BuyDisabled;

        return BulkOrderUnsupportedReason.None;
    }

    /// <summary>
    /// Configured attributes first in configured order, then the rest by DisplayOrder, Id.
    /// </summary>
    /// <remarks>
    /// BulkOrderAxisAttributeIds holds <b>ProductAttribute</b> ids (that is what the admin picker lists), so
    /// the match is on mapping.ProductAttributeId. Matching on mapping.Id compiles and never matches.
    /// A configured attribute the product does not have is skipped: it consumes no slot and is not pinned,
    /// because it does not exist for that product.
    /// </remarks>
    protected virtual IList<ProductAttributeMapping> OrderEligible(GridData data, IList<int> axisAttributeIds)
    {
        var eligible = data.Mappings.Where(mapping => !IsPinOnly(data, mapping)).ToList();

        var configured = (axisAttributeIds ?? [])
            .Select(attributeId => eligible.FirstOrDefault(mapping => mapping.ProductAttributeId == attributeId))
            .Where(mapping => mapping != null)
            .ToList();

        var rest = eligible
            .Where(mapping => !configured.Contains(mapping))
            .OrderBy(mapping => mapping.DisplayOrder)
            .ThenBy(mapping => mapping.Id);

        return [.. configured, .. rest];
    }

    /// <summary>
    /// Extended for set-valued attributes. Incoming selections win; a required pin falls back to
    /// a default so the grid renders.
    /// </summary>
    protected virtual IList<int> ResolvePinValues(GridData data, ProductAttributeMapping mapping,
        IDictionary<int, IList<int>> pinSelections, bool forceValue = false)
    {
        var values = data.Values[mapping.Id];

        if (values.Count == 0)
            return [];

        //ReadonlyCheckboxes: nop accepts EXACTLY the IsPreSelected ids and refuses anything else with
        //"You cannot change read-only values" (ShoppingCartService.cs:911). CommonHelper.ArraysEqual compares
        //length AND order, so a superset fails just as surely as a wrong value - submitting every value, as
        //this did, makes every cell of such a product un-addable the moment one value is not preselected.
        //PreSelectedIds is built from GetProductAttributeValuesAsync's own order (LoadAsync), which is the
        //order nop builds its allowed array in, so the comparison lines up.
        //Incoming selections are ignored rather than trusted: the customer cannot change these.
        if (mapping.AttributeControlType == AttributeControlType.ReadonlyCheckboxes)
            return [.. data.PreSelectedIds[mapping.Id]];

        var isMultiSelect = mapping.AttributeControlType == AttributeControlType.Checkboxes;

        if (pinSelections != null && pinSelections.TryGetValue(mapping.Id, out var chosen))
        {
            //the entry exists, so the customer has an opinion - including "none", which is respected
            var valid = chosen.Where(id => values.Any(value => value.Id == id)).Distinct().ToList();

            return isMultiSelect ? valid : [.. valid.Take(1)];
        }

        //optional pins default to nothing selected - that is a valid signature. EXCEPT when the
        //cell budget demoted this attribute off an axis: it appears in every combination's signature, so
        //leaving it out of the key matches nothing and the grid renders empty.
        if (!mapping.IsRequired && !forceValue)
            return [];

        var preSelected = values.Where(value => data.PreSelectedIds[mapping.Id].Contains(value.Id)).ToList();

        if (preSelected.Count > 0)
            return isMultiSelect ? [.. preSelected.Select(value => value.Id)] : [preSelected[0].Id];

        //Default only where nop's own control would. A required DropdownList with
        //no pre-selected value renders with its first <option> selected because the browser does that -
        //_ProductAttributes.cshtml adds the "---" option only when !IsRequired. A RadioList, ColorSquares,
        //ImageSquares or Checkboxes renders with NOTHING chosen and the cart refuses the line until the
        //customer picks (:869). Inventing a first value for those was a manufacturing instruction the customer
        //never gave - the same fault the demoted carve-out above was conditioned to avoid. Holding nothing
        //here is what BuildAsync turns into "Choose X above" on the cells.
        //...EXCEPT under forceValue, which is the demotion carve-out and not a default at all: a demoted OPTIONAL
        //pin on a restricted product appears in every combination's signature, so holding nothing there makes
        //the target shorter than any real combination and the grid renders EMPTY. The rule above rescues a
        //required pin with "Choose X above"; an optional one has no such notice, so it still gets a value.
        return forceValue || mapping.AttributeControlType == AttributeControlType.DropdownList
            ? [values[0].Id]
            : [];
    }

    #endregion

    #region Utilities - construction

    /// <summary>The key for one cell: its axis values plus every pinned value that has one.</summary>
    protected virtual CombinationKey CellKey(IList<(int MappingId, int ValueId)> axisSelections,
    IList<(int MappingId, IList<int> ValueIds)> pinSelections)
    {
        return CombinationKey.Create(axisSelections
            .Select(selection => (selection.MappingId, (IEnumerable<int>)[selection.ValueId]))
            .Concat(pinSelections.Select(pin => (pin.MappingId, (IEnumerable<int>)pin.ValueIds))));
    }

    /// <summary>
    /// The synthetic "(none)" value of an OPTIONAL axis: id 0, which contributes no part to any
    /// key, so a cell under it submits nothing for that attribute and looks up the shorter signature - exactly
    /// what nop's product page does when its "---" option is left selected. Without it the grid cannot
    /// express "no Fit", which is a purchasable option, on any product: every cell under that axis would
    /// have to name a value the customer never chose.
    /// </summary>
    protected virtual BulkOrderAxisValue NoneValue(GridData data)
        => new() { Id = 0, Name = data.NoneName, DisplayOrder = int.MaxValue };

    /// <summary>
    /// Cells the cross-product would produce, without building it.
    /// </summary>
    /// <remarks>
    /// long, not int: five axes of forty values overflows int, and an overflowed count compares as negative
    /// and passes the budget — which is the failure this exists to prevent.
    /// </remarks>
    protected virtual long ProjectedCrossProductCells(GridData data, IList<ProductAttributeMapping> axes)
    {
        var cells = 1L;

        foreach (var axis in axes)
        {
            //an optional axis carries its "(none)" value as well
            var values = data.Values[axis.Id].Count + (axis.IsRequired ? 0 : 1);

            cells *= Math.Max(values, 1);
        }

        return cells;
    }

    /// <summary>
    /// Builds columns, rows and cells for one axis/pin split. Pure - this is what the budget loop re-runs.
    /// </summary>
    /// <param name="data">Loaded product data</param>
    /// <param name="axes">Attributes on the axes, axis 1 first</param>
    /// <param name="signaturePins">Pins that take part in combination matching</param>
    /// <param name="allPins">Every pin holding a value - what a cell submits</param>
    /// <param name="conditionalPins">Pins conditional on an axis: mapping id -> the axis values that satisfy the condition</param>
    /// <param name="textBlockedPins">
    /// Per cell: REQUIRED pins with no values whose condition depends on an axis, same shape as
    /// conditionalPins. A cell whose axis value satisfies one of these takes no quantity.
    /// </param>
    /// <param name="choiceBlockedPins">
    /// Per cell: REQUIRED pins holding nothing. A null set means every cell; a set means
    /// the cells whose axis value satisfies the pin's condition. Those cells render blocked, saying what to
    /// choose, and take no quantity until it is chosen.
    /// </param>
    protected virtual BulkOrderGrid Compose(GridData data,
        IList<ProductAttributeMapping> axes,
        IList<(int MappingId, IList<int> ValueIds)> signaturePins,
        IList<(int MappingId, IList<int> ValueIds)> allPins,
        IDictionary<int, ISet<int>> conditionalPins,
        IDictionary<int, ISet<int>> textBlockedPins,
        IDictionary<int, ISet<int>> choiceBlockedPins)
    {
        //a missing combination closes a cell only where nop would refuse the sale. Everywhere
        //else the combination is extra information (Sku, and stock at ManageStockByAttributes), never a gate.
        var restricts = data.RestrictsToExistingCombinations;

        var axisValueIdsInUse = new Dictionary<int, HashSet<int>>();

        foreach (var axis in axes)
            axisValueIdsInUse[axis.Id] = [];

        if (restricts)
        {
            //the target signature is the axes plus the pins that participate in signatures.
            //only values that actually appear get a column or a row, so an all-dash line never
            //renders. Unrestricted products skip this scan entirely: every value is shown, exactly as
            //nop's own product page shows it, so there is nothing to compute.
            //
            //A condition on an AXIS makes the target signature VARY BY AXIS VALUE, so this is not one set. A
            //conditional pin belongs to a combination's signature only where the depended-on axis value
            //satisfies the condition - exactly how GenerateAllCombinationsAsync builds them
            //(ProductAttributeParser.cs:872-884). Against one fixed target that includes the pin, every
            //NON-satisfying axis value matches nothing and loses its column outright: not a dash, a missing
            //column, on cells the store would sell.
            var fixedPins = signaturePins
                .Where(pin => !conditionalPins.ContainsKey(pin.MappingId))
                .ToList();

            var conditionalSignaturePins = signaturePins
                .Where(pin => conditionalPins.ContainsKey(pin.MappingId))
                .ToList();

            //what a matching signature MUST carry: the required axes and the pins holding a value
            var baseTarget = axes.Where(axis => axis.IsRequired).Select(axis => axis.Id)
                .Concat(fixedPins.Select(pin => pin.MappingId))
                .ToHashSet();

            //what it MAY carry or omit: an OPTIONAL axis (a combination that omits it is the one its
            //"(none)" value reaches), and a REQUIRED pin the customer has not chosen yet - its
            //cells render blocked whatever the combinations say, so for the scan any value of it is accepted
            var flexible = axes.Where(axis => !axis.IsRequired).Select(axis => axis.Id)
                .Concat(choiceBlockedPins.Keys.Where(data.MappingIdsInAnySignature.Contains))
                .ToHashSet();

            //the signature a key must carry, given the axis values that key itself names
            ISet<int> TargetFor(CombinationKey key)
            {
                //no conditional pin in the signature: one target for the whole grid, allocated once. This is
                //the case for almost every product, so the common path costs one allocation.
                if (conditionalSignaturePins.Count == 0)
                    return baseTarget;

                var target = new HashSet<int>(baseTarget);

                foreach (var pin in conditionalSignaturePins)
                {
                    var dependsOn = data.Conditions[pin.MappingId].DependsOnMappingId;
                    var satisfying = conditionalPins[pin.MappingId];
                    var named = false;

                    foreach (var part in key.Parts)
                    {
                        if (part.MappingId != dependsOn)
                            continue;

                        named = true;

                        //a cell offers exactly ONE value for an axis, so a key naming several for the
                        //depended-on attribute is not a key any cell can produce - leave the pin out
                        if (part.ValueIds.Length == 1 && satisfying.Contains(part.ValueIds[0]))
                            target.Add(pin.MappingId);

                        break;
                    }

                    //no value for A at all is A's "(none)", which an empty-required condition is met by
                    if (!named && satisfying.Contains(0))
                        target.Add(pin.MappingId);
                }

                return target;
            }

            //set equality on the target, with the flexible mappings free to appear or not. Written as a loop
            //for the same reason CombinationKey.HasSignature is: once per combination per rebuild, and a
            //candidate that fails must not allocate.
            bool Matches(CombinationKey key)
            {
                var target = TargetFor(key);
                var carried = 0;

                foreach (var part in key.Parts)
                {
                    if (target.Contains(part.MappingId))
                        carried++;
                    else if (!flexible.Contains(part.MappingId))
                        return false;
                }

                return carried == target.Count;
            }

            foreach (var entry in data.Index.Where(entry => Matches(entry.Key)))
            {
                foreach (var axis in axes)
                {
                    var named = false;

                    foreach (var part in entry.Key.Parts)
                    {
                        if (part.MappingId != axis.Id)
                            continue;

                        named = true;

                        foreach (var valueId in part.ValueIds)
                            axisValueIdsInUse[axis.Id].Add(valueId);

                        break;
                    }

                    //only an optional axis can be absent from a match (Matches), and its absence is what
                    //makes "(none)" a real column or row here rather than a dash
                    if (!named)
                        axisValueIdsInUse[axis.Id].Add(0);
                }
            }
        }

        IList<BulkOrderAxisValue> ValuesOf(ProductAttributeMapping axis)
        {
            IEnumerable<BulkOrderAxisValue> values = data.Values[axis.Id];

            if (restricts)
                values = values.Where(value => axisValueIdsInUse[axis.Id].Contains(value.Id));

            //an optional axis ends with "(none)". Unrestricted: always, as nop's product page
            //always offers "---". Restricted: only when a matching combination omits the attribute, exactly
            //as any other value earns its column.
            if (!axis.IsRequired && (!restricts || axisValueIdsInUse[axis.Id].Contains(0)))
                values = values.Append(NoneValue(data));

            return [.. values];
        }

        var columns = axes.Count > 0 ? ValuesOf(axes[0]) : [];
        var rowAxes = axes.Skip(1).ToList();

        //the row tuples: the cross-product of the remaining axes' values, in axis order
        IList<IList<BulkOrderAxisValue>> tuples = [[]];

        foreach (var axis in rowAxes)
        {
            var next = new List<IList<BulkOrderAxisValue>>();

            foreach (var tuple in tuples)
                foreach (var value in ValuesOf(axis))
                    next.Add([.. tuple, value]);

            tuples = next;
        }

        var rows = new List<BulkOrderRow>();

        foreach (var tuple in tuples)
        {
            var cells = new List<BulkOrderCell>();

            //a product whose attributes are ALL pin-only still gets exactly one cell. Iterating
            //`columns` alone produced a row with no cells at all, so nothing existed for the refusal rule,
            //price or the stock message to attach to, and the view improvised a bare quantity box instead.
            //A null column means "this cell has no first axis", which is the only thing that distinguishes it.
            IList<BulkOrderAxisValue> cellColumns = axes.Count > 0 ? columns : [null];

            foreach (var column in cellColumns)
            {
                //may contain a ValueId of 0: the "(none)" value of an optional axis
                var axisSelections = new List<(int MappingId, int ValueId)>();

                if (column != null)
                    axisSelections.Add((axes[0].Id, column.Id));

                for (var i = 0; i < rowAxes.Count; i++)
                    axisSelections.Add((rowAxes[i].Id, tuple[i].Id));

                //Does a condition that depends on an AXIS hold for THIS cell? One test, three consumers - what
                //the cell submits and looks up, whether it can be ordered from this screen at all, and whether
                //it is waiting on a required choice. Written once because they must never disagree: a cell
                //that submits an attribute while also saying it cannot collect it is incoherent, and a cell
                //that refuses to take a quantity for an attribute it does not even submit is refusing for no
                //reason. "(none)" is a value like any other here: id 0, satisfying an empty-required condition.
                bool SatisfiedHere(int pinMappingId, ISet<int> satisfying)
                {
                    var dependsOn = data.Conditions[pinMappingId].DependsOnMappingId;
                    var axisValue = axisSelections.FirstOrDefault(sel => sel.MappingId == dependsOn);

                    return axisValue.MappingId != 0 && satisfying.Contains(axisValue.ValueId);
                }

                //a conditional pin reaches only the cells whose axis value satisfies it.
                //Filtering HERE rather than when the pin list is built is the whole point - the answer differs
                //per cell. It matters twice over:
                //  - what the cell submits: on a non-matching line nop's own product page would never have
                //    offered the attribute, so putting it there is the wrong manufacturing instruction and the
                //    wrong price (the cart does not warn about it - ShoppingCartService.cs:858-865 only DROPS
                //    unmet-condition attributes before the required check, it never rejects one);
                //  - what the cell looks up: GenerateAllCombinationsAsync strips unmet-condition attributes
                //    from the combinations it generates (ProductAttributeParser.cs:872-884), so a restricted
                //    product's combinations have exactly this mixed shape. An unfiltered key would miss them
                //    and dash cells the store would sell.
                var cellSignaturePins = signaturePins;
                var cellAllPins = allPins;

                if (conditionalPins.Count > 0)
                {
                    bool applies((int MappingId, IList<int> ValueIds) pin) =>
                        !conditionalPins.TryGetValue(pin.MappingId, out var satisfying)
                        || SatisfiedHere(pin.MappingId, satisfying);

                    cellSignaturePins = [.. signaturePins.Where(applies)];
                    cellAllPins = [.. allPins.Where(applies)];
                }

                //A required attribute the grid has no input for blocks only the cells
                //whose axis value satisfies its condition, because those are the only lines nop asks for it -
                //the condition writes the value onto those cells and no others, and :857 strips it from the rest
                //BEFORE the required check at :869. The sibling cells are ordinary and must render.
                //
                //These pins are absent from `allPins` (that list is pins HOLDING a value, and this one holds
                //none), which is why they arrive as their own parameter rather than being found in it.
                var requiresText = textBlockedPins.Any(blocked => SatisfiedHere(blocked.Key, blocked.Value));

                //a REQUIRED pin the customer has not chosen. Null: every cell.
                //A set: the cells whose axis value satisfies its condition. Blocked, not refused.
                var requiresChoice = choiceBlockedPins.Any(blocked =>
                    blocked.Value == null || SatisfiedHere(blocked.Key, blocked.Value));

                //"(none)" contributes no part: what the cell SUBMITS is its real values only, so the
                //line nop receives is byte-for-byte what its product page sends with "---" left selected
                var submitted = axisSelections.Where(selection => selection.ValueId != 0).ToList();

                //what the cell LOOKS UP with: the submitted axis values the combinations actually mention. On
                //a restricted product every axis is in a signature (IsPinOnly rule 3), so this is `submitted`.
                //On an unrestricted one an axis the combinations never mention is dropped, so the Sku and the
                //stock of the Size x Colour combination are still found under a Fit column - which is what
                //lets Fit be an axis at all.
                var lookedUp = data.Index.Count == 0
                    ? submitted
                    : submitted.Where(selection => data.MappingIdsInAnySignature.Contains(selection.MappingId)).ToList();

                var lookupKey = CellKey(lookedUp, cellSignaturePins);
                var selectionKey = CellKey(submitted, cellAllPins);

                //the customer-facing identity of the cell: axes only, "(none)" INCLUDED - "M / Navy / none" is
                //a different cell from "M / Navy / Slim", and it must find its typed quantity again after a
                //pin change. CombinationKey keeps a part whose one value is 0.
                var axisKey = CellKey(axisSelections, []);

                ProductAttributeCombination combination = null;

                //the lookup runs for every product that HAS combinations, restricted or not - it is where Sku
                //comes from. Only the meaning of a miss depends on `restricts`.
                if (data.Index.Count > 0)
                    data.Index.TryGetValue(lookupKey, out combination);

                cells.Add(new BulkOrderCell
                {
                    Column = column,

                    //a cell awaiting a required choice cannot know whether its variant exists until the choice
                    //is made - the pin is not in its key. It renders, blocked, and is composed again when it is.
                    AllowedWithoutCombination = !restricts || requiresChoice,
                    ProductAttributeCombinationId = combination?.Id ?? 0,
                    Sku = combination?.Sku,
                    Selection = selectionKey.Parts,
                    AxisSelection = axisKey.Parts,
                    RequiresTextAttribute = requiresText,
                    RequiresPinChoice = requiresChoice
                });
            }

            //a tuple whose every cell is a dash is a row of nothing - drop it.
            //Only reachable when `restricts`: otherwise every cell exists.
            if (cells.Any(cell => cell.Exists))
                rows.Add(new BulkOrderRow { Labels = tuple, Cells = cells });
        }

        return new BulkOrderGrid
        {
            ProductId = data.Product.Id,
            RestrictsToExistingCombinations = restricts,
            HasCombinations = data.Index.Count > 0,
            Columns = columns,
            Rows = rows,
            Axes = [.. axes.Select(axis => new BulkOrderAxis
            {
                ProductAttributeMappingId = axis.Id,
                Name = data.Names[axis.Id],
                Values = ValuesOf(axis)
            })]
        };
    }

    #endregion

    #region Methods

    public virtual async Task<BulkOrderGrid> BuildAsync(Product product,
        IDictionary<int, IList<int>> pinSelections,
        IList<int> axisAttributeIds,
        int maxCells,
        bool showAttributeImages)
    {
        ArgumentNullException.ThrowIfNull(product);

        //a product whose add-to-cart needs data this screen has nowhere to collect is refused
        //here, before any work: no attributes are loaded, no combinations parsed, no cells composed. The
        //customer is sent to the product page, which already has the form and already validates it.
        var unsupported = UnsupportedReasonFor(product);

        if (unsupported != BulkOrderUnsupportedReason.None)
            return new BulkOrderGrid { ProductId = product.Id, UnsupportedReason = unsupported };

        var data = await LoadAsync(product, showAttributeImages);

        //no attributes at all: one quantity box, no axes, no pins. There is
        //nothing for a combination to key on, so the product cannot be restricted whatever its settings say -
        //which is why AllowedWithoutCombination is true here regardless of the product's own settings.
        //The ROW AND CELL are not decoration. This must take the same code path as a 5-axis garment, and
        //that is only true if there is a cell for the pricing and availability rules to be asked about. With
        //no cell, the refusal rule never runs and the view renders a bare box on a product nop would refuse.
        if (data.Mappings.Count == 0)
            return new BulkOrderGrid
            {
                ProductId = product.Id,
                Rows = [new BulkOrderRow { Cells = [new BulkOrderCell { AllowedWithoutCombination = true }] }]
            };

        var ordered = OrderEligible(data, axisAttributeIds);

        //blocked = cannot be an axis this attempt. FOUR sets, not one, because they mean different things to
        //the customer: the budget moved it (say so), a condition moved it (still selectable as a pin), a
        //condition BLOCKED it (rendered, disabled, with the reason), or a condition hid
        //it outright (not shown at all, because nothing the customer can do here would reveal it).
        var budgetDemoted = new HashSet<int>();
        var conditionDemoted = new HashSet<int>();
        var conditionBlocked = new HashSet<int>();
        var hidden = new HashSet<int>();

        var demotedNames = new List<string>();
        var budget = maxCells > 0 ? maxCells : int.MaxValue;

        //the carve-out - forcing a value onto a demoted optional pin - only earns its keep where a
        //key miss closes a cell. Unrestricted, forcing would put an attribute the customer never chose,
        //and its PriceAdjustment, onto every line.
        var forceDemotedPins = data.RestrictsToExistingCombinations;

        //A budget demotion changes the axis set, and every condition was resolved against the old one: a
        //condition demotion, hiding or block may all be wrong for the new axes. So they are
        //re-derived from nothing on the next pass. budgetDemoted only grows, which is what keeps the loop
        //finite - the condition sets converge for any fixed budgetDemoted.
        void DemoteLastAxis(IList<ProductAttributeMapping> axes)
        {
            budgetDemoted.Add(axes[^1].Id);
            demotedNames.Add(data.Names[axes[^1].Id]);

            conditionDemoted.Clear();
            conditionBlocked.Clear();
            hidden.Clear();
        }

        while (true)
        {
            var axes = ordered
                .Where(mapping => !budgetDemoted.Contains(mapping.Id)
                                  && !conditionDemoted.Contains(mapping.Id)
                                  && !conditionBlocked.Contains(mapping.Id)
                                  && !hidden.Contains(mapping.Id))
                .Take(BulkOrderDefaults.MAX_AXIS_COUNT)
                .ToList();

            var axisIds = axes.Select(axis => axis.Id).ToHashSet();

            //`hidden` alone is excluded here. A conditionBlocked mapping IS a pin - that is the whole point:
            //it renders, disabled, saying what would unlock it.
            var pinned = data.Mappings
                .Where(mapping => !axisIds.Contains(mapping.Id) && !hidden.Contains(mapping.Id))
                .OrderBy(mapping => mapping.DisplayOrder)
                .ThenBy(mapping => mapping.Id)
                .ToList();

            //Pin state is resolved ONCE per attempt; the signature, the submitted selection and the rendered
            //control all read it. Deriving them separately is how the grid and the strip drift apart.
            var pins = pinned
                .Select(mapping => new PinState
                {
                    Mapping = mapping,
                    Selected = ResolvePinValues(data, mapping, pinSelections,
                        forceDemotedPins && budgetDemoted.Contains(mapping.Id)),
                    Default = ResolvePinValues(data, mapping, null,
                        forceDemotedPins && budgetDemoted.Contains(mapping.Id))
                })
                .ToList();

            //---- Attribute conditions. Resolved here because some of them change the axis set ------------
            var restart = false;
            var conditionalPins = new Dictionary<int, ISet<int>>();   //pin mapping id -> satisfying A values

            //data.Mappings order, not dictionary order: with two conditions on one product the enumeration
            //order decides which attribute gets demoted, and that must not be an implementation detail
            foreach (var mapping in data.Mappings)
            {
                if (hidden.Contains(mapping.Id) || !data.Conditions.TryGetValue(mapping.Id, out var condition))
                    continue;

                //a chain whose upstream attribute is HIDDEN. A is not on the card at all, so "choose A first"
                //would name a control the customer cannot find. nop strips A, finds B's condition unmet, and
                //strips B too - so B is hidden with it.
                if (hidden.Contains(condition.DependsOnMappingId))
                {
                    hidden.Add(mapping.Id);
                    restart = true;
                    break;
                }

                var bIsAxis = axisIds.Contains(mapping.Id);
                var aIsAxis = axisIds.Contains(condition.DependsOnMappingId);

                if (bIsAxis && aIsAxis)
                {
                    //case 4: demote B, never A. A is what something else depends on and is typically the
                    //primary attribute - colour, size. Demoting A would cost every product that uses it.
                    //On the next attempt B is a pin and A is still an axis, so this resolves into case 3.
                    conditionDemoted.Add(mapping.Id);
                    restart = true;
                    break;
                }

                if (aIsAxis)
                {
                    //case 3: B is pinned, A is an axis. Show B when a rendered A value satisfies it; its
                    //value then reaches only those cells (Compose).
                    var satisfying = SatisfyingAxisValueIds(data, condition);

                    if (satisfying.Count > 0)
                    {
                        conditionalPins[mapping.Id] = satisfying;
                        continue;
                    }

                    //No A value can satisfy it - a two-value condition, typically, since an axis cell offers
                    //one value and nop compares by count first. HIDDEN, not blocked: nothing the customer can
                    //do on this card would reveal it, so a disabled control saying "choose A first" would be
                    //a lie. nop never shows B either, for the same reason.
                    hidden.Add(mapping.Id);
                    restart = true;
                    break;
                }

                //A is pinned, so the condition has one answer for the whole grid.
                //A pin whose OWN condition is unmet holds nothing, as far as anything depending on it is
                //concerned. Without that, a chain - C depends on B, B depends on A - lets C read the value B
                //is merely REMEMBERING while blocked, and C renders as a live control unlocked by a selection
                //that is not in effect. Remembering a blocked pin's value is deliberate - it is how re-ticking
                //restores what the customer chose - so the hole that opens is closed in the same place.
                IList<int> aSelected = conditionBlocked.Contains(condition.DependsOnMappingId)
                    ? []
                    : pins.FirstOrDefault(pin => pin.Mapping.Id == condition.DependsOnMappingId)?.Selected ?? [];

                if (condition.IsMetBy((IReadOnlyCollection<int>)aSelected))
                    continue;

                //Unmet. The attribute is not offered - and it is SHOWN, disabled, naming what unlocks it
                //rather than being hidden: unticking the attribute it depends on used to remove it from
                //the strip with no explanation at all. The value still
                //travels and is still ignored, so re-ticking brings the pin back at what the customer chose
                //rather than at its default.
                if (!conditionBlocked.Add(mapping.Id))
                    continue;                      //already known: nothing changed, so nothing to redo

                //ALWAYS restart, for two independent reasons. Case 2: this was an axis, so the axis set just
                //changed and another eligible attribute may take the freed slot. Both cases: anything
                //depending on THIS attribute may already have been evaluated earlier in this pass, against
                //the value it is now only remembering - `data.Mappings` order decides which, and that must
                //not decide the answer. The Add above is what terminates the loop: on the next pass it
                //returns false and nothing restarts.
                restart = true;
                break;
            }

            if (restart)
                continue;
            //-----------------------------------------------------------------------------------------------

            //what a cell submits: every pin holding a value, EXCEPT the ones whose condition is
            //unmet. This exclusion is what makes it safe to leave a blocked pin in
            //the DOM at all: nop strips unmet-condition attributes inside ParseProductAttributes, which this
            //screen never goes through, so nothing downstream would remove them for us.
            var allPins = pins
                .Where(pin => pin.Selected.Count > 0 && !conditionBlocked.Contains(pin.Mapping.Id))
                .Select(pin => (MappingId: pin.Mapping.Id, ValueIds: pin.Selected))
                .ToList();

            //The pins as the strip renders them. A local function because BOTH early returns below need it:
            //a product-level refusal that dropped its pins took the strip with it, leaving the customer
            //looking at a card whose cause - the checkbox they had just ticked - was no longer on screen.
            List<BulkOrderPin> BuildPins() =>
            [
                .. pins.Select(pin => new BulkOrderPin
                {
                    ProductAttributeMappingId = pin.Mapping.Id,
                    Name = data.Names[pin.Mapping.Id],
                    TextPrompt = data.Prompts[pin.Mapping.Id],
                    IsRequired = pin.Mapping.IsRequired,
                    AttributeControlTypeId = pin.Mapping.AttributeControlTypeId,
                    Values = data.Values[pin.Mapping.Id],
                    SelectedValueIds = pin.Selected,
                    DefaultValueIds = pin.Default,
                    WasDemoted = budgetDemoted.Contains(pin.Mapping.Id),
                    IsConditional = conditionalPins.ContainsKey(pin.Mapping.Id),
                    AppliesToValueName = conditionalPins.TryGetValue(pin.Mapping.Id, out var satisfying)
                        ? data.Values[data.Conditions[pin.Mapping.Id].DependsOnMappingId]
                              .FirstOrDefault(value => satisfying.Contains(value.Id))?.Name
                          ?? (satisfying.Contains(0) ? data.NoneName : null)
                        : null,
                    IsBlockedByCondition = conditionBlocked.Contains(pin.Mapping.Id),
                    DependsOnName = conditionBlocked.Contains(pin.Mapping.Id)
                        ? LabelOf(data, data.Conditions[pin.Mapping.Id].DependsOnMappingId)
                        : null
                })
            ];

            //a REQUIRED attribute the grid has no input for. WHERE it applies is not the same
            //question for every product, and asking it as one refused cells the store sells:
            //  - unconditional, or conditional with the condition MET by the pin state: it applies to every
            //    line of this card, so the product genuinely cannot be ordered here;
            //  - conditional on an AXIS: it applies only to the cells whose axis
            //    value satisfies it. Its siblings need nothing this screen cannot supply and nop sells them.
            //conditionBlocked is excluded and that exclusion is load-bearing: `pinned` no longer drops an
            //unmet pin, and nop drops the attribute before the required check when its condition is unmet
            //so counting one here would refuse a product nop would sell.
            //ReadonlyCheckboxes with nothing pre-selected belongs here too: nop accepts exactly the
            //pre-selected set (:911) AND demands a value (:869), and nothing satisfies both - on the product
            //page either. Refused the same way, with the same sentence.
            var missingRequired = pinned
                .Where(mapping => !conditionBlocked.Contains(mapping.Id) && mapping.IsRequired
                                  && (data.Values[mapping.Id].Count == 0
                                      || (mapping.AttributeControlType == AttributeControlType.ReadonlyCheckboxes
                                          && data.PreSelectedIds[mapping.Id].Count == 0)))
                .ToList();

            var blocksEveryCell = missingRequired
                .FirstOrDefault(mapping => !conditionalPins.ContainsKey(mapping.Id));

            if (blocksEveryCell != null)
                return new BulkOrderGrid
                {
                    ProductId = product.Id,
                    UnsupportedReason = BulkOrderUnsupportedReason.RequiredTextAttribute,
                    UnsupportedDetail = LabelOf(data, blocksEveryCell.Id),
                    Pins = BuildPins()
                };

            //ordered by DisplayOrder then Id, because `pinned` is: with two of them the name in the notice
            //must not depend on dictionary ordering
            var textBlocked = missingRequired
                .Where(mapping => conditionalPins.ContainsKey(mapping.Id))
                .ToList();

            var textBlockedPins = textBlocked
                .ToDictionary(mapping => mapping.Id, mapping => conditionalPins[mapping.Id]);

            //a REQUIRED pin holding NOTHING: no pre-selected value, and not a
            //DropdownList, the one control nop's own page defaults (ResolvePinValues). nop refuses every line
            //until the customer chooses (:869); the grid used to choose FOR them, which put a value the
            //customer never gave onto every line. Now the cells render blocked, saying what to choose -
            //recoverable on this screen, so it is a notice and not an unsupported-product card. Same
            //per-cell split as the text rule: a pin whose condition does not depend on an axis applies to
            //every cell (null), an axis-dependent one only to the cells whose axis value satisfies it.
            var missingChoice = pinned
                .Where(mapping => !conditionBlocked.Contains(mapping.Id) && mapping.IsRequired
                                  && !missingRequired.Contains(mapping)
                                  && data.Values[mapping.Id].Count > 0
                                  && pins.First(pin => pin.Mapping.Id == mapping.Id).Selected.Count == 0)
                .ToList();

            var choiceBlockedPins = missingChoice.ToDictionary(
                mapping => mapping.Id,
                mapping => conditionalPins.TryGetValue(mapping.Id, out var satisfying) ? satisfying : null);

            //what takes part in matching: only pins the combinations actually mention
            List<(int MappingId, IList<int> ValueIds)> signaturePins = data.Index.Count == 0
                ? allPins
                : [.. allPins.Where(pin => data.MappingIdsInAnySignature.Contains(pin.MappingId))];

            //an unrestricted grid IS the cross-product of every value, so its size is known from
            //the value counts alone. Check before composing: building the explosion to discover it is the
            //failure.
            if (!data.RestrictsToExistingCombinations && axes.Count > 1
                && ProjectedCrossProductCells(data, axes) > budget)
            {
                DemoteLastAxis(axes);
                continue;
            }

            var grid = Compose(data, axes, signaturePins, allPins, conditionalPins, textBlockedPins, choiceBlockedPins);
            var cells = grid.Rows.Count * grid.Columns.Count;

            //one axis alone over budget renders anyway: a single attribute with 500 values is a data problem,
            //not a layout problem
            if (cells > budget && axes.Count > 1)
            {
                DemoteLastAxis(axes);
                continue;
            }

            //Counted from the cells that were actually composed, and only the ones that COULD have taken a
            //quantity: a dash cell (no combination on a restricted product) was never orderable, so it
            //neither needs explaining nor proves the grid is worth rendering.
            var orderable = grid.Rows.SelectMany(row => row.Cells)
                .Where(cell => cell.Exists)
                .ToList();

            var textBlockedCells = orderable.Count(cell => cell.RequiresTextAttribute);

            //EVERY cell that could have been ordered needs a value this screen cannot collect, so the product
            //cannot be ordered here after all. Reached when the depended-on axis has a single value: every
            //cell carries it, so every cell satisfies the condition. A grid whose every box is refused is
            //worse than the unsupported-product card, which at least links to the page that can take the order.
            if (textBlockedCells > 0 && textBlockedCells == orderable.Count)
                return new BulkOrderGrid
                {
                    ProductId = product.Id,
                    UnsupportedReason = BulkOrderUnsupportedReason.RequiredTextAttribute,
                    UnsupportedDetail = LabelOf(data, textBlocked[0].Id),
                    Pins = BuildPins()
                };

            return grid with
            {
                ExceedsBudget = cells > budget,
                DemotedAxisNames = demotedNames,
                RequiredTextAttributeName = textBlockedCells > 0 ? LabelOf(data, textBlocked[0].Id) : null,
                RequiredChoiceAttributeName = missingChoice.Count > 0 ? LabelOf(data, missingChoice[0].Id) : null,
                Pins = BuildPins()
            };
        }
    }

    #endregion
}