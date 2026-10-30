using ClosedXML.Excel;
using AwesomeAssertions;
using Moq;
using Nop.Core;
using Nop.Core.Caching;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Logging;
using Nop.Plugin.CW.Core.Domain.Branches;
using Nop.Plugin.CW.Core.Domain.PackNames;
using Nop.Plugin.CW.Core.Domain.PackNames.DTOs;
using Nop.Plugin.CW.Core.Domain.Settings;
using Nop.Plugin.CW.Core.Services.Branches;
using Nop.Plugin.CW.Core.Services.PackNames;
using Nop.Plugin.CW.Core.Services.PackNames.Bulk;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Logging;
using NUnit.Framework;

namespace CW.Tests.Core.Services.PackNames;

/// <summary>
/// The admin bulk actions on packnames: the request is refused whole only when it cannot be acted on at all; otherwise
/// each packname is checked with the single save's rules, skipped with its reason when a rule refuses it, and written ten
/// to a transaction - an error stops the action and keeps what the batches before it did.
/// </summary>
[TestFixture]
public class PackNameBulkServiceTests
{
    private const int StoreId = 1;

    private Dictionary<int, PackName> _packNames;
    private Dictionary<int, PackNameList> _lists;
    private Dictionary<int, Branch> _branches;
    private HashSet<(int ListId, string Name)> _namesTaken;
    private MultiStoreSettings _settings;

    private Mock<IPackNameService> _packNameService;
    private Mock<ICustomerActivityService> _activity;
    private Mock<ILogger> _logger;

    #region Utilities

    private PackNameBulkService Build(Mock<IStaticCacheManager> cache = null)
    {
        var branchService = new Mock<IBranchService>();
        branchService.Setup(service => service.GetBranchByIdAsync(It.IsAny<int>()))
            .ReturnsAsync((int id) => _branches.GetValueOrDefault(id));
        branchService.Setup(service => service.GetAllBranchesByStoreIdAsync(StoreId))
            .ReturnsAsync(() => _branches.Values.Where(branch => !branch.Deleted).ToList());
        branchService.Setup(service => service.GetFormattedBreadCrumbAsync(StoreId, It.IsAny<Branch>(), It.IsAny<IList<Branch>>(), It.IsAny<string>()))
            .ReturnsAsync((int storeId, Branch branch, IList<Branch> all, string separator) => branch.Name);

        var localization = new Mock<ILocalizationService>();
        localization.Setup(service => service.GetResourceAsync(It.IsAny<string>())).ReturnsAsync((string key) => key switch
        {
            "Plugins.CW.Core.Admin.PackNames.Bulk.Skip.NameTaken" => "name taken in {0}",
            "Plugins.CW.Core.Admin.PackNames.Bulk.TooMany" => "at most {0}",
            "Plugins.CW.Core.Admin.PackNames.Bulk.Stopped" => "stopped, {0} not processed",
            "Plugins.CW.Core.ActivityLog.EditPackName" => "edited {0}",
            "Plugins.CW.Core.ActivityLog.DeletePackName" => "deleted {0}",
            _ => key
        });

        var settings = new Mock<ISettingService>();
        settings.Setup(service => service.LoadSettingAsync<MultiStoreSettings>(StoreId)).ReturnsAsync(() => _settings);

        var workContext = new Mock<IWorkContext>();
        workContext.Setup(context => context.GetCurrentCustomerAsync()).ReturnsAsync(new Customer { Id = 1 });

        return new PackNameBulkService(_packNameService.Object, branchService.Object, _activity.Object, localization.Object,
            settings.Object, _logger.Object, workContext.Object, (cache ?? new Mock<IStaticCacheManager>()).Object);
    }

    private void AddPackNames(int listId, int count, int firstId = 1, bool active = true)
    {
        for (var id = firstId; id < firstId + count; id++)
            _packNames[id] = new PackName { Id = id, Name = $"Packname {id}", PackNameListId = listId, Active = active };
    }

