using AwesomeAssertions;
using Moq;
using Nop.Core.Caching;
using Nop.Core.Configuration;
using Nop.Core.Infrastructure;
using Nop.Data;
using Nop.Plugin.CW.Core.Domain.Branches;
using Nop.Plugin.CW.Core.Domain.PackNames;
using Nop.Plugin.CW.Core.Services.MultiStores;
using NUnit.Framework;

namespace CW.Tests.Core.Services.MultiStores;

/// <summary>
/// A manager's store is the store of the list their own packname is in, read from the list itself, and there is none
/// once the packname, the list or the list's branch is deleted
/// </summary>
[TestFixture]
public class ManagerStoreTests
{
    private readonly List<PackName> _packNames = [];
    private readonly List<PackNameList> _lists = [];
    private readonly List<Branch> _branches = [];
    private MultiStoreService _service = null!;

    [SetUp]
    public void SetUp()
    {
        //a CacheKey reads its default cache time from the app settings, which nop's start-up provides
        Singleton<AppSettings>.Instance ??= new AppSettings([new CacheConfig()]);

        _packNames.Clear();
        _lists.Clear();
        _branches.Clear();

        var packNameRepository = new Mock<IRepository<PackName>>();
        packNameRepository.Setup(repository => repository.Table).Returns(() => _packNames.AsQueryable());

        var listRepository = new Mock<IRepository<PackNameList>>();
        listRepository.Setup(repository => repository.Table).Returns(() => _lists.AsQueryable());

        var branchRepository = new Mock<IRepository<Branch>>();
        branchRepository.Setup(repository => repository.Table).Returns(() => _branches.AsQueryable());

        //no caching: every call runs the query
        var cache = new Mock<IStaticCacheManager>();
        cache.Setup(manager => manager.GetAsync(It.IsAny<CacheKey>(), It.IsAny<Func<Task<int?>>>()))
            .Returns((CacheKey _, Func<Task<int?>> acquire) => acquire());

        _service = TestObject.Create<MultiStoreService>();
        TestObject.Set(_service, "_packNameRepository", packNameRepository.Object);
        TestObject.Set(_service, "_packNameListRepository", listRepository.Object);
        TestObject.Set(_service, "_branchRepository", branchRepository.Object);
        TestObject.Set(_service, "_staticCacheManager", cache.Object);
    }

    private Task<int?> ManagerStoreAsync(int managerId) => TestObject.InvokeAsync<int?>(_service, "GetManagerStoreIdAsync", managerId);

    [Test]
    public async Task The_store_is_the_store_of_the_managers_list()
    {
        _branches.Add(new Branch { Id = 7, StoreId = 4 });
        _lists.Add(new PackNameList { Id = 1, StoreId = 4, BranchId = 7 });
        _packNames.Add(new PackName { Id = 100, PackNameListId = 1, Manager = true });

        (await ManagerStoreAsync(100)).Should().Be(4);
    }

    [Test]
    public async Task A_list_without_a_branch_still_has_its_store()
    {
        _lists.Add(new PackNameList { Id = 1, StoreId = 4 });
        _packNames.Add(new PackName { Id = 100, PackNameListId = 1, Manager = true });

        (await ManagerStoreAsync(100)).Should().Be(4);
    }

    [Test]
    public async Task A_deleted_packname_has_no_store()
    {
        _branches.Add(new Branch { Id = 7, StoreId = 4 });
        _lists.Add(new PackNameList { Id = 1, StoreId = 4, BranchId = 7 });
        _packNames.Add(new PackName { Id = 100, PackNameListId = 1, Manager = true, Deleted = true });

        (await ManagerStoreAsync(100)).Should().BeNull();
    }

    [Test]
    public async Task A_packname_in_a_deleted_list_has_no_store()
    {
        _branches.Add(new Branch { Id = 7, StoreId = 4 });
        _lists.Add(new PackNameList { Id = 1, StoreId = 4, BranchId = 7, Deleted = true });
        _packNames.Add(new PackName { Id = 100, PackNameListId = 1, Manager = true });

        (await ManagerStoreAsync(100)).Should().BeNull();
    }

    [Test]
    public async Task A_packname_in_a_list_of_a_deleted_branch_has_no_store()
    {
        _branches.Add(new Branch { Id = 7, StoreId = 4, Deleted = true });
        _lists.Add(new PackNameList { Id = 1, StoreId = 4, BranchId = 7 });
        _packNames.Add(new PackName { Id = 100, PackNameListId = 1, Manager = true });

        (await ManagerStoreAsync(100)).Should().BeNull();
    }
}
