using Nop.Core.Domain.Common;
using Nop.Core.Domain.Directory;
using Nop.Plugin.CW.Core.Domain.Branches;
using Nop.Plugin.CW.Core.Services.Branches;
using Nop.Plugin.CW.Core.Services.PackNames;
using Nop.Plugin.Integration.Odoo.Models.Responses;
using Nop.Services.Common;
using Nop.Services.Directory;

namespace Nop.Plugin.Integration.Odoo.Services;

/// <summary>
/// Synchronises Odoo delivery addresses (res.partner, type=delivery) into CW.Core Branches,
/// driven by the new <c>x_store</c> model.
///
/// For each active Odoo store (x_store):
///   - resolve the mapped nopCommerce store (via <see cref="IOdooStoreMapService"/>; run store sync first),
///   - upsert every company in x_studio_companies (res.partner, type company) as an invoiceable Head
///     Office: a top-level CW.Core <see cref="Branch"/> with the company's address,
///   - upsert each of that company's delivery addresses (res.partner, type delivery) as a Branch whose
///     parent is the Head Office.
/// Every branch is keyed on CardCode = its Odoo res.partner id, and written through
/// <see cref="IBranchService"/> / <see cref="IAddressService"/>. A new branch then gets what CW.Core gives a
/// branch created in the admin (<see cref="IPackNameProvisioningService.ProvisionNewBranchAsync"/>: its default
/// packname list, when the store uses branches as packname lists and has the PackName feature on).
/// </summary>
public class BranchSyncService : IBranchSyncService
{
    #region Fields

    private readonly IOdooApiService _odooApiService;
    private readonly IOdooLogService _odooLogService;
    private readonly IOdooStoreMapService _storeMapService;
    private readonly IAddressService _addressService;
    private readonly ICountryService _countryService;
    private readonly IStateProvinceService _stateProvinceService;
    private readonly IBranchService _branchService;
    private readonly IPackNameProvisioningService _packNameProvisioningService;

    #endregion

    #region Ctor

    public BranchSyncService(
        IOdooApiService odooApiService,
        IOdooLogService odooLogService,
        IOdooStoreMapService storeMapService,
        IAddressService addressService,
        ICountryService countryService,
        IStateProvinceService stateProvinceService,
        IBranchService branchService,
        IPackNameProvisioningService packNameProvisioningService)
    {
        _odooApiService = odooApiService;
        _odooLogService = odooLogService;
        _storeMapService = storeMapService;
        _addressService = addressService;
        _countryService = countryService;
        _stateProvinceService = stateProvinceService;
        _branchService = branchService;
        _packNameProvisioningService = packNameProvisioningService;
    }

    #endregion

    #region Utilities

    /// <param name="parentBranchId">Null for a head office (company); the head office's branch id for a
    /// delivery address.</param>
    private async Task<Branch> CreateBranchAsync(int eventId, OdooPartnerResponse partner, string cardCode, int storeId,
        int? parentBranchId, IList<Country> allCountries, IDictionary<int, IList<StateProvince>> statesByCountryId)
    {
        var branch = new Branch
        {
            BranchCode = $"ODOO_{partner.Id}",
            Name = partner.Name,
            StoreId = storeId,
            ParentBranchId = parentBranchId,
            CardCode = cardCode,
            Active = partner.Active,
            CreatedOnUtc = DateTime.UtcNow,
            UpdatedOnUtc = DateTime.UtcNow,
            Deleted = false
        };

        await _branchService.InsertBranchAsync(branch);

        await LogInfoAsync(
            $"Created branch '{branch.Name}' (BranchCode: {branch.BranchCode}) from Odoo partner {partner.Id}.",
            eventId, "Branch", partner.Id, branch.Id);

        await SyncAddressAsync(eventId, branch, partner, allCountries, statesByCountryId);

        // The store's automation for a new branch, as when an admin creates one; it checks the setting and the
        // PackName feature itself.
        await _packNameProvisioningService.ProvisionNewBranchAsync(branch);

        return branch;
    }

