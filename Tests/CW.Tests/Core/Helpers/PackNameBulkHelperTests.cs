using AwesomeAssertions;
using Nop.Plugin.CW.Core.Helpers;
using NUnit.Framework;

namespace CW.Tests.Core.Helpers;

/// <summary>
/// How the packname ids a grid posts for a bulk action or an export are read.
/// </summary>
[TestFixture]
public class PackNameBulkHelperTests
{
    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void Nothing_posted_is_no_ids(string posted)
    {
        PackNameBulkHelper.ParseIds(posted).Should().BeEmpty();
    }

    [Test]
    public void Ids_are_read_in_order_once_each()
    {
        PackNameBulkHelper.ParseIds("12, 15,12,,40 ").Should().Equal(12, 15, 40);
    }

    [Test]
    public void Anything_but_a_positive_whole_number_is_left_out()
    {
        PackNameBulkHelper.ParseIds("7,-3,0,abc,1.5,1e3,+9,8").Should().Equal(7, 8);
    }
}
