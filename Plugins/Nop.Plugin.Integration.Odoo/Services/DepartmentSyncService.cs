using Nop.Plugin.CW.Core.Domain.MultiStores;
using Nop.Plugin.CW.Core.Services.Customers;
using Nop.Plugin.CW.Core.Services.MultiStores;

namespace Nop.Plugin.Integration.Odoo.Services;

/// <summary>
/// Creates/updates a CW.Core <see cref="Department"/> (and its <c>DEP_</c> customer role) for each
/// active Odoo <c>estore.department</c>, and deactivates the NOP department of an archived one.
///
/// Rules (see spec §6):
///   - resolve the parent nop store from <c>store_id</c> via <see cref="IOdooStoreMapService"/>
///     (run store sync first); skip store-less departments,
///   - archived in Odoo → deactivate the matching NOP department (never create one),
///   - match an existing department by (StoreId, Name); rename in Odoo creates a new one,
///   - reuse CW.Core's <see cref="ICustomerOverrideService.SaveDepartmentCustomerRole"/> +
///     <see cref="IMultiStoreService"/> — the DEP_ role naming and cache logic are NOT duplicated,
///   - departments upsert every run (no revision skip — low volume).
/// </summary>
public class DepartmentSyncService : IDepartmentSyncService
{
    #region Fields

    private readonly IOdooApiService _odooApiService;
    private readonly IOdooLogService _odooLogService;
    private readonly IOdooStoreMapService _storeMapService;
    private readonly ICustomerOverrideService _customerOverrideService;
    private readonly IMultiStoreService _multiStoreService;

    #endregion

    #region Ctor

    public DepartmentSyncService(
        IOdooApiService odooApiService,
        IOdooLogService odooLogService,
        IOdooStoreMapService storeMapService,
        ICustomerOverrideService customerOverrideService,
        IMultiStoreService multiStoreService)
    {
        _odooApiService = odooApiService;
        _odooLogService = odooLogService;
        _storeMapService = storeMapService;
        _customerOverrideService = customerOverrideService;
        _multiStoreService = multiStoreService;
    }

    #endregion

    #region Methods

    /// <inheritdoc/>
    public async Task<DepartmentSyncResult> SyncDepartmentsAsync(int logEventId)
    {
        // Every retrieved estore.department is counted exactly once (see SyncTally).
        var tally = new SyncTally();

        var odooDepartments = await _odooApiService.FetchDepartmentsAsync();
        if (odooDepartments.Count == 0)
        {
            await _odooLogService.WarningAsync("No Odoo departments (estore.department) found. Department sync skipped.", logEventId, entityType: "Department");
            return new DepartmentSyncResult(0, 0, 0);
        }

        await _odooLogService.InformationAsync($"Found {odooDepartments.Count} Odoo department(s) to sync.", logEventId, entityType: "Department");

        foreach (var odooDept in odooDepartments)
        {
            var outcome = SyncOutcome.Skipped;
            try
            {
                // The parent store many2one is authoritative (the dept name is NOT a routing key).
                if (odooDept.Store is null)
                {
                    await _odooLogService.WarningAsync(
                        $"Odoo department '{odooDept.Name}' (ID {odooDept.Id}) has no store set. Skipping.",
                        logEventId, entityType: "Department", odooEntityId: odooDept.Id);
                    continue;
                }

                var nopStoreId = await _storeMapService.GetNopStoreIdAsync(odooDept.Store.Id);
                if (nopStoreId is null)
                {
                    await _odooLogService.WarningAsync(
                        $"Odoo department '{odooDept.Name}' (ID {odooDept.Id}) → Odoo store {odooDept.Store.Id} is not mapped to a nopCommerce store (run store sync first). Skipping.",
                        logEventId, entityType: "Department", odooEntityId: odooDept.Id);
                    continue;
                }

                var name = (odooDept.Name ?? odooDept.DisplayName)?.Trim();
                if (string.IsNullOrWhiteSpace(name))
                {
                    await _odooLogService.WarningAsync(
                        $"Odoo department (ID {odooDept.Id}) has no name. Skipping.",
                        logEventId, entityType: "Department", odooEntityId: odooDept.Id);
                    continue;
                }

                // Match an existing department by (StoreId, Name).
                var existing = await _multiStoreService.GetDepartmentByStoreAndNameAsync(nopStoreId.Value, name);

                // Archived in Odoo: deactivate the matching NOP department; never create one.
                if (!odooDept.Active)
                {
                    if (existing is { Active: true })
                    {
                        existing.Active = false;
                        await _multiStoreService.UpdateDepartmentAsync(existing);

                        await _odooLogService.InformationAsync(
                            $"Deactivated department '{existing.Name}' (ID {existing.Id}, store {nopStoreId.Value}) — archived in Odoo (department {odooDept.Id}).",
                            logEventId, entityType: "Department", odooEntityId: odooDept.Id, nopEntityId: existing.Id);
                        outcome = SyncOutcome.Updated;
                    }
                    else
                    {
                        await _odooLogService.InformationAsync(
                            $"Odoo department '{name}' (ID {odooDept.Id}) is archived and has no active NOP department. Skipped.",
                            logEventId, entityType: "Department", odooEntityId: odooDept.Id);
                    }
                    continue;
                }

                if (existing != null)
                {
                    existing.Name = name;
                    existing.Active = odooDept.Active;

                    // Keeps the DEP_{storeId}_{name} role in sync (CW.Core owns this logic).
                    await _customerOverrideService.SaveDepartmentCustomerRole(existing);
                    await _multiStoreService.UpdateDepartmentAsync(existing);

                    await _odooLogService.InformationAsync(
                        $"Updated department '{existing.Name}' (ID {existing.Id}, store {nopStoreId.Value}) from Odoo department {odooDept.Id}.",
                        logEventId, entityType: "Department", odooEntityId: odooDept.Id, nopEntityId: existing.Id);
                    outcome = SyncOutcome.Updated;
                }
                else
                {
                    var department = new Department
                    {
                        StoreId = nopStoreId.Value,
                        Name = name,
                        Active = odooDept.Active
                    };

                    // Creates the DEP_ customer role and sets department.CustomerRoleId (CW.Core logic).
                    await _customerOverrideService.SaveDepartmentCustomerRole(department);
                    await _multiStoreService.InsertDepartmentAsync(department);

                    await _odooLogService.InformationAsync(
                        $"Created department '{department.Name}' (ID {department.Id}, store {nopStoreId.Value}, role {department.CustomerRoleId}) from Odoo department {odooDept.Id}.",
                        logEventId, entityType: "Department", odooEntityId: odooDept.Id, nopEntityId: department.Id);
                    outcome = SyncOutcome.Created;
                }
            }
            catch (Exception ex)
            {
                await _odooLogService.ErrorAsync(
                    $"Error syncing Odoo department {odooDept.Id} ('{odooDept.Name}'): {ex.Message}",
                    logEventId, fullMessage: ex.ToString(), entityType: "Department", odooEntityId: odooDept.Id);
            }
            finally
            {
                tally.Add(outcome);
            }
        }

        var result = new DepartmentSyncResult(tally.Created, tally.Updated, tally.Skipped);
        await _odooLogService.InformationAsync(
            $"Department sync complete: {result.Created} created, {result.Updated} updated, {result.Skipped} skipped, {result.Processed} processed.",
            logEventId, entityType: "Department");

        return result;
    }

    #endregion
}