    private static PackNameBulkRequest Request(PackNameBulkAction action, IEnumerable<int> ids, int targetListId = 0)
    {
        return new PackNameBulkRequest(StoreId, action, ids.ToList(), targetListId);
    }

    #endregion

    [SetUp]
    public void SetUp()
    {
        _packNames = new Dictionary<int, PackName>();
        _branches = new Dictionary<int, Branch>
        {
            [5] = new() { Id = 5, StoreId = StoreId, Name = "Harbour", Active = true },
            [6] = new() { Id = 6, StoreId = StoreId, Name = "Closed branch", Active = false }
        };
        _lists = new Dictionary<int, PackNameList>
        {
            [10] = new() { Id = 10, StoreId = StoreId, BranchId = 5, Name = "Harbour", Active = true },
            [11] = new() { Id = 11, StoreId = StoreId, BranchId = 5, Name = "Harbour night shift", Active = true },
            [12] = new() { Id = 12, StoreId = StoreId, BranchId = 5, Name = "Switched off", Active = false },
            [13] = new() { Id = 13, StoreId = StoreId, BranchId = 6, Name = "In a closed branch", Active = true },
            [20] = new() { Id = 20, StoreId = 2, BranchId = null, Name = "Other store", Active = true }
        };
        _namesTaken = new HashSet<(int, string)>();
        _settings = new MultiStoreSettings();

        _packNameService = new Mock<IPackNameService>();
        _packNameService.Setup(service => service.GetPackNamesByIdsAsync(It.IsAny<int[]>()))
            .ReturnsAsync((int[] ids) => ids.Where(_packNames.ContainsKey).Select(id => _packNames[id]).ToList());
        _packNameService.Setup(service => service.GetPackNameListsByIdsAsync(It.IsAny<int[]>()))
            .ReturnsAsync((int[] ids) => ids.Where(_lists.ContainsKey).Select(id => _lists[id]).ToList());
        _packNameService.Setup(service => service.GetPackNameListByIdAsync(It.IsAny<int>()))
            .ReturnsAsync((int id) => _lists.GetValueOrDefault(id));
        _packNameService.Setup(service => service.GetActivePackNameListsByStoreIdAsync(StoreId))
            .ReturnsAsync(() => _lists.Values.Where(list => list.StoreId == StoreId && list.Active && !list.Deleted).ToList());
        _packNameService.Setup(service => service.PackNameNameExistsAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int>()))
            .ReturnsAsync((int listId, string name, int ignoreId) => _namesTaken.Contains((listId, name)));
        _packNameService.Setup(service => service.UpdatePackNameAsync(It.IsAny<PackName>())).Returns(Task.CompletedTask);
        _packNameService.Setup(service => service.DeletePackNameAsync(It.IsAny<PackName>()))
            .Callback<PackName>(packName => packName.Deleted = true)
            .Returns(Task.CompletedTask);

