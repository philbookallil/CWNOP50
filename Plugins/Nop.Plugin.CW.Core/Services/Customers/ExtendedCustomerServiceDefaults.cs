using Nop.Core.Caching;

namespace Nop.Plugin.CW.Core.Services.Customers;

public static partial class ExtendedCustomerServiceDefaults
{
    #region Caching defaults

    /// <summary>
    /// Gets a key for caching
    /// </summary>
    public static CacheKey DepartmentCustomerRoleIdsCacheKey => new("CW.department.all.customerroles.ids.");

    #endregion
}
