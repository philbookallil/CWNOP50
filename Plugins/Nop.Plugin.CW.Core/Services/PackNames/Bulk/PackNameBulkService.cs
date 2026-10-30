using ClosedXML.Excel;
using Nop.Core;
using Nop.Core.Caching;
using Nop.Plugin.CW.Core.Domain.PackNames;
using Nop.Plugin.CW.Core.Domain.PackNames.DTOs;
using Nop.Plugin.CW.Core.Domain.Settings;
using Nop.Plugin.CW.Core.Services.Branches;
using Nop.Plugin.CW.Core.Services.MultiStores;
using Nop.Plugin.CW.Core.Services.Subsidies;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Logging;

namespace Nop.Plugin.CW.Core.Services.PackNames.Bulk;

/// <summary>
/// The admin packname grids' bulk actions and Excel export
/// </summary>
public class PackNameBulkService : IPackNameBulkService
{
    #region Fields

    protected readonly IPackNameService _packNameService;
    protected readonly IBranchService _branchService;
    protected readonly ICustomerActivityService _customerActivityService;
    protected readonly ILocalizationService _localizationService;
    protected readonly ISettingService _settingService;
    protected readonly ILogger _logger;
    protected readonly IWorkContext _workContext;
    protected readonly IStaticCacheManager _staticCacheManager;

    #endregion

    #region Ctor

    public PackNameBulkService(IPackNameService packNameService,
        IBranchService branchService,
        ICustomerActivityService customerActivityService,
        ILocalizationService localizationService,
        ISettingService settingService,
        ILogger logger,
        IWorkContext workContext,
        IStaticCacheManager staticCacheManager)
    {
        _packNameService = packNameService;
        _branchService = branchService;
        _customerActivityService = customerActivityService;
        _localizationService = localizationService;
        _settingService = settingService;
        _logger = logger;
        _workContext = workContext;
        _staticCacheManager = staticCacheManager;
    }

    #endregion

    #region Utilities

    /// <summary>
    /// A column of the Excel export: its header and how a packname's value is read
    /// </summary>
    /// <param name="Header">The column header</param>
    /// <param name="Value">Reads the packname's value as text</param>
    protected record PackNameExportColumn(string Header, Func<PackNameDTO, string> Value);

    /// <summary>
    /// What a bulk action came to for one packname
    /// </summary>
    protected enum PackNameBulkOutcome
    {
        Changed,
        Unchanged,
        Skipped
    }

    /// <summary>
    /// Why a list cannot take packnames moved into it, or null when it can: it must be in the store and not deleted, be
    /// switched on, and be in a branch that is switched on when it has a branch - the rules of the single save
    /// </summary>
    /// <param name="targetList">The list; null when it was not found</param>
    /// <param name="storeId">The store of the packnames</param>
    /// <returns>The reason, or null</returns>
    protected virtual async Task<string> CheckMoveTargetAsync(PackNameList targetList, int storeId)
    {
        if (targetList == null || targetList.Deleted || targetList.StoreId != storeId)
            return await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.PackNames.PackName.Field.PackNameList.WrongStore");

        if (!targetList.Active)
            return await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.PackNames.PackName.Field.PackNameList.Inactive");

        if (targetList.BranchId > 0)
        {
            var branch = await _branchService.GetBranchByIdAsync(targetList.BranchId.Value);

            if (branch == null || branch.Deleted || !branch.Active)
                return await _localizationService.GetResourceAsync("Plugins.CW.Core.PackNames.PackName.Field.Branch.Inactive");
        }

        return null;
    }

    /// <summary>
    /// Writes the activity log entry the single save or delete writes for the packname
    /// </summary>
    /// <param name="systemKeyword">The activity type</param>
    /// <param name="resourceKey">The resource of the entry's text, with the packname's name as {0}</param>
    /// <param name="packName">The packname</param>
    protected virtual async Task LogActivityAsync(string systemKeyword, string resourceKey, PackName packName)
    {
        await _customerActivityService.InsertActivityAsync(systemKeyword,
            string.Format(await _localizationService.GetResourceAsync(resourceKey), packName.Name), packName);
    }