        _activity = new Mock<ICustomerActivityService>();
        _activity.Setup(service => service.InsertActivityAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<BaseEntity>()))
            .ReturnsAsync(new ActivityLog());

        _logger = new Mock<ILogger>();
    }

    #region Refusals

    [Test]
    public async Task Nothing_selected_is_refused()
    {
        var result = await Build().ApplyAsync(Request(PackNameBulkAction.Activate, [0, -4]));

        result.RefusedMessage.Should().Be("Plugins.CW.Core.Admin.PackNames.Bulk.NothingSelected");
        _packNameService.Verify(service => service.UpdatePackNameAsync(It.IsAny<PackName>()), Times.Never);
    }

    [Test]
    public async Task More_than_the_limit_is_refused_whole()
    {
        AddPackNames(10, PackNameBulkDefaults.MaxPackNames + 1);

        var result = await Build().ApplyAsync(Request(PackNameBulkAction.Deactivate, _packNames.Keys));

        result.RefusedMessage.Should().Be($"at most {PackNameBulkDefaults.MaxPackNames}");
        _packNameService.Verify(service => service.UpdatePackNameAsync(It.IsAny<PackName>()), Times.Never);
    }

    [Test]
    public async Task An_action_the_service_does_not_know_is_refused()
    {
        AddPackNames(10, 1);

        var result = await Build().ApplyAsync(Request((PackNameBulkAction)99, [1]));

        result.RefusedMessage.Should().Be("Plugins.CW.Core.Admin.PackNames.Bulk.UnknownAction");
    }

    [TestCase(20, "Plugins.CW.Core.Admin.PackNames.PackName.Field.PackNameList.WrongStore", TestName = "Move to a list of another store")]
    [TestCase(99, "Plugins.CW.Core.Admin.PackNames.PackName.Field.PackNameList.WrongStore", TestName = "Move to a list that does not exist")]
    [TestCase(12, "Plugins.CW.Core.Admin.PackNames.PackName.Field.PackNameList.Inactive", TestName = "Move to a switched-off list")]
    [TestCase(13, "Plugins.CW.Core.PackNames.PackName.Field.Branch.Inactive", TestName = "Move to a list in a switched-off branch")]
    public async Task A_move_to_a_list_that_takes_no_packnames_is_refused_whole(int targetListId, string message)
    {
        AddPackNames(10, 3);

        var result = await Build().ApplyAsync(Request(PackNameBulkAction.Move, [1, 2, 3], targetListId));

        result.RefusedMessage.Should().Be(message);
        _packNames.Values.Should().OnlyContain(packName => packName.PackNameListId == 10);
        _packNameService.Verify(service => service.UpdatePackNameAsync(It.IsAny<PackName>()), Times.Never);
    }

    #endregion

    #region Each packname

    [Test]
    public async Task Deactivate_switches_off_the_active_ones_and_counts_the_others_as_unchanged()
    {
        AddPackNames(10, 3);
        _packNames[2].Active = false;

        var result = await Build().ApplyAsync(Request(PackNameBulkAction.Deactivate, [1, 2, 3]));

        result.RefusedMessage.Should().BeNull();
        result.Changed.Should().Be(2);
        result.Unchanged.Should().Be(1);
        _packNames.Values.Should().OnlyContain(packName => !packName.Active);
        _packNameService.Verify(service => service.UpdatePackNameAsync(It.IsAny<PackName>()), Times.Exactly(2));
        _activity.Verify(service => service.InsertActivityAsync("EditPackName", "edited Packname 1", _packNames[1]), Times.Once);
        _activity.Verify(service => service.InsertActivityAsync("EditPackName", "edited Packname 2", It.IsAny<BaseEntity>()), Times.Never);
    }

    [Test]
    public async Task Activate_switches_on()
    {
        AddPackNames(10, 2, active: false);

        var result = await Build().ApplyAsync(Request(PackNameBulkAction.Activate, [1, 2]));

        result.Changed.Should().Be(2);
        _packNames.Values.Should().OnlyContain(packName => packName.Active);
    }

    [Test]
    public async Task Delete_deletes_each_packname_and_logs_it()
    {
        AddPackNames(10, 2);

        var result = await Build().ApplyAsync(Request(PackNameBulkAction.Delete, [1, 2]));

        result.Changed.Should().Be(2);
        _packNameService.Verify(service => service.DeletePackNameAsync(It.IsAny<PackName>()), Times.Exactly(2));
        _activity.Verify(service => service.InsertActivityAsync("DeletePackName", "deleted Packname 2", _packNames[2]), Times.Once);
    }

    [Test]
    public async Task A_packname_that_is_gone_or_in_another_store_is_skipped_and_the_rest_go_ahead()
    {
        AddPackNames(10, 2);
        _packNames[3] = new PackName { Id = 3, Name = "Elsewhere", PackNameListId = 20, Active = true };
        _packNames[4] = new PackName { Id = 4, Name = "Already deleted", PackNameListId = 10, Active = true, Deleted = true };

        var result = await Build().ApplyAsync(Request(PackNameBulkAction.Deactivate, [1, 2, 3, 4, 77]));

        result.Changed.Should().Be(2);
        result.Skipped.Select(skip => skip.Name).Should().Equal("Elsewhere", "Already deleted", "#77");
        result.Skipped.Should().OnlyContain(skip => skip.Reason == "Plugins.CW.Core.Admin.PackNames.Bulk.Skip.NotFound");
        _packNames[3].Active.Should().BeTrue();
        _packNames[4].Active.Should().BeTrue();
    }

    [Test]
    public async Task A_move_skips_a_name_the_list_already_has_and_one_moved_by_the_same_action()
    {
        AddPackNames(10, 4);
        _packNames[3].Name = "Packname 1";
        _namesTaken.Add((11, "Packname 2"));

        var result = await Build().ApplyAsync(Request(PackNameBulkAction.Move, [1, 2, 3, 4], 11));

        result.Changed.Should().Be(2);
        result.Skipped.Select(skip => skip.PackNameId).Should().Equal(2, 3);
        result.Skipped.Should().OnlyContain(skip => skip.Reason == "name taken in Harbour night shift");
        _packNames[1].PackNameListId.Should().Be(11);
        _packNames[2].PackNameListId.Should().Be(10);
        _packNames[3].PackNameListId.Should().Be(10);
        _packNames[4].PackNameListId.Should().Be(11);
    }

    [Test]
    public async Task A_packname_already_in_the_target_list_is_unchanged()
    {
        AddPackNames(11, 1);

        var result = await Build().ApplyAsync(Request(PackNameBulkAction.Move, [1], 11));

        result.Unchanged.Should().Be(1);
        _packNameService.Verify(service => service.UpdatePackNameAsync(It.IsAny<PackName>()), Times.Never);
    }

    #endregion

    #region Batches

    [Test]
    public async Task An_error_stops_the_action_and_keeps_the_batches_before_it()
    {
        AddPackNames(10, 25);
        _packNameService.Setup(service => service.UpdatePackNameAsync(It.Is<PackName>(packName => packName.Id == 15)))
            .ThrowsAsync(new InvalidOperationException("database said no"));

        var result = await Build().ApplyAsync(Request(PackNameBulkAction.Deactivate, Enumerable.Range(1, 25)));

        result.RefusedMessage.Should().BeNull();
        result.Changed.Should().Be(10, "only the first batch of ten committed");
        result.NotProcessed.Should().Be(15);
        result.StoppedMessage.Should().Be("stopped, 15 not processed");
        _logger.Verify(logger => logger.ErrorAsync(It.Is<string>(message => message.Contains("15 of 25")),
            It.Is<Exception>(exception => exception.Message == "database said no"), It.IsAny<Customer>()), Times.Once);
        _packNameService.Verify(service => service.UpdatePackNameAsync(It.Is<PackName>(packName => packName.Id > 15)), Times.Never);
    }

    [Test]
    public async Task A_list_switched_off_during_a_move_takes_nothing_more()
    {
        AddPackNames(10, 15);

        //the target is switched off while the first batch is written
        _packNameService.Setup(service => service.UpdatePackNameAsync(It.Is<PackName>(packName => packName.Id == 10)))
            .Callback(() => _lists[11].Active = false)
            .Returns(Task.CompletedTask);

        var result = await Build().ApplyAsync(Request(PackNameBulkAction.Move, Enumerable.Range(1, 15), 11));

        result.RefusedMessage.Should().BeNull();
        result.Changed.Should().Be(10);
        result.Skipped.Should().HaveCount(5);
        result.Skipped.Should().OnlyContain(skip => skip.Reason == "Plugins.CW.Core.Admin.PackNames.PackName.Field.PackNameList.Inactive");
        _packNames.Values.Where(packName => packName.Id > 10).Should().OnlyContain(packName => packName.PackNameListId == 10);
    }

    [Test]
    public async Task Once_the_target_is_switched_off_a_packname_already_in_it_is_still_unchanged()
    {
        AddPackNames(10, 15);
        AddPackNames(11, 1, firstId: 16);

        _packNameService.Setup(service => service.UpdatePackNameAsync(It.Is<PackName>(packName => packName.Id == 10)))
            .Callback(() => _lists[11].Active = false)
            .Returns(Task.CompletedTask);

        var result = await Build().ApplyAsync(Request(PackNameBulkAction.Move, Enumerable.Range(1, 16), 11));

        result.Changed.Should().Be(10);
        result.Unchanged.Should().Be(1);
        result.Skipped.Should().HaveCount(5);
        result.Skipped.Should().NotContain(skip => skip.PackNameId == 16);
    }

    [Test]
    public async Task A_cache_that_cannot_be_cleared_after_a_batch_does_not_stop_the_action()
    {
        AddPackNames(10, 25);
        var cache = new Mock<IStaticCacheManager>();
        cache.Setup(manager => manager.RemoveByPrefixAsync(It.IsAny<string>(), It.IsAny<object[]>()))
            .ThrowsAsync(new InvalidOperationException("cache said no"));

        var result = await Build(cache).ApplyAsync(Request(PackNameBulkAction.Deactivate, Enumerable.Range(1, 25)));

        result.Changed.Should().Be(25);
        result.NotProcessed.Should().Be(0);
        result.StoppedMessage.Should().BeNull();
        _logger.Verify(logger => logger.WarningAsync(It.IsAny<string>(), It.Is<Exception>(exception => exception.Message == "cache said no"),
            It.IsAny<Customer>()), Times.Exactly(3));
        _logger.Verify(logger => logger.ErrorAsync(It.IsAny<string>(), It.IsAny<Exception>(), It.IsAny<Customer>()), Times.Never);
    }

    [Test]
    public async Task Each_batch_reads_its_packnames_inside_its_own_transaction()
    {
        AddPackNames(10, 25);

        await Build().ApplyAsync(Request(PackNameBulkAction.Deactivate, Enumerable.Range(1, 25)));

        _packNameService.Verify(service => service.GetPackNamesByIdsAsync(It.IsAny<int[]>()), Times.Exactly(3));
        _packNameService.Verify(service => service.GetPackNamesByIdsAsync(It.Is<int[]>(ids => ids.Length == 10 && ids[0] == 11)), Times.Once);
    }

    #endregion

    #region Move targets and export

    [Test]
    public async Task Move_targets_are_the_switched_on_lists_in_switched_on_branches_by_path()
    {
        var targets = await Build().GetMoveTargetsAsync(StoreId);

        targets.Select(target => target.PackNameListId).Should().Equal(10, 11);
        targets.Select(target => target.Text).Should().Equal("Harbour >> Harbour", "Harbour >> Harbour night shift");
    }

    [Test]
    public async Task The_export_has_the_store_columns_and_keeps_values_as_text()
    {
        _settings.EmployeeNumberEnabled = true;
        _settings.GenderEnabled = false;

        var packNames = new List<PackNameDTO>
        {
            new() { Id = 1, Name = "=SUM(A1)", EmployeeNumber = "007", Branch = "Harbour", PackNameListName = "Harbour", Email = "ann@example.com", Manager = true, Active = true }
        };

        var bytes = await Build().ExportToXlsxAsync(StoreId, packNames);

        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var sheet = workbook.Worksheet(1);
        var headers = sheet.Row(1).CellsUsed().Select(cell => cell.GetString()).ToList();

        headers.Should().StartWith(new[] { "Plugins.CW.Core.Admin.PackNames.PackName.Field.EmployeeNumber", "Plugins.CW.Core.Admin.PackNames.PackName.Field.Name" });
        headers.Should().NotContain("Plugins.CW.Core.Admin.PackNames.PackName.Field.Gender");
        headers.Should().Contain("Plugins.CW.Core.Admin.PackNames.SearchEmail");
        sheet.Cell(2, 1).GetString().Should().Be("007");
        sheet.Cell(2, 1).DataType.Should().Be(XLDataType.Text);
        sheet.Cell(2, 2).HasFormula.Should().BeFalse();
        sheet.Cell(2, 2).GetString().Should().Be("=SUM(A1)");
    }

    #endregion
}
