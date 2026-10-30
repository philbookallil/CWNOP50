using AwesomeAssertions;
using Nop.Core.Domain.Catalog;
using Nop.Plugin.CW.Core.Helpers;
using Nop.Web.Models.Catalog;
using NUnit.Framework;

namespace CW.Tests.Core.Helpers;

/// <summary>
/// The rule deciding which of a replacement product's required options the exchange picker cannot collect
/// (fix 21, phase 4b-4).
/// </summary>
/// <remarks>
/// This exists to be pinned. FindUncollectableRequiredAttributes is correct only while it MIRRORS what
/// _ReplacementProductAttrs.cshtml renders — the first colour attribute that has values, plus every
/// non-colour attribute that has values. If the view starts rendering something the helper still counts
/// as uncollectable, the picker refuses a product it can in fact specify; if the view stops rendering
/// something the helper thinks is fine, the customer gets an exchange that can never be completed.
/// Nothing else enforces that pairing, so these tests are it.
/// </remarks>
[TestFixture]
public class ProductsHelperTests
{
    #region Utilities

    private static ProductDetailsModel.ProductAttributeValueModel Value(int id, string name)
        => new() { Id = id, Name = name };

    private static ProductDetailsModel.ProductAttributeModel Attr(
        string name,
        bool isRequired = false,
        bool hasCondition = false,
        AttributeControlType controlType = AttributeControlType.DropdownList,
        params ProductDetailsModel.ProductAttributeValueModel[] values)
    {
        var attribute = new ProductDetailsModel.ProductAttributeModel
        {
            Name = name,
            IsRequired = isRequired,
            HasCondition = hasCondition,
            AttributeControlType = controlType
        };

        foreach (var value in values)
            attribute.Values.Add(value);

        return attribute;
    }

    private static ProductDetailsModel Product(params ProductDetailsModel.ProductAttributeModel[] attributes)
    {
        var product = new ProductDetailsModel();

        foreach (var attribute in attributes)
            product.ProductAttributes.Add(attribute);

        return product;
    }

    private static ProductDetailsModel.ProductAttributeModel Colour(bool isRequired = false, bool withValues = true)
        => withValues
            ? Attr("Colour", isRequired, false, AttributeControlType.ColorSquares, Value(1, "Red"), Value(2, "Blue"))
            : Attr("Colour", isRequired, false, AttributeControlType.ColorSquares);

    private static ProductDetailsModel.ProductAttributeModel Size(bool isRequired = true)
        => Attr("Size", isRequired, false, AttributeControlType.DropdownList, Value(10, "S"), Value(11, "M"));

    /// <summary>A free-text attribute: no values to pick from, so the picker cannot render it.</summary>
    private static ProductDetailsModel.ProductAttributeModel FreeText(string name, bool isRequired, bool hasCondition = false)
        => Attr(name, isRequired, hasCondition, AttributeControlType.TextBox);

    #endregion

    #region Nothing to report

    [Test]
    public void FindUncollectableRequiredAttributes_ColourAndSize_AreBothCollectable()
    {
        ProductsHelper.FindUncollectableRequiredAttributes(Product(Colour(true), Size()))
            .Should().BeEmpty();
    }

    [Test]
    public void FindUncollectableRequiredAttributes_ProductWithNoAttributes_ReportsNothing()
    {
        ProductsHelper.FindUncollectableRequiredAttributes(Product()).Should().BeEmpty();
    }

    [Test]
    public void FindUncollectableRequiredAttributes_NullProduct_ReportsNothing()
    {
        ProductsHelper.FindUncollectableRequiredAttributes(null).Should().BeEmpty();
    }

    #endregion

    #region The case fix 21 was built for

    /// <summary>
    /// nop's sample Custom T-Shirt: one required free-text attribute the picker has no field for. This is
    /// the product that shipped as order 122 with no attribute description at all before fix 20 and 21.
    /// </summary>
    [Test]
    public void FindUncollectableRequiredAttributes_RequiredFreeText_IsReported()
    {
        ProductsHelper.FindUncollectableRequiredAttributes(Product(FreeText("Enter your text", isRequired: true)))
            .Should().ContainSingle().Which.Should().Be("Enter your text");
    }

    /// <summary>
    /// An optional one is not a blocker: cart validation accepts the product without it, so refusing the
    /// exchange would deny a replacement the customer could legitimately receive.
    /// </summary>
    [Test]
    public void FindUncollectableRequiredAttributes_OptionalFreeText_IsNotReported()
    {
        ProductsHelper.FindUncollectableRequiredAttributes(Product(FreeText("Gift message", isRequired: false)))
            .Should().BeEmpty();
    }

    /// <summary>
    /// Deliberate: nop enforces a conditional attribute only when its controlling attribute is set, and
    /// the picker does not evaluate conditions, so treating one as blocking would refuse products that
    /// are in fact orderable.
    /// </summary>
    [Test]
    public void FindUncollectableRequiredAttributes_RequiredButConditional_IsDeliberatelyNotReported()
    {
        ProductsHelper.FindUncollectableRequiredAttributes(
                Product(Size(), FreeText("Engraving", isRequired: true, hasCondition: true)))
            .Should().BeEmpty();
    }

    [Test]
    public void FindUncollectableRequiredAttributes_MixedProduct_ReportsOnlyWhatItCannotCollect()
    {
        var result = ProductsHelper.FindUncollectableRequiredAttributes(
            Product(Colour(true), Size(), FreeText("Enter your text", isRequired: true)));

        result.Should().ContainSingle().Which.Should().Be("Enter your text");
    }

    #endregion

    #region The two Values.Any() rules, which are the easiest thing to break

