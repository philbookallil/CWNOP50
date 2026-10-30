using Nop.Core.Domain.Customers;
using Nop.Plugin.CW.Core.Domain.Branches;
using Nop.Plugin.CW.Core.Domain.PackNames;

namespace Nop.Plugin.CW.Core.Services.PackNames
{
    public interface IPackNameContext
    {
        /// <summary>
        /// Gets the current packaging name
        /// </summary>
        /// <returns>A task that represents the asynchronous operation</returns>
        Task<PackName> GetCurrentPackNameAsync(Customer customer);

        /// <summary>
        /// Gets the current packaging name branch
        /// </summary>
        /// <returns>A task that represents the asynchronous operation</returns>
        Task<Branch> GetCurrentPackNameBranchAsync(Customer customer);

        /// <summary>
        /// Gets the selected packNameList identifier. If negative, it is a department
        /// </summary>
        /// <returns>A task that represents the asynchronous operation</returns>
        Task<int> GetSelectedPackNameListIdAsync(Customer customer);

        void ClearPackNameContext();
    }
}
