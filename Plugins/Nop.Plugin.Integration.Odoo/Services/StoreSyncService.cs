using Nop.Core.Domain.Stores;
using Nop.Plugin.Integration.Odoo.Models.Responses;
using Nop.Services.Stores;

namespace Nop.Plugin.Integration.Odoo.Services;

/// <summary>
/// Creates/updates a nopCommerce <see cref="Store"/> for each active Odoo <c>x_store</c>.
///
/// Rules (see spec §5):
///   - only active stores are synced; archived ones are logged, NOP stores are never deleted,
///   - stores with no URL are still created (empty Url/Hosts), but an empty Odoo URL never blanks
///     an existing store's Url/Hosts; an Odoo host is added to, not substituted for, the host list,
///   - DisplayOrder and SslEnabled are set on create only; a new store never sorts ahead of the
///     existing ones (nop's fallback store is the first by DisplayOrder),
///   - identity/incremental via <see cref="OdooStoreMapping"/> (skip when revision unchanged),
///   - first run reconciles to an existing NOP store by Host, then by Name, else creates.
/// </summary>
public class StoreSyncService : IStoreSyncService
{
    #region Fields

    private readonly IOdooApiService _odooApiService;
    private readonly IOdooLogService _odooLogService;
    private readonly IOdooStoreMapService _storeMapService;
    private readonly IStoreService _storeService;

    #endregion

    #region Ctor

    public StoreSyncService(
        IOdooApiService odooApiService,
        IOdooLogService odooLogService,
        IOdooStoreMapService storeMapService,
        IStoreService storeService)
    {
        _odooApiService = odooApiService;
        _odooLogService = odooLogService;
        _storeMapService = storeMapService;
        _storeService = storeService;
    }

    #endregion

    #region Utilities

    /// <summary>
    /// Trims the Odoo URL and ensures a trailing "/" (as nop's admin does when saving a store).
    /// Returns empty for a null/blank URL.
    /// </summary>
    private static string NormaliseUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return string.Empty;

