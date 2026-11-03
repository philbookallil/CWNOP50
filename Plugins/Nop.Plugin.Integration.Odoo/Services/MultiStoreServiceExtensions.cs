using Nop.Plugin.CW.Core.Domain.MultiStores;
using Nop.Plugin.CW.Core.Services.MultiStores;

namespace Nop.Plugin.Integration.Odoo.Services;

internal static class MultiStoreServiceExtensions
{
    /// <summary>
    /// Finds the single non-deleted department in a store whose name matches exactly
    /// (case-insensitive), or null. This is the (StoreId, Name) natural key used to match Odoo
    /// departments to CW.Core departments.
    /// </summary>
    public static async Task<Department> GetDepartmentByStoreAndNameAsync(
        this IMultiStoreService multiStoreService, int storeId, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        var trimmed = name.Trim();
        var departments = await multiStoreService.GetDepartmentsByStoreIdAsync(storeId, searchName: trimmed);
        return departments.FirstOrDefault(d =>
            !d.Deleted && string.Equals(d.Name?.Trim(), trimmed, StringComparison.OrdinalIgnoreCase));
    }
}
