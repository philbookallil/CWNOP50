using System.Globalization;
using AwesomeAssertions;
using CW.Common.Helpers;
using NUnit.Framework;

namespace CW.Tests.Core.Helpers;

/// <summary>
/// Amounts on the admin return page are shown with two decimals whatever the culture of the request: a culture's own
/// default number of decimals (three on some systems for en-AU) must not reach a money amount.
/// </summary>
[TestFixture]
public class FormatHelperTests
{
    [TestCase("en-AU")]
    [TestCase("en-US")]
    [TestCase("es-CO")]
    public void FormatAbsPrice_always_shows_two_decimals(string cultureName)
    {
        var culture = (CultureInfo)CultureInfo.GetCultureInfo(cultureName).Clone();
        culture.NumberFormat.NumberDecimalDigits = 3;
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = culture;
            var sep = culture.NumberFormat.NumberDecimalSeparator;

            FormatHelper.FormatAbsPrice(22m).Should().Be($"22{sep}00");
            FormatHelper.FormatAbsPrice(-5.5m).Should().Be($"-5{sep}50");
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }
}