        var value = url.Trim();
        return value.EndsWith('/') ? value : value + "/";
    }

    /// <summary>
    /// Extracts the host (with optional port) from a URL: strips the scheme and any path/trailing
    /// slash. Returns empty for a null/blank URL.
    /// </summary>
    private static string ParseHost(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return string.Empty;

        var value = url.Trim();

        var schemeIndex = value.IndexOf("://", StringComparison.Ordinal);
        if (schemeIndex >= 0)
            value = value[(schemeIndex + 3)..];

        var slashIndex = value.IndexOf('/');
        if (slashIndex >= 0)
            value = value[..slashIndex];

        return value.Trim();
    }

    /// <summary>
    /// True when the host appears in the store's comma/semicolon-separated Hosts list.
    /// </summary>
    private static bool HostMatches(Store store, string host)
    {
        if (string.IsNullOrEmpty(store.Hosts))
            return false;

        return store.Hosts
            .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(h => h.Trim())
            .Any(h => string.Equals(h, host, StringComparison.OrdinalIgnoreCase));
    }

    #endregion

    #region Methods

    /// <inheritdoc/>
    public async Task<StoreSyncResult> SyncStoresAsync(int logEventId)
    {
        // Every retrieved x_store is counted exactly once (see SyncTally).
        var tally = new SyncTally();

        var odooStores = await _odooApiService.FetchStoresAsync();
        if (odooStores.Count == 0)
        {
            await _odooLogService.WarningAsync("No Odoo stores (x_store) found. Store sync skipped.", logEventId, entityType: "Store");
            return new StoreSyncResult(0, 0, 0);
        }

        await _odooLogService.InformationAsync($"Found {odooStores.Count} Odoo store(s) to sync.", logEventId, entityType: "Store");

        // Snapshot of nop stores for first-run reconciliation; newly created stores are appended.
        var nopStores = (await _storeService.GetAllStoresAsync()).ToList();

        // Which Odoo store already owns each nop store (NopStoreId is unique), kept current during the run.
        var odooStoreIdByNopStoreId = (await _storeMapService.GetNopStoreIdLookupAsync())
            .ToDictionary(kv => kv.Value, kv => kv.Key);

        foreach (var odooStore in odooStores)
        {
            var outcome = SyncOutcome.Skipped;
            try
            {
                if (!odooStore.XActive)
                {                    await _odooLogService.InformationAsync(
                        $"Odoo store '{odooStore.XName}' (ID {odooStore.Id}) is archived — NOP stores are never deleted; left unchanged.",
                        logEventId, entityType: "Store", odooEntityId: odooStore.Id);
                    continue;
                }

                var mapping = await _storeMapService.GetByOdooStoreIdAsync(odooStore.Id);

                // Resolve the mapped nop store first: if it no longer exists in NOP, fall through to
                // reconciliation below instead of being skipped as "unchanged" forever.
                var nopStore = mapping != null
                    ? nopStores.FirstOrDefault(s => s.Id == mapping.NopStoreId)
                    : null;

                // Incremental: skip only when BOTH the revision and write_date are unchanged. What bumps
                // x_studio_sync_revision is configured in Odoo and not guaranteed to cover every edit;
                // write_date is maintained by the ORM on every write. A mapping with no recorded
                // write_date (written before the column existed) is never skipped.
                if (mapping != null && nopStore != null
                    && mapping.LastSyncedRevision == odooStore.XStudioSyncRevision
                    && mapping.LastSyncedWriteDate != null
                    && string.Equals(mapping.LastSyncedWriteDate, odooStore.WriteDate, StringComparison.Ordinal))
                {                    await _odooLogService.InformationAsync(
                        $"Odoo store '{odooStore.XName}' (ID {odooStore.Id}) unchanged (revision {odooStore.XStudioSyncRevision}, written {odooStore.WriteDate}). Skipped.",
                        logEventId, entityType: "Store", odooEntityId: odooStore.Id);
                    continue;
                }

                var storeName = (odooStore.XName ?? odooStore.DisplayName)?.Trim() ?? string.Empty;
                var url = NormaliseUrl(odooStore.XStudioStoreUrl);
                var host = ParseHost(url);

                // No mapping yet (first run): reconcile to an existing nop store by Host, then Name.
                if (nopStore == null)
                {
                    var candidate = !string.IsNullOrEmpty(host)
                        ? nopStores.FirstOrDefault(s => HostMatches(s, host))
                        : null;

                    if (candidate == null && !string.IsNullOrEmpty(storeName))
                        candidate = nopStores.FirstOrDefault(s =>
                            string.Equals(s.Name?.Trim(), storeName, StringComparison.OrdinalIgnoreCase));

                    // One Odoo store per nop store (unique index on NopStoreId): never adopt a store another
                    // Odoo store already owns — that is a duplicate in Odoo for a person to resolve.
                    if (candidate != null
                        && odooStoreIdByNopStoreId.TryGetValue(candidate.Id, out var ownerOdooStoreId)
                        && ownerOdooStoreId != odooStore.Id)
                    {                        await _odooLogService.WarningAsync(
                            $"Odoo store '{storeName}' (ID {odooStore.Id}) matches nopCommerce store '{candidate.Name}' (ID {candidate.Id}), which is already mapped to Odoo store {ownerOdooStoreId}. Resolve the duplicate in Odoo. Skipped.",
                            logEventId, entityType: "Store", odooEntityId: odooStore.Id, nopEntityId: candidate.Id);
                        continue;
                    }

                    nopStore = candidate;
                }

                var isNewStore = nopStore == null;
                if (isNewStore)
                {
                    // nop falls back to the first store (by DisplayOrder, then Id) for any unmatched
                    // host, so a new store must never sort ahead of the existing ones.
                    var minDisplayOrder = nopStores.Count > 0 ? nopStores.Min(s => s.DisplayOrder) : 0;

                    nopStore = new Store
                    {
                        Name = storeName,
                        Url = url,
                        Hosts = host,
                        SslEnabled = url.StartsWith("https://", StringComparison.OrdinalIgnoreCase),
                        DisplayOrder = Math.Max(odooStore.XStudioSequence, minDisplayOrder),
                        DefaultLanguageId = 0
                    };
                    await _storeService.InsertStoreAsync(nopStore);
                    nopStores.Add(nopStore);

                    await _odooLogService.InformationAsync(
                        $"Created nopCommerce store '{nopStore.Name}' (ID {nopStore.Id}) from Odoo store {odooStore.Id}." +
                        (string.IsNullOrEmpty(url) ? " (no URL in Odoo — Url/Host left empty; set before going live)" : string.Empty),
                        logEventId, entityType: "Store", odooEntityId: odooStore.Id, nopEntityId: nopStore.Id);
                }
                else
                {
                    nopStore.Name = storeName;

                    // Never blank a live store's Url/Hosts from an empty Odoo value, and add the Odoo
                    // host to the existing list rather than replacing it (stores often carry extra
                    // hosts). DisplayOrder and SslEnabled stay admin-owned after creation.
                    if (!string.IsNullOrEmpty(url))
                    {
                        nopStore.Url = url;
                        if (!HostMatches(nopStore, host))
                            nopStore.Hosts = string.IsNullOrWhiteSpace(nopStore.Hosts)
                                ? host
                                : $"{nopStore.Hosts.Trim().TrimEnd(',')},{host}";
                    }

                    await _storeService.UpdateStoreAsync(nopStore);

                    await _odooLogService.InformationAsync(
                        $"Updated nopCommerce store '{nopStore.Name}' (ID {nopStore.Id}) from Odoo store {odooStore.Id}" +
                        (mapping == null ? " (matched existing store on first run)" : string.Empty) +
                        (string.IsNullOrEmpty(url) ? "; no URL in Odoo — existing Url/Hosts kept." : "."),
                        logEventId, entityType: "Store", odooEntityId: odooStore.Id, nopEntityId: nopStore.Id);
                }

                await _storeMapService.UpsertAsync(odooStore.Id, nopStore.Id, odooStore.XStudioSyncRevision, odooStore.WriteDate);
                odooStoreIdByNopStoreId[nopStore.Id] = odooStore.Id;

                outcome = isNewStore ? SyncOutcome.Created : SyncOutcome.Updated;
            }
            catch (Exception ex)
            {
                await _odooLogService.ErrorAsync(
                    $"Error syncing Odoo store {odooStore.Id} ('{odooStore.XName}'): {ex.Message}",
                    logEventId, fullMessage: ex.ToString(), entityType: "Store", odooEntityId: odooStore.Id);
            }
            finally
            {
                tally.Add(outcome);
            }
        }

        var result = new StoreSyncResult(tally.Created, tally.Updated, tally.Skipped);
        await _odooLogService.InformationAsync(
            $"Store sync complete: {result.Created} created, {result.Updated} updated, {result.Skipped} skipped, {result.Processed} processed.",
            logEventId, entityType: "Store");

        return result;
    }

    #endregion
}
