namespace Nop.Plugin.CW.Core.Filters;

/// <summary>
/// Represents where <see cref="CheckCWFeatureAttribute"/> resolves the store identifier from
/// </summary>
public enum CWFeatureStoreSource
{
    /// <summary>
    /// No source chosen. Never valid: the filter blocks and logs an error. Exists so that omitting
    /// StoreSource is a visible mistake rather than a silent fallback to the current store, which in
    /// the admin area is the default store and therefore almost always the wrong one.
    /// </summary>
    NotSet = 0,

    /// <summary>
    /// The current store, from IStoreContext. Correct for all public store actions.
    /// In the admin area this resolves to the default store, so it is almost always wrong there.
    /// </summary>
    CurrentStore = 1,

    /// <summary>
    /// An action parameter, bound model property, route value or query-string value
    /// named by <see cref="CheckCWFeatureAttribute.ParameterName"/> (default "storeId")
    /// </summary>
    Parameter = 2,

    /// <summary>
    /// The store of the order whose identifier is named by
    /// <see cref="CheckCWFeatureAttribute.ParameterName"/> (default "orderId")
    /// </summary>
    Order = 3,

    /// <summary>
    /// Enabled for at least one store. For global admin pages with no store scope,
    /// such as the credit notes report or global fee settings.
    /// </summary>
    AnyStore = 4,

    /// <summary>
    /// The store of the branch whose identifier is named by ParameterName (default "branchId")
    /// </summary>
    Branch = 5,

    /// <summary>
    /// The store of the packName list whose identifier is named by ParameterName (default "listId")
    /// </summary>
    PackNameList = 6,

    /// <summary>
    /// The store of the packName whose identifier is named by ParameterName (default "packNameId"),
    /// resolved through its packName list
    /// </summary>
    PackName = 7,

    /// <summary>
    /// The store of the department whose identifier is named by ParameterName (default "departmentId")
    /// </summary>
    Department = 8,

    /// <summary>
    /// The store of the subsidy whose identifier is named by ParameterName (default "subsidyId")
    /// </summary>
    Subsidy = 9,

    /// <summary>
    /// The store of the product group whose identifier is named by ParameterName (default "productGroupId")
    /// </summary>
    ProductGroup = 10,

    /// <summary>
    /// The store of the subsidy-packName mapping whose identifier is named by ParameterName
    /// (default "subsidyPackNameId"), resolved through its subsidy
    /// </summary>
    SubsidyPackName = 11
}