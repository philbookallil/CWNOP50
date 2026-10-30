using Moq;
using Nop.Core.Events;
using Nop.Plugin.CW.Core.Domain.PackNames;
using Nop.Plugin.CW.Core.Services.PackNames;
using NUnit.Framework;

namespace CW.Tests.Core.Services.PackNames;

/// <summary>
/// Every save of a packname - created, edited, moved singly or in bulk - puts its customer in the packname's branch, and
/// every save of a list puts its packnames' customers in the list's branch
/// </summary>
[TestFixture]
public class PackNameCustomerBranchEventConsumerTests
{
    [Test]
    public async Task A_created_packname_puts_its_customer_in_its_branch()
    {
        var provisioning = new Mock<IPackNameProvisioningService>();
        var packName = new PackName { Id = 400, PackNameListId = 30, CustomerId = 1048 };

        await new PackNameCustomerBranchEventConsumer(provisioning.Object).HandleEventAsync(new EntityInsertedEvent<PackName>(packName));

        provisioning.Verify(service => service.SetCustomerBranchFromPackNameAsync(packName), Times.Once);
    }

    [Test]
    public async Task A_saved_packname_puts_its_customer_in_its_branch()
    {
        var provisioning = new Mock<IPackNameProvisioningService>();
        var packName = new PackName { Id = 400, PackNameListId = 30, CustomerId = 1048 };

        await new PackNameCustomerBranchEventConsumer(provisioning.Object).HandleEventAsync(new EntityUpdatedEvent<PackName>(packName));

        provisioning.Verify(service => service.SetCustomerBranchFromPackNameAsync(packName), Times.Once);
    }

    [Test]
    public async Task A_saved_list_puts_its_packnames_customers_in_its_branch()
    {
        var provisioning = new Mock<IPackNameProvisioningService>();
        var list = new PackNameList { Id = 30, BranchId = 6, StoreId = 1 };

        await new PackNameCustomerBranchEventConsumer(provisioning.Object).HandleEventAsync(new EntityUpdatedEvent<PackNameList>(list));

        provisioning.Verify(service => service.SetCustomerBranchesFromPackNameListAsync(list), Times.Once);
    }
}
