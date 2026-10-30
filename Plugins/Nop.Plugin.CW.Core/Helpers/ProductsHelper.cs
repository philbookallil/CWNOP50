using Nop.Core.Domain.Catalog;
using Nop.Web.Models.Catalog;

namespace Nop.Plugin.CW.Core.Helpers
{
    public static class ProductsHelper
    {
        #region Utilities

        private static bool NameStartsWithVariantions(string name, IList<string> variations)
        {
            if (string.IsNullOrEmpty(name)) return false;
            if (variations == null || variations.Count == 0) return false;

            return variations.Any(s => name.ToLower().StartsWith(s));
        }

        private static bool NameContainsVariantions(string name, IList<string> variations)
        {
            if (string.IsNullOrEmpty(name)) return false;
            if (variations == null || variations.Count == 0) return false;
            return variations.Any(s => name.Contains(s, StringComparison.OrdinalIgnoreCase));
        }

        #endregion

        #region Colour

        /// <summary>
        /// A confident colour match: the name is spelled out in full.
        /// </summary>
        public static bool IsColourAttributeName(string name)
        {
            IList<string> colorNameVariations = new List<string>() { "color", "colour" };
            return NameStartsWithVariantions(name, colorNameVariations);
        }

        /// <summary>
        /// A loose, last-resort colour match, for a misspelled or truncated name — "Colur", "Colr", "Col".
        /// </summary>
        /// <remarks>
        /// Only ever consulted by <see cref="FindColourAttribute"/>, and only when no attribute matched
        /// the full spelling. On its own this prefix also catches <c>Collar</c>, <c>Collection</c> and
        /// <c>Column</c>, which is exactly why it must not be used as a standalone predicate: a product
        /// with a Collar attribute AND a real Colour attribute used to have the real one treated as a
        /// second colour, dropped from the picker, and — if required — the product refused.
        /// </remarks>
        public static bool IsLooseColourAttributeName(string name)
        {
            IList<string> looseColorNameVariations = new List<string>() { "col" };
            return NameStartsWithVariantions(name, looseColorNameVariations);
        }

        public static bool IsColourAttributeType(AttributeControlType controlType)
        {
            return controlType == AttributeControlType.ColorSquares;
        }

        public static bool IsColourAttribute(ProductDetailsModel.ProductAttributeModel attributeModel)
        {
            return IsColourAttributeType(attributeModel.AttributeControlType) || IsColourAttributeName(attributeModel.Name);
        }

        /// <remarks>
        /// Unused — kept only in case the mapping-level path is revived.
        /// If it ever is, give it the same two-pass treatment as <see cref="FindColourAttribute"/>; the
        /// loose "col" prefix must not be applied per attribute.
        /// </remarks>
        public static bool IsColourAttribute(ProductAttributeMapping attributeMapping, ProductAttribute productAttribute)
        {

            return IsColourAttributeType(attributeMapping.AttributeControlType) || IsColourAttributeName(productAttribute.Name);
        }

        /// <summary>
        /// The one attribute the replacement picker renders as the colour choice, or null.
        /// </summary>
        /// <remarks>
        /// THE selection rule, called by both _ReplacementProductAttrs.cshtml and
        /// <see cref="FindUncollectableRequiredAttributes"/> so the two cannot drift apart.
        /// <para>
        /// Two passes, deliberately. The full spelling — or a ColorSquares control, which is a colour
        /// picker whatever it is called — wins outright. Only when nothing matches that does the loose
        /// "col" prefix get a say, so a misspelled name is still found while <c>Collar</c> can never
        /// outrank a real <c>Colour</c> attribute sitting beside it.
        /// </para>
        /// Only an attribute with values can be rendered: one with nothing to pick from would produce a
        /// dropdown holding just "- Select -", which the customer could never satisfy.
        /// </remarks>
        public static ProductDetailsModel.ProductAttributeModel FindColourAttribute(
            IList<ProductDetailsModel.ProductAttributeModel> attributes)
        {
            if (attributes == null)
                return null;

            var renderable = attributes.Where(a => a.Values.Any()).ToList();

            return renderable.FirstOrDefault(IsColourAttribute)
                ?? renderable.FirstOrDefault(a => IsLooseColourAttributeName(a.Name));
        }

        #endregion

        #region Size

        public static bool IsSizeAttribute(ProductDetailsModel.ProductAttributeModel attributeModel)
        {
            IList<string> sizeNameVariations = new List<string>() { "size" };
            var nameSize = NameContainsVariantions(attributeModel.Name, sizeNameVariations);
            var textSize = NameContainsVariantions(attributeModel.TextPrompt, sizeNameVariations);
            return textSize || nameSize;
        }

        public static bool IsSizeAttribute(ProductAttributeMapping attributeMapping, ProductAttribute productAttribute)
        {
            IList<string> sizeNameVariations = new List<string>() { "size" };
            var nameSize = NameContainsVariantions(productAttribute.Name, sizeNameVariations);
            var textSize = NameContainsVariantions(attributeMapping.TextPrompt, sizeNameVariations);
            return textSize || nameSize;
            //return IsSizeAttributeName(attributeMapping.ProductAttribute.Name);
        }

        #endregion

        #region The picker's mirror

        /// <summary>
        /// The options this product requires that the replacement picker has no way to collect.
        /// </summary>
        /// <remarks>
        /// This MUST mirror what _ReplacementProductAttrs.cshtml renders: the colour attribute chosen by
        /// <see cref="FindColourAttribute"/>, plus every other attribute that has values to pick from.
        /// Both now call that same method, so the colour half cannot drift; what still has to be kept in
        /// step by hand is the "everything else with values" half.
        ///
        /// Anything not rendered has no field on screen, so when it is required the replacement would be
        /// refused by cart validation after approval and the exchange could never be completed.
        ///
        /// Attributes with a condition are left out on purpose: nop only enforces one when its
        /// controlling attribute is set, and the picker does not evaluate conditions, so treating them as
        /// blocking would refuse products that are in fact orderable.
        /// </remarks>
        public static IList<string> FindUncollectableRequiredAttributes(ProductDetailsModel productDetails)
        {
            var uncollectable = new List<string>();

            var attributes = productDetails?.ProductAttributes;

            if (attributes == null)
                return uncollectable;

            var renderedColour = FindColourAttribute(attributes);

            foreach (var attribute in attributes)
            {
                //rendered as the colour dropdown
                if (ReferenceEquals(attribute, renderedColour))
                    continue;

                //rendered as one of the other dropdowns
                if (attribute.Values.Any())
                    continue;

                if (attribute.IsRequired && !attribute.HasCondition)
                    uncollectable.Add(attribute.Name);
            }

            return uncollectable;
        }

        #endregion
    }
}