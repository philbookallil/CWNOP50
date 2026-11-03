using Nop.Data;
using Nop.Plugin.Integration.Odoo.Domain;

namespace Nop.Plugin.Integration.Odoo.Services;

public class OdooStoreMapService : IOdooStoreMapService
{
    private readonly IRepository<OdooStoreMapping> _repository;

    public OdooStoreMapService(IRepository<OdooStoreMapping> repository)
    {
        _repository = repository;
    }

    public async Task<OdooStoreMapping> GetByOdooStoreIdAsync(int odooStoreId)
    {
        return await _repository.Table.FirstOrDefaultAsync(m => m.OdooStoreId == odooStoreId);
    }

    public async Task<int?> GetNopStoreIdAsync(int odooStoreId)
    {
        var mapping = await GetByOdooStoreIdAsync(odooStoreId);
        return mapping?.NopStoreId;
    }

    public async Task<Dictionary<int, int>> GetNopStoreIdLookupAsync()
    {
        var mappings = await _repository.GetAllAsync(query => query);
        return mappings.ToDictionary(m => m.OdooStoreId, m => m.NopStoreId);
    }

    public async Task UpsertAsync(int odooStoreId, int nopStoreId, int revision, string writeDate)
    {
        var mapping = await GetByOdooStoreIdAsync(odooStoreId);
        if (mapping == null)
        {
            mapping = new OdooStoreMapping
            {
                OdooStoreId = odooStoreId,
                NopStoreId = nopStoreId,
                LastSyncedRevision = revision,
                LastSyncedWriteDate = writeDate,
                CreatedOnUtc = DateTime.UtcNow,
                UpdatedOnUtc = DateTime.UtcNow
            };
            await _repository.InsertAsync(mapping, false);
        }
        else
        {
            mapping.NopStoreId = nopStoreId;
            mapping.LastSyncedRevision = revision;
            mapping.LastSyncedWriteDate = writeDate;
            mapping.UpdatedOnUtc = DateTime.UtcNow;
            await _repository.UpdateAsync(mapping, false);
        }
    }
}
