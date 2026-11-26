using AwesomeAssertions;
using Moq;
using Nop.Core.Caching;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Gdpr;
using Nop.Core.Events;
using Nop.Data;
using Nop.Plugin.CW.Core.Domain.PackNames;
using Nop.Plugin.CW.Core.Services.PackNames;
using NUnit.Framework;

namespace CW.Tests.Core.Services.PackNames;

/// <summary>
/// A deleted customer is no packname's customer any more, whichever of nop's two deletes it was: the ordinary one or the
/// permanent one (GDPR), which publishes no deleted event for the customer
/// </summary>
[TestFixture]
public class PackNameCustomerDeletedEventConsumerTests
{
    private List<PackName> _packNames;
    private List<PackName> _saved;
    private PackNameCustomerDeletedEventConsumer _consumer;

    [SetUp]
    public void SetUp()
    {
        _packNames =
        [
            new() { Id = 1, CustomerId = 7, Manager = true, Active = true, Name = "Ann Lee", PrintName = "Ann", EmployeeNumber = "E7",
                Gender = "F", POEntry = "PO 1", Comments = "Comment", PackNameListId = 30, DepartmentId = 4, CostCenterId = 5 },
            new() { Id = 2, CustomerId = 8, Manager = true },
            new() { Id = 3, CustomerId = null }
        ];
        _saved = [];

        var repository = new Mock<IRepository<PackName>>();
        repository.Setup(repo => repo.GetAllAsync(It.IsAny<Func<IQueryable<PackName>, IQueryable<PackName>>>(),
                It.IsAny<Func<ICacheKeyService, CacheKey>>(), It.IsAny<bool>()))
            .ReturnsAsync((Func<IQueryable<PackName>, IQueryable<PackName>> func, Func<ICacheKeyService, CacheKey> getCacheKey, bool includeDeleted) =>
                (IList<PackName>)func(_packNames.Where(packName => includeDeleted || !packName.Deleted).AsQueryable()).ToList());
        //published: the packname caches are cleared by the updated event
        repository.Setup(repo => repo.UpdateAsync(It.IsAny<PackName>(), true))
            .Callback<PackName, bool>((packName, publishEvent) => _saved.Add(packName))
            .Returns(Task.CompletedTask);

        _consumer = new PackNameCustomerDeletedEventConsumer(repository.Object);
    }

    private Task DeleteAsync(int customerId, bool permanently)
        => permanently
            ? _consumer.HandleEventAsync(new CustomerPermanentlyDeleted(customerId, "ann@cw.test"))
            : _consumer.HandleEventAsync(new EntityDeletedEvent<Customer>(new Customer { Id = customerId }));

    [TestCase(true)]
    [TestCase(false)]
    public async Task The_deleted_customers_packname_loses_its_customer_and_is_no_manager(bool permanently)
    {
        await DeleteAsync(7, permanently);

        _saved.Should().ContainSingle().Which.Id.Should().Be(1);
        _packNames[0].CustomerId.Should().BeNull();
        _packNames[0].Manager.Should().BeFalse();
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task The_rest_of_the_packname_is_untouched(bool permanently)
    {
        await DeleteAsync(7, permanently);

        _packNames[0].Should().BeEquivalentTo(new PackName
        {
            Id = 1, CustomerId = null, Manager = false, Active = true, Name = "Ann Lee", PrintName = "Ann", EmployeeNumber = "E7",
            Gender = "F", POEntry = "PO 1", Comments = "Comment", PackNameListId = 30, DepartmentId = 4, CostCenterId = 5
        });
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task Another_customers_packname_keeps_its_customer(bool permanently)
    {
        await DeleteAsync(7, permanently);

        _packNames[1].CustomerId.Should().Be(8);
        _packNames[1].Manager.Should().BeTrue();
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task A_deleted_packname_loses_its_customer_too(bool permanently)
    {
        _packNames.Add(new PackName { Id = 4, CustomerId = 7, Manager = true, Deleted = true });

        await DeleteAsync(7, permanently);

        _saved.Select(packName => packName.Id).Should().BeEquivalentTo(new[] { 1, 4 });
        _packNames.Should().NotContain(packName => packName.CustomerId == 7);
        _packNames[3].Deleted.Should().BeTrue();
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task A_customer_without_a_packname_saves_nothing(bool permanently)
    {
        await DeleteAsync(99, permanently);

        _saved.Should().BeEmpty();
    }

    [Test]
    public async Task A_deleted_event_without_a_customer_is_ignored()
    {
        await _consumer.HandleEventAsync(new EntityDeletedEvent<Customer>(null));

        _saved.Should().BeEmpty();
    }
}