    /// <summary>
    /// The view renders the first colour attribute THAT HAS VALUES. A required colour attribute with
    /// nothing to pick from would otherwise render a dropdown holding only "- Select -", and the customer
    /// would be told to choose an option that does not exist.
    /// </summary>
    [Test]
    public void FindUncollectableRequiredAttributes_RequiredColourWithNoValues_IsReported()
    {
        ProductsHelper.FindUncollectableRequiredAttributes(Product(Colour(isRequired: true, withValues: false)))
            .Should().ContainSingle().Which.Should().Be("Colour");
    }

    /// <summary>
    /// A second colour-ish attribute is not special: it has values, so the picker renders it as an
    /// ordinary dropdown and the customer can supply it.
    /// </summary>
    /// <remarks>
    /// Changed by fix 32. It used to be excluded from the "other attributes" list and silently dropped,
    /// which — when required — refused a product that could in fact be specified.
    /// </remarks>
    [Test]
    public void FindUncollectableRequiredAttributes_SecondColourAttribute_IsRenderedNotReported()
    {
        var second = Attr("Colour trim", isRequired: true, controlType: AttributeControlType.ColorSquares,
            values: [Value(20, "Gold"), Value(21, "Silver")]);

        ProductsHelper.FindUncollectableRequiredAttributes(Product(Colour(true), second))
            .Should().BeEmpty();
    }

    /// <summary>
    /// Every NON-colour attribute with values is rendered, not just one — so a product with three
    /// dropdowns is fully collectable.
    /// </summary>
    [Test]
    public void FindUncollectableRequiredAttributes_SeveralNonColourDropdowns_AreAllCollectable()
    {
        var product = Product(
            Colour(true),
            Size(),
            Attr("Fit", true, values: [Value(30, "Regular"), Value(31, "Slim")]),
            Attr("Sleeve", true, values: [Value(40, "Short"), Value(41, "Long")]));

        ProductsHelper.FindUncollectableRequiredAttributes(product).Should().BeEmpty();
    }

    #endregion

    #region FindColourAttribute - the two-pass rule (fix 32)

    /// <summary>
    /// The bug fix 32 closed. "Collar" starts with "col", which used to satisfy the colour test outright
    /// — so on a product carrying both, the REAL Colour attribute was seen as a SECOND colour, dropped
    /// from the picker, and (being required) the product was refused with "we cannot take these details
    /// online". The loose prefix is now consulted only when nothing matches the full spelling.
    /// </summary>
    [Test]
    public void FindColourAttribute_FullSpellingBeatsLoosePrefix()
    {
        var collar = Attr("Collar", values: [Value(50, "Button-down"), Value(51, "Spread")]);
        var colour = Colour();

        ProductsHelper.FindColourAttribute(Product(collar, colour).ProductAttributes)
            .Should().BeSameAs(colour, "'Colour' is spelled in full, so 'Collar' never gets a say");
    }

    [Test]
    public void FindUncollectableRequiredAttributes_CollarBesideColour_RefusesNothing()
    {
        var collar = Attr("Collar", isRequired: true, values: [Value(50, "Button-down"), Value(51, "Spread")]);

        ProductsHelper.FindUncollectableRequiredAttributes(Product(collar, Colour(true)))
            .Should().BeEmpty("both attributes have values, so both render");
    }

    /// <summary>
    /// Why the loose prefix is kept rather than removed: it is misspelling tolerance, and it still works
    /// when there is no correctly spelled attribute to lose to.
    /// </summary>
    [Test]
    public void FindColourAttribute_MisspelledName_IsStillFoundWhenNothingElseMatches()
    {
        var misspelled = Attr("Colur", values: [Value(60, "Red"), Value(61, "Blue")]);

        ProductsHelper.FindColourAttribute(Product(Size(), misspelled).ProductAttributes)
            .Should().BeSameAs(misspelled);
    }

    /// <summary>
    /// "Collar" alone still takes the colour slot, and that is harmless: the view labels the dropdown
    /// with the attribute's own name and only draws swatches for a ColorSquares control, so it renders
    /// as an ordinary "Collar" dropdown and is collected normally.
    /// </summary>
    [Test]
    public void FindColourAttribute_CollarAlone_TakesTheSlot_AndThatIsHarmless()
    {
        var collar = Attr("Collar", values: [Value(50, "Button-down"), Value(51, "Spread")]);

        ProductsHelper.FindColourAttribute(Product(collar, Size()).ProductAttributes)
            .Should().BeSameAs(collar);
    }

    /// <summary>A ColorSquares control is a colour picker whatever it is called.</summary>
    [Test]
    public void FindColourAttribute_ColourSquares_WinsWhateverItIsNamed()
    {
        var swatches = Attr("Shade", controlType: AttributeControlType.ColorSquares,
            values: [Value(70, "Ecru"), Value(71, "Bone")]);

        ProductsHelper.FindColourAttribute(Product(Size(), swatches).ProductAttributes)
            .Should().BeSameAs(swatches);
    }

    [Test]
    public void FindColourAttribute_ValuelessCandidate_IsNotChosen()
    {
        ProductsHelper.FindColourAttribute(Product(Colour(true, withValues: false), Size()).ProductAttributes)
            .Should().BeNull("a dropdown holding only '- Select -' is not something a customer can satisfy");
    }

    [Test]
    public void FindColourAttribute_NoCandidate_IsNull()
    {
        ProductsHelper.FindColourAttribute(Product(Size()).ProductAttributes).Should().BeNull();
    }

    [Test]
    public void FindColourAttribute_NullList_IsNull()
    {
        ProductsHelper.FindColourAttribute(null).Should().BeNull();
    }

    #endregion
}
