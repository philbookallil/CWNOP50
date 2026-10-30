using AwesomeAssertions;
using Nop.Plugin.Payments.NABGateway;
using Nop.Plugin.Payments.NABGateway.Helpers;
using NUnit.Framework;

namespace CW.Tests.Payments.NABGateway;

/// <summary>
/// The card number as sent to NAB, and the store / customer tokens that make up the purchase-order reference.
/// </summary>
[TestFixture]
public class CardNumberAndReferenceTests
{
    #region Card number

    [TestCase("4111 1111 1111 1111", "4111111111111111", TestName = "Spaces are removed")]
    [TestCase("4111-1111-1111-1111", "4111111111111111", TestName = "Dashes are removed")]
    [TestCase(" 4111 1111-1111 1111 ", "4111111111111111", TestName = "Mixed separators and padding are removed")]
    [TestCase("4111x1111", "41111111", TestName = "Anything that is not a digit is removed")]
    public void Card_number_keeps_only_digits(string entered, string expected)
    {
        CardNumberHelper.Sanitize(entered).Should().Be(expected);
    }

    [TestCase(null, TestName = "No card number gives an empty string")]
    [TestCase("", TestName = "An empty card number gives an empty string")]
    [TestCase("   ", TestName = "A blank card number gives an empty string")]
    public void Missing_card_number_gives_an_empty_string(string? entered)
    {
        CardNumberHelper.Sanitize(entered!).Should().BeEmpty();
    }

    #endregion

    #region Reference tokens

    [TestCase("Shop", 10, "Shop", TestName = "A short name is kept")]
    [TestCase("Uniform Store Australia", 10, "Uniform-St", TestName = "A long name is cut and spaces become dashes")]
    [TestCase("Jane Smith-Brown", 15, "Jane-Smith-Brow", TestName = "The customer token is cut at 15")]
    [TestCase("ABCDEFGHI-JKL", 10, "ABCDEFGHI", TestName = "A dash left at the cut is trimmed")]
    [TestCase("Shop.", 10, "Shop", TestName = "A trailing full stop is trimmed")]
    [TestCase("Shop.-.", 10, "Shop", TestName = "Several trailing dashes and stops are trimmed")]
    [TestCase("Store   ", 10, "Store", TestName = "Trailing spaces are trimmed")]
    public void Name_token_is_cut_trimmed_and_dashed(string name, int maxLength, string expected)
    {
        NABXMLPaymentProcessor.BuildNameToken(name, maxLength).Should().Be(expected);
    }

    [Test]
    public void Name_token_defaults_to_ten_characters()
    {
        NABXMLPaymentProcessor.BuildNameToken("Uniform Store Australia").Should().Be("Uniform-St");
    }

    [TestCase(null, TestName = "No name gives an empty token")]
    [TestCase("", TestName = "An empty name gives an empty token")]
    public void Missing_name_gives_an_empty_token(string? name)
    {
        NABXMLPaymentProcessor.BuildNameToken(name!).Should().BeEmpty();
    }

    #endregion
}
