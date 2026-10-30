using AwesomeAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Nop.Plugin.CW.Core.BusinessLogic;
using Nop.Plugin.CW.Core.BusinessLogic.Requests;
using Nop.Plugin.CW.Core.Controllers;
using NUnit.Framework;

namespace CW.Tests.Core.Controllers;

/// <summary>
/// The bulk ordering add refuses a body that does not read as a request, so the page reports the failure instead of
/// "0 added"
/// </summary>
[TestFixture]
public class BulkOrderControllerTests
{
    [Test]
    public async Task A_body_that_does_not_read_as_a_request_is_refused()
    {
        //a quantity past what a number holds leaves the model binder with no request at all
        var logic = new Mock<IBulkOrderLogic>();
        var controller = TestObject.Create<BulkOrderController>();
        TestObject.Set(controller, "_bulkOrderLogic", logic.Object);

        var result = await controller.AddToCart(null!);

        result.Should().BeOfType<BadRequestResult>();
        logic.Verify(l => l.AddToCartAsync(It.IsAny<BulkOrderAddRequest>()), Times.Never);
    }

    [Test]
    public async Task A_request_with_an_empty_line_is_refused()
    {
        var logic = new Mock<IBulkOrderLogic>();
        var controller = TestObject.Create<BulkOrderController>();
        TestObject.Set(controller, "_bulkOrderLogic", logic.Object);

        var result = await controller.AddToCart(new BulkOrderAddRequest([null!]));

        result.Should().BeOfType<BadRequestResult>();
        logic.Verify(l => l.AddToCartAsync(It.IsAny<BulkOrderAddRequest>()), Times.Never);
    }
}
