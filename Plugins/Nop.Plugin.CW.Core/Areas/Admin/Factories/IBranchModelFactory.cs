using Nop.Core.Domain.Stores;
using Nop.Plugin.CW.Core.Areas.Admin.Models.Branches;
using Nop.Plugin.CW.Core.Areas.Admin.Models.Common;
using Nop.Plugin.CW.Core.Domain.Branches;

namespace Nop.Plugin.CW.Core.Areas.Admin.Factories;

public interface IBranchModelFactory
{
    /// <summary>
    /// Prepare paged branch list model
    /// </summary>
    /// <param name="searchModel">Branch search model</param>
    /// <param name="store">Store</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the branch list model
    /// </returns>
    Task<BranchListModel> PrepareBranchListModelAsync(BranchSearchModel searchModel, Store store);

    /// <summary>
    /// Prepare branch model
    /// </summary>
    /// <param name="model">Branch model</param>
    /// <param name="branch">Branch</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the branch model
    /// </returns>
    Task<BranchModel> PrepareBranchModelAsync(BranchModel model, Branch branch);

    Task<BranchModel> PrepareBranchModelAsync(int storeId, int? branchId, int? parentBranchId);

    /// <summary>
    /// Prepare paged branch address list model
    /// </summary>
    /// <param name="searchModel">Branch address search model</param>
    /// <param name="branch">Branch</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the branch address list model
    /// </returns>
    Task<CWAddressListModel> PrepareBranchAddressListModelAsync(CWAddressSearchModel searchModel, Branch branch);

    /// <summary>
    /// Prepare the branch's server-rendered address collection (the branch page). The paged list
    /// method above still serves BranchController's grid.
    /// </summary>
    Task<BranchAddressesModel> PrepareBranchAddressesModelAsync(Branch branch);
}