    /// <param name="parentBranchId">Null for a head office (company); the head office's branch id for a
    /// delivery address. Odoo is the source of the hierarchy, so it is re-applied on every update.</param>
    private async Task UpdateBranchAsync(int eventId, Branch branch, OdooPartnerResponse partner, int? parentBranchId,
        IList<Country> allCountries, IDictionary<int, IList<StateProvince>> statesByCountryId)
    {
        branch.Name = partner.Name;
        branch.Active = partner.Active;
        branch.ParentBranchId = parentBranchId;
        branch.UpdatedOnUtc = DateTime.UtcNow;

        await _branchService.UpdateBranchAsync(branch);

        await LogInfoAsync(
            $"Updated branch '{branch.Name}' (ID {branch.Id}) from Odoo partner {partner.Id}.",
            eventId, "Branch", partner.Id, branch.Id);

        await SyncAddressAsync(eventId, branch, partner, allCountries, statesByCountryId);
    }

    /// <summary>
    /// Creates or updates the nopCommerce <see cref="Address"/> for a branch and ensures the
    /// branch-address link exists. The first address already linked to the branch is reused
    /// (updated in place); otherwise a new address is inserted and linked.
    /// </summary>
    private async Task SyncAddressAsync(int eventId, Branch branch, OdooPartnerResponse partner,
        IList<Country> allCountries, IDictionary<int, IList<StateProvince>> statesByCountryId)
    {
        // Resolve country and state from Odoo Many2One fields.
        int? countryId = null;
        int? stateProvinceId = null;

        if (partner.CountryId is not null)
        {
            var matchedCountry = allCountries.FirstOrDefault(c =>
                string.Equals(c.Name, partner.CountryId.Name, StringComparison.OrdinalIgnoreCase));
            countryId = matchedCountry?.Id;

            if (countryId.HasValue && partner.StateId is not null)
            {
                // Odoo state names look like "New South Wales (AU)" – strip the country suffix.
                var odooStateName = partner.StateId.Name;
                var parenthesisIndex = odooStateName.LastIndexOf('(');
                if (parenthesisIndex > 0)
                    odooStateName = odooStateName[..parenthesisIndex].TrimEnd();

                if (!statesByCountryId.TryGetValue(countryId.Value, out var states))
                {
                    states = await _stateProvinceService.GetStateProvincesByCountryIdAsync(countryId.Value, showHidden: true);
                    statesByCountryId[countryId.Value] = states;
                }

                var state = states.FirstOrDefault(s =>
                    string.Equals(s.Name, odooStateName, StringComparison.OrdinalIgnoreCase));
                stateProvinceId = state?.Id;
            }
        }

        var existingAddresses = await _branchService.GetAddressesByBranchIdAsync(branch.Id);
        var address = existingAddresses.FirstOrDefault();

        if (address is null)
        {
            address = new Address
            {
                FirstName = string.Empty,
                LastName = string.Empty,
                Company = partner.Name,
                Address1 = partner.Street,
                Address2 = partner.Street2,
                City = partner.City,
                ZipPostalCode = partner.Zip,
                CountryId = countryId,
                StateProvinceId = stateProvinceId,
                PhoneNumber = partner.Phone,
                Email = partner.Email,
                CreatedOnUtc = DateTime.UtcNow
            };

            await _addressService.InsertAddressAsync(address);
            await _branchService.InsertBranchAddressAsync(branch, address);

            await LogInfoAsync($"Created address for branch '{branch.Name}' (ID {branch.Id}).",
                eventId, "Branch", partner.Id, branch.Id);
        }
        else
        {
            address.Company = partner.Name;
            address.Address1 = partner.Street;
            address.Address2 = partner.Street2;
            address.City = partner.City;
            address.ZipPostalCode = partner.Zip;
            address.CountryId = countryId;
            address.StateProvinceId = stateProvinceId;
            address.PhoneNumber = partner.Phone;
            address.Email = partner.Email;

            await _addressService.UpdateAddressAsync(address);

            await LogInfoAsync($"Updated address for branch '{branch.Name}' (ID {branch.Id}).",
                eventId, "Branch", partner.Id, branch.Id);
        }
    }

    private Task LogInfoAsync(string message, int eventId, string entityType, int? odooId, int? nopId) =>
        _odooLogService.InformationAsync(message, eventId, null, entityType, odooId, nopId);

