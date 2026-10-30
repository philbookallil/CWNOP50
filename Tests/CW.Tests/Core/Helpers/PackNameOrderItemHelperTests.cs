using AwesomeAssertions;
using Nop.Plugin.CW.Core.Helpers;
using NUnit.Framework;

namespace CW.Tests.Core.Helpers;

/// <summary>
/// Who a packname order line is for, as printed under the product on the invoice and the packing slip:
/// packname, department and cost centre, the empty ones left out.
/// </summary>
[TestFixture]
public class PackNameOrderItemHelperTests
{
    [Test]
    public void AllThree_JoinedInOrder()
    {
        PackNameOrderItemHelper.FormatFor("Jane Doe", "Sales", "CC-01").Should().Be("Jane Doe · Sales · CC-01");
    }

    [Test]
    public void NoCostCentre_LeftOut()
    {
        PackNameOrderItemHelper.FormatFor("test test", "Department 2", "").Should().Be("test test · Department 2");
    }

    [Test]
    public void OnlyThePackName()
    {
        PackNameOrderItemHelper.FormatFor("Steve Gates", null, "   ").Should().Be("Steve Gates");
    }

    [Test]
    public void NoPackName_TheOthersStillShown()
    {
        PackNameOrderItemHelper.FormatFor(null, "Sales", "CC-01").Should().Be("Sales · CC-01");
    }

    [Test]
    public void SurroundingSpaces_Trimmed()
    {
        PackNameOrderItemHelper.FormatFor("  Jane  ", " Sales ", null).Should().Be("Jane · Sales");
    }

    [Test]
    public void NothingStored_Null()
    {
        PackNameOrderItemHelper.FormatFor(null, "", "  ").Should().BeNull();
    }
}
