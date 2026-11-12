using AwesomeAssertions;
using Nop.Core.Domain.Orders;
using Nop.Core.Events;
using Nop.Plugin.CW.Core.Services;
using Nop.Services.Events;
using NUnit.Framework;

namespace CW.Tests.Core.Services.Events;

/// <summary>
/// The events CW.Core's main consumer does not handle: a cart line update has nothing in CW.Core that reacts to it
/// </summary>
[TestFixture]
public class EventConsumerContractTests
{
    [Test]
    public void A_cart_line_update_is_not_consumed()
        => typeof(EventConsumer).GetInterfaces().Should().NotContain(typeof(IConsumer<EntityUpdatedEvent<ShoppingCartItem>>),
            "it only republished an update of the packname cart row, which nothing handles");
}