    /// <summary>
    /// Clears again, once a batch has committed, the cache entries the packname cache consumer clears for each packname
    /// </summary>
    /// <remarks>
    /// The consumer clears them when each row is written, which is before the batch commits; a request that read the rows
    /// meanwhile without waiting for the batch's locks could have cached them as they were. The keys are the consumer's
    /// (PackNameCacheEvenConsumer), so a key added there is added here too.
    /// </remarks>
    /// <param name="packNames">The packnames the batch wrote</param>
    /// <param name="deleted">Whether the batch deleted them</param>
    protected virtual async Task ClearPackNameCachesAsync(IList<PackName> packNames, bool deleted)
    {
        await _staticCacheManager.RemoveByPrefixAsync(PackNameServicesDefaults.PackNameByCustomerCacheKeyAllPrefix);
        await _staticCacheManager.RemoveByPrefixAsync(PackNameServicesDefaults.PackNamesByManagerCacheKeyAllPrefix);
        await _staticCacheManager.RemoveByPrefixAsync(PackNameServicesDefaults.PackNameOwnedIdsByManagerCacheKeyAllPrefix);
        await _staticCacheManager.RemoveByPrefixAsync(PackNameServicesDefaults.ActivePackNamesByListIdCacheKeyAllPrefix);
        await _staticCacheManager.RemoveByPrefixAsync(PackNameServicesDefaults.ActivePackNamesByListIdForDepartmentsCacheKeyAllPrefix);
        await _staticCacheManager.RemoveByPrefixAsync(PackNameServicesDefaults.PackNameListsByDepartmentIdsCacheKeyAllPrefix);
        await _staticCacheManager.RemoveByPrefixAsync(MultiStoreServicesDefaults.PackNameListIdsByManagerCacheKeyAllPrefix);
        await _staticCacheManager.RemoveByPrefixAsync(SubsidyServicesDefaults.SubsidyPackNamesPrefix);
        await _staticCacheManager.RemoveByPrefixAsync(NopEntityCacheDefaults<PackName>.ByIdsPrefix);
        await _staticCacheManager.RemoveByPrefixAsync(NopEntityCacheDefaults<PackName>.AllPrefix);

        foreach (var packName in packNames)
        {
            await _staticCacheManager.RemoveAsync(NopEntityCacheDefaults<PackName>.ByIdCacheKey, packName);
            await _staticCacheManager.RemoveAsync(PackNameServicesDefaults.ActivePackNameByManagerIdCacheKey, packName);
            await _staticCacheManager.RemoveAsync(PackNameServicesDefaults.DepartmentsByManagerCacheKey, packName.Id);
            await _staticCacheManager.RemoveAsync(MultiStoreServicesDefaults.PackNameListsByManagerCacheKey, packName);
            await _staticCacheManager.RemoveAsync(BranchServicesDefaults.ManagerStoreIdCacheKey, packName);
            await _staticCacheManager.RemoveAsync(SubsidyServicesDefaults.SubsidiesByPackNameCacheKey, packName.Id);
            await _staticCacheManager.RemoveAsync(SubsidyServicesDefaults.InOrderingSubsidiesByPackNameCacheKey, packName.Id);
            await _staticCacheManager.RemoveAsync(SubsidyServicesDefaults.OrderingBlockedPackNameCacheKey, packName.Id);
            await _staticCacheManager.RemoveAsync(SubsidyServicesDefaults.PackNameOrderingProductsCacheKey, packName.Id);
            await _staticCacheManager.RemoveAsync(SubsidyServicesDefaults.PackNameHasActiveProductGroupsCacheKey, packName.Id);
            await _staticCacheManager.RemoveAsync(SubsidyServicesDefaults.PackNameNextSubsidyStatusChangeCacheKey, packName.Id);

            if (deleted)
                await _staticCacheManager.RemoveAsync(PackNameServicesDefaults.PackNameAddressesCacheKey, packName);

            //a moved packname takes its customer to the list's branch (PackNameCustomerBranchEventConsumer)
            if (packName.CustomerId > 0)
                await _staticCacheManager.RemoveAsync(BranchServicesDefaults.BranchByCustomerIdCacheKey, packName.CustomerId.Value);
        }

        await _staticCacheManager.RemoveByPrefixAsync(BranchServicesDefaults.BranchStaffCountPrefix);
    }