    private Task LogWarningAsync(string message, int eventId, string entityType, int? odooId, int? nopId) =>
        _odooLogService.WarningAsync(message, eventId, null, entityType, odooId, nopId);

    private Task LogErrorAsync(string message, int eventId, string entityType, int? odooId, int? nopId) =>
        _odooLogService.ErrorAsync(message, eventId, null, entityType, odooId, nopId);

    #endregion

    #region Methods

    /// <inheritdoc/>
    public async Task<BranchSyncResult> SyncAllBranchesAsync(int logEventId)
    {
        // Every Odoo partner handled — each company (as its head office) and each delivery address — is
        // counted exactly once (see SyncTally). Stores are only reported in the summary line.
        var tally = new SyncTally();
        var storesProcessed = 0;

        try
        {
            await LogInfoAsync("Starting branch sync across all Odoo stores.", logEventId, "Branch", null, null);

            // Pre-load country lookups; states are loaded lazily per country and cached.
            var allCountries = await _countryService.GetAllCountriesAsync(showHidden: true);
            var statesByCountryId = new Dictionary<int, IList<StateProvince>>();

            // Fetch all Odoo stores (x_store) with their linked companies.
            var odooStores = await _odooApiService.FetchStoresAsync();
            if (odooStores.Count == 0)
            {
                const string msg = "No Odoo stores (x_store) found. Branch sync skipped.";
                await LogWarningAsync(msg, logEventId, "Branch", null, null);
                return new BranchSyncResult(false, 0, 0, 0, msg);
            }

            await LogInfoAsync($"Found {odooStores.Count} Odoo store(s) to evaluate.", logEventId, "Branch", null, null);

            foreach (var odooStore in odooStores)
            {
                try
                {
                    // Only active stores are synced (matches store sync policy).
                    if (!odooStore.XActive)
                        continue;

                    // Resolve the mapped nopCommerce store id (populated by store sync).
                    var nopStoreId = await _storeMapService.GetNopStoreIdAsync(odooStore.Id);
                    if (nopStoreId is null)
                    {
                        await LogInfoAsync(
                            $"Odoo store '{odooStore.XName}' (ID {odooStore.Id}) is not mapped to a nopCommerce store (run store sync first). Skipping its branches.",
                            logEventId, "Branch", odooStore.Id, null);
                        continue;
                    }

                    var companyIds = odooStore.XStudioCompanies ?? new List<int>();
                    if (companyIds.Count == 0)
                    {
                        await LogInfoAsync(
                            $"Odoo store '{odooStore.XName}' (ID {odooStore.Id}) has no linked companies.",
                            logEventId, "Branch", odooStore.Id, null);
                        storesProcessed++;
                        continue;
                    }

                    await LogInfoAsync(
                        $"Processing Odoo store '{odooStore.XName}' (ID {odooStore.Id}) → nop store ID {nopStoreId.Value}; {companyIds.Count} company/companies.",
                        logEventId, "Branch", odooStore.Id, null);

                    // Existing branches for this store, keyed by CardCode (= Odoo partner id).
                    var existingBranches = await _branchService.GetAllBranchesByStoreIdAsync(nopStoreId.Value);
                    var branchesByCardCode = existingBranches
                        .Where(b => !string.IsNullOrEmpty(b.CardCode))
                        .GroupBy(b => b.CardCode)
                        .ToDictionary(g => g.Key, g => g.First());

                    // Each company (res.partner, type company) is an invoiceable Head Office: a top-level
                    // branch. Its delivery addresses (type delivery) are branches beneath it.
                    var companies = await _odooApiService.GetPartnersByIdsAsync(companyIds);

                    foreach (var missingId in companyIds.Except(companies.Select(c => c.Id)))
                        await LogWarningAsync(
                            $"Odoo store '{odooStore.XName}' lists company {missingId}, which no longer exists in Odoo. Skipped.",
                            logEventId, "Branch", missingId, null);

                    foreach (var company in companies)
                    {
                        // The company itself, counted once as its head office.
                        var companyOutcome = SyncOutcome.Skipped;
                        Branch headOffice = null;
                        try
                        {
                            var headOfficeCode = company.Id.ToString();
                            branchesByCardCode.TryGetValue(headOfficeCode, out headOffice);

                            if (headOffice == null && !company.Active)
                            {
                                // Archived and never synced: nothing to create, and its addresses have no parent.
                                await LogInfoAsync(
                                    $"Odoo company '{company.Name}' (ID {company.Id}) is archived and has no head office in NOP. Skipped with its delivery addresses.",
                                    logEventId, "Branch", company.Id, null);
                                continue;
                            }

                            if (headOffice == null)
                            {
                                headOffice = await CreateBranchAsync(logEventId, company, headOfficeCode, nopStoreId.Value, null, allCountries, statesByCountryId);
                                branchesByCardCode[headOfficeCode] = headOffice;
                                companyOutcome = SyncOutcome.Created;
                            }
                            else
                            {
                                // Also deactivates the head office when the company is archived in Odoo.
                                await UpdateBranchAsync(logEventId, headOffice, company, null, allCountries, statesByCountryId);
                                companyOutcome = SyncOutcome.Updated;
                            }
                        }
                        catch (Exception ex)
                        {
                            await LogErrorAsync(
                                $"Failed to sync Odoo company '{company.Name}' (ID {company.Id}) as a head office: {ex.Message}",
                                logEventId, "Branch", company.Id, null);
                        }
                        finally
                        {
                            tally.Add(companyOutcome);
                        }

                        // Its delivery addresses — only when a head office exists to parent them.
                        if (headOffice == null)
                            continue;

                        List<OdooPartnerResponse> deliveryAddresses;
                        try
                        {
                            deliveryAddresses = await _odooApiService.GetDeliveryAddressesAsync(company.Id);
                        }
                        catch (Exception ex)
                        {
                            // Nothing was retrieved, so there is nothing to count.
                            await LogErrorAsync(
                                $"Failed to fetch delivery addresses of Odoo company '{company.Name}' (ID {company.Id}): {ex.Message}",
                                logEventId, "Branch", company.Id, null);
                            continue;
                        }

                        foreach (var partner in deliveryAddresses)
                        {
                            var outcome = SyncOutcome.Skipped;
                            try
                            {
                                var cardCode = partner.Id.ToString();
                                if (branchesByCardCode.TryGetValue(cardCode, out var existingBranch))
                                {
                                    // Also deactivates the branch when the address is archived in Odoo, and
                                    // re-parents branches created before head offices existed.
                                    await UpdateBranchAsync(logEventId, existingBranch, partner, headOffice.Id, allCountries, statesByCountryId);
                                    outcome = SyncOutcome.Updated;
                                }
                                else if (partner.Active)
                                {
                                    var newBranch = await CreateBranchAsync(logEventId, partner, cardCode, nopStoreId.Value, headOffice.Id, allCountries, statesByCountryId);
                                    branchesByCardCode[cardCode] = newBranch;
                                    outcome = SyncOutcome.Created;
                                }
                                // else: archived and never synced — nothing to create, counted as skipped.
                            }
                            catch (Exception ex)
                            {
                                await LogErrorAsync(
                                    $"Failed to sync delivery address '{partner.Name}' (Odoo ID {partner.Id}): {ex.Message}",
                                    logEventId, "Branch", partner.Id, null);
                            }
                            finally
                            {
                                tally.Add(outcome);
                            }
                        }
                    }

                    storesProcessed++;
                }
                catch (Exception ex)
                {
                    await LogErrorAsync(
                        $"Failed to process Odoo store '{odooStore.XName}' (ID {odooStore.Id}): {ex.Message}",
                        logEventId, "Branch", odooStore.Id, null);
                }
            }

            var result = new BranchSyncResult(true, tally.Created, tally.Updated, tally.Skipped, null);
            await LogInfoAsync(
                $"Branch sync complete: {storesProcessed} store(s) processed; {result.Processed} Odoo partner(s): {result.Created} created, {result.Updated} updated, {result.Skipped} skipped.",
                logEventId, "Branch", null, null);

            return result;
        }
        catch (Exception ex)
        {
            await LogErrorAsync($"Branch sync failed: {ex.Message}", logEventId, "Branch", null, null);
            return new BranchSyncResult(false, tally.Created, tally.Updated, tally.Skipped, ex.Message);
        }
    }

    #endregion
}
