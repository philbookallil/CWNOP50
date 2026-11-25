using AwesomeAssertions;
using Moq;
using Nop.Core.Domain.Orders;
using Nop.Data;
using Nop.Plugin.CW.Core.Services.CreditNotes;
using NUnit.Framework;

namespace CW.Tests.Core.Services.CreditNotes;

/// <summary>
/// Which orders gave their store credit back: the ones the balance views do not count
/// </summary>
[TestFixture]
public class GivenBackCreditTests
{
    [Test]
    public async Task A_cancelled_order_and_one_that_is_gone_gave_the_credit_back_and_a_deleted_one_did_not()
    {
        var orders = new Mock<IRepository<Order>>();
        orders.Setup(mock => mock.Table).Returns(new List<Order>
        {
            new() { Id = 1, OrderStatus = OrderStatus.Pending },
            new() { Id = 2, OrderStatus = OrderStatus.Cancelled },
            new() { Id = 3, OrderStatus = OrderStatus.Processing, Deleted = true }
        }.AsQueryable());

        var service = TestObject.Create<CreditNotesService>();
        TestObject.Set(service, "_orderRepository", orders.Object);

        (await service.GetGivenBackOrderIdsAsync([1, 2, 3, 4])).Should().Equal(2, 4);
        (await service.GetGivenBackOrderIdsAsync([])).Should().BeEmpty();
    }
}