    /// <summary>
    /// Acts on one packname, inside its batch's transaction
    /// </summary>
    /// <param name="packNameId">The packname identifier asked for</param>
    /// <param name="packName">The packname; null when it was not found</param>
    /// <param name="lists">The lists of the packnames asked for, by identifier</param>
    /// <param name="request">The action</param>
    /// <param name="targetList">The list to move to; null unless the action is a move, or when it no longer exists</param>
    /// <param name="targetRefusal">Why the target list takes no more packnames; null when it still takes them</param>
    /// <param name="movedNames">The names this action has moved into the target list so far</param>
    /// <param name="skipped">Where a packname a rule refuses is recorded</param>
    /// <returns>What the action came to for the packname</returns>
    protected virtual async Task<PackNameBulkOutcome> ApplyOneAsync(int packNameId, PackName packName, IReadOnlyDictionary<int, PackNameList> lists,
        PackNameBulkRequest request, PackNameList targetList, string targetRefusal, ISet<string> movedNames, IList<PackNameBulkSkip> skipped)
    {
        //a packname that is gone, or that is not in this store, is not acted on: the store is the one the feature switch
        //and the screen were checked for
        if (packName == null || packName.Deleted
            || !lists.TryGetValue(packName.PackNameListId, out var list) || list.Deleted || list.StoreId != request.StoreId)
        {
            skipped.Add(new PackNameBulkSkip(packNameId, packName?.Name ?? $"#{packNameId}",
                await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.PackNames.Bulk.Skip.NotFound")));

            return PackNameBulkOutcome.Skipped;
        }

        switch (request.Action)
        {
            case PackNameBulkAction.Activate:
            case PackNameBulkAction.Deactivate:
                {
                    var active = request.Action == PackNameBulkAction.Activate;

                    if (packName.Active == active)
                        return PackNameBulkOutcome.Unchanged;

                    packName.Active = active;
                    await _packNameService.UpdatePackNameAsync(packName);
                    await LogActivityAsync("EditPackName", "Plugins.CW.Core.ActivityLog.EditPackName", packName);

                    return PackNameBulkOutcome.Changed;
                }

            case PackNameBulkAction.Delete:
                {
                    //the packname's subsidy assignments and cart lines go with it, through the delete event, as for the single delete
                    await _packNameService.DeletePackNameAsync(packName);
                    await LogActivityAsync("DeletePackName", "Plugins.CW.Core.ActivityLog.DeletePackName", packName);

                    return PackNameBulkOutcome.Changed;
                }

            case PackNameBulkAction.Move:
                {
                    if (targetList != null && packName.PackNameListId == targetList.Id)
                        return PackNameBulkOutcome.Unchanged;

                    //the target was refused since the action started: nothing more is moved into it
                    if (targetRefusal != null)
                    {
                        skipped.Add(new PackNameBulkSkip(packName.Id, packName.Name, targetRefusal));

                        return PackNameBulkOutcome.Skipped;
                    }

                    //one packname per name in a list, checked here, inside the write's transaction, against the list as it is
                    //now and against the packnames this action has already moved into it
                    var name = packName.Name?.Trim() ?? string.Empty;

                    if ((name.Length > 0 && movedNames.Contains(name))
                        || await _packNameService.PackNameNameExistsAsync(targetList.Id, packName.Name, packName.Id))
                    {
                        skipped.Add(new PackNameBulkSkip(packName.Id, packName.Name,
                            string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.PackNames.Bulk.Skip.NameTaken"), targetList.Name)));

                        return PackNameBulkOutcome.Skipped;
                    }

                    packName.PackNameListId = targetList.Id;
                    await _packNameService.UpdatePackNameAsync(packName);
                    await LogActivityAsync("EditPackName", "Plugins.CW.Core.ActivityLog.EditPackName", packName);

                    if (name.Length > 0)
                        movedNames.Add(name);

                    return PackNameBulkOutcome.Changed;
                }

            default:
                throw new ArgumentOutOfRangeException(nameof(request), request.Action, "Unknown packname bulk action");
        }
    }

    #endregion

    #region Methods

    public virtual async Task<PackNameBulkResult> ApplyAsync(PackNameBulkRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = new PackNameBulkResult();

        if (!Enum.IsDefined(request.Action))
        {
            result.RefusedMessage = await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.PackNames.Bulk.UnknownAction");
            return result;
        }

        var ids = (request.PackNameIds ?? Array.Empty<int>()).Where(id => id > 0).Distinct().ToList();

        if (ids.Count == 0)
        {
            result.RefusedMessage = await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.PackNames.Bulk.NothingSelected");
            return result;
        }

        if (ids.Count > PackNameBulkDefaults.MaxPackNames)
        {
            result.RefusedMessage = string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.PackNames.Bulk.TooMany"), PackNameBulkDefaults.MaxPackNames);
            return result;
        }

        if (request.Action == PackNameBulkAction.Move)
        {
            var refusal = await CheckMoveTargetAsync(await _packNameService.GetPackNameListByIdAsync(request.TargetPackNameListId), request.StoreId);

            if (refusal != null)
            {
                result.RefusedMessage = refusal;
                return result;
            }
        }

        var movedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var processed = 0;

        try
        {
            foreach (var batch in ids.Chunk(PackNameBulkDefaults.WriteBatchSize))
            {
                var changed = 0;
                var unchanged = 0;
                var skipped = new List<PackNameBulkSkip>();
                IList<PackName> packNames;

                //everything inside runs in sequence on purpose: two connections open at once would promote the transaction to
                //a distributed one. The scope commits when it is disposed after Complete, so the batch is counted only after that
                using (var scope = PackNameTransaction.Begin(TimeSpan.FromMinutes(1)))
                {
                    //read inside the transaction, batch by batch: a packname edited, or a list switched off, while an earlier
                    //batch was written is acted on as it is now. The repository returns deleted packnames too; ApplyOneAsync
                    //skips them
                    packNames = await _packNameService.GetPackNamesByIdsAsync(batch);
                    var lists = (await _packNameService.GetPackNameListsByIdsAsync(packNames.Select(packName => packName.PackNameListId).Distinct().ToArray()))
                        .ToDictionary(list => list.Id);

                    PackNameList targetList = null;
                    string targetRefusal = null;

                    if (request.Action == PackNameBulkAction.Move)
                    {
                        //read from the database, not the list cache, so a list switched off meanwhile is seen as it is now
                        targetList = (await _packNameService.GetPackNameListsByIdsAsync([request.TargetPackNameListId])).FirstOrDefault();
                        targetRefusal = await CheckMoveTargetAsync(targetList, request.StoreId);
                    }

                    var byId = packNames.ToDictionary(packName => packName.Id);

                    foreach (var id in batch)
                    {
                        var packName = byId.GetValueOrDefault(id);
                        var outcome = await ApplyOneAsync(id, packName, lists, request, targetList, targetRefusal, movedNames, skipped);

                        if (outcome == PackNameBulkOutcome.Changed)
                            changed++;
                        else if (outcome == PackNameBulkOutcome.Unchanged)
                            unchanged++;
                    }

                    scope.Complete();
                }

                result.Changed += changed;
                result.Unchanged += unchanged;

                foreach (var skip in skipped)
                    result.Skipped.Add(skip);

                processed += batch.Length;

                if (changed > 0)
                {
                    //the batch is committed: a cache that cannot be cleared expires on its own, so the batches still to run go ahead
                    try
                    {
                        await ClearPackNameCachesAsync(packNames, request.Action == PackNameBulkAction.Delete);
                    }
                    catch (Exception exception)
                    {
                        await _logger.WarningAsync($"Bulk action {request.Action} on packnames of store {request.StoreId}: the packname caches could not be cleared after a batch",
                            exception, await _workContext.GetCurrentCustomerAsync());
                    }
                }
            }
        }
        catch (Exception exception)
        {
            //logged here, once the batch's transaction is gone: a log entry written inside it would be rolled back with it
            result.NotProcessed = ids.Count - processed;
            result.StoppedMessage = string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.PackNames.Bulk.Stopped"), result.NotProcessed);

            await _logger.ErrorAsync($"Bulk action {request.Action} on packnames of store {request.StoreId} stopped: {result.NotProcessed} of {ids.Count} packname(s) were not processed",
                exception, await _workContext.GetCurrentCustomerAsync());
        }

        return result;
    }

    public virtual async Task<IList<PackNameBulkMoveTarget>> GetMoveTargetsAsync(int storeId)
    {
        var lists = await _packNameService.GetActivePackNameListsByStoreIdAsync(storeId);
        var branches = await _branchService.GetAllBranchesByStoreIdAsync(storeId);
        var branchesById = branches.ToDictionary(branch => branch.Id);
        var targets = new List<PackNameBulkMoveTarget>();

        foreach (var list in lists)
        {
            if (list.BranchId is not > 0)
            {
                targets.Add(new PackNameBulkMoveTarget(list.Id, list.Name));
                continue;
            }

            //a list in a branch that is switched off takes no packnames
            if (!branchesById.TryGetValue(list.BranchId.Value, out var branch) || !branch.Active)
                continue;

            var path = await _branchService.GetFormattedBreadCrumbAsync(storeId, branch, branches);
            targets.Add(new PackNameBulkMoveTarget(list.Id, $"{path} >> {list.Name}"));
        }

        return targets.OrderBy(target => target.Text, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    public virtual async Task<byte[]> ExportToXlsxAsync(int storeId, IList<PackNameDTO> packNames)
    {
        ArgumentNullException.ThrowIfNull(packNames);

        var settings = await _settingService.LoadSettingAsync<MultiStoreSettings>(storeId);

        async Task<string> FieldAsync(string field) =>
            await _localizationService.GetResourceAsync($"Plugins.CW.Core.Admin.PackNames.PackName.Field.{field}");

        var yes = await _localizationService.GetResourceAsync("Admin.Common.Yes");
        var no = await _localizationService.GetResourceAsync("Admin.Common.No");
        var genders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["I"] = await FieldAsync("Gender.Mixed"),
            ["M"] = await FieldAsync("Gender.Male"),
            ["F"] = await FieldAsync("Gender.Female"),
            ["N"] = await FieldAsync("Gender.Neutral")
        };

        //the columns of the admin grid the store shows, plus the list, the account's e-mail and the active flag
        var columns = new List<PackNameExportColumn>();

        if (settings.EmployeeNumberEnabled)
            columns.Add(new PackNameExportColumn(await FieldAsync("EmployeeNumber"), packName => packName.EmployeeNumber));

        columns.Add(new PackNameExportColumn(await FieldAsync("Name"), packName => packName.Name));

        if (settings.PrintNameEnabled)
            columns.Add(new PackNameExportColumn(await FieldAsync("PrintName"), packName => packName.PrintName));

        columns.Add(new PackNameExportColumn(await FieldAsync("Branch"), packName => packName.Branch));
        columns.Add(new PackNameExportColumn(await FieldAsync("PackNameList"), packName => packName.PackNameListName));
        columns.Add(new PackNameExportColumn(await FieldAsync("Department"), packName => packName.Department));
        columns.Add(new PackNameExportColumn(await FieldAsync("CostCenter"), packName => packName.CostCenter));

        if (settings.GenderEnabled)
            columns.Add(new PackNameExportColumn(await FieldAsync("Gender"), packName => !string.IsNullOrEmpty(packName.Gender) && genders.TryGetValue(packName.Gender, out var gender) ? gender : packName.Gender));

        if (settings.POEntryEnabled)
            columns.Add(new PackNameExportColumn(await FieldAsync("POEntry"), packName => packName.POEntry));

        columns.Add(new PackNameExportColumn(await FieldAsync("Group"), packName => packName.Group ? yes : no));
        columns.Add(new PackNameExportColumn(await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.PackNames.SearchEmail"), packName => packName.Email));
        columns.Add(new PackNameExportColumn(await FieldAsync("Manager"), packName => packName.Manager ? yes : no));
        columns.Add(new PackNameExportColumn(await FieldAsync("Active"), packName => packName.Active ? yes : no));

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("PackNames");

        for (var index = 0; index < columns.Count; index++)
        {
            var header = sheet.Cell(1, index + 1);

            header.Value = columns[index].Header;
            header.Style.Font.Bold = true;
            header.Style.Fill.BackgroundColor = XLColor.FromHtml("#DDEBF7");

            sheet.Column(index + 1).Width = Math.Max(14, columns[index].Header.Length + 4);
        }

        //every value is written as text, so an employee number such as 007 keeps its zeros and a value starting with = is
        //not read as a formula
        for (var row = 0; row < packNames.Count; row++)
        {
            for (var index = 0; index < columns.Count; index++)
                sheet.Cell(row + 2, index + 1).Value = columns[index].Value(packNames[row]) ?? string.Empty;
        }

        sheet.SheetView.FreezeRows(1);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);

        return stream.ToArray();
    }

    #endregion
}