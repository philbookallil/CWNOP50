using Nop.Plugin.CW.Core.Domain.PackNames;
using Nop.Plugin.CW.Core.Models.PackNames;
using Nop.Plugin.CW.Core.Services.PackNames.Import;

namespace Nop.Plugin.CW.Core.Factories;

public interface IPackNameModelFactory
{
    /// <summary>
    /// Prepares one page of the packnames the current manager may order for in an entry of the storefront selector
    /// </summary>
    /// <remarks>The roster follows the manager's ownership of pack name lists, department assignments and the store's
    /// settings. A page holds the first packnames by name, or the first that match the term, plus on the first page the
    /// selected packname and those with cart lines. Which pack names are editable and whether ordering is blocked is worked
    /// out for the page.</remarks>
    /// <param name="listId">The identifier of the pack name list to retrieve. If negative, represents a virtual list for a department, where
    /// the absolute value is the department ID.</param>
    /// <param name="term">What the manager typed in the selector; empty for the first page</param>
    /// <param name="selectedPackNameId">The packname in context, kept on the first page; 0 for none</param>
    /// <returns>The page; empty when the manager has no packnames to see in the entry</returns>
    Task<PackNameSelectorPageModel> PreparePackNamesModelForManagerAsync(int listId, string term, int selectedPackNameId);

    /// <summary>
    /// Prepare the packName register model
    /// </summary>
    /// <param name="model">PackName register model</param>
    /// <param name="manager">Manager</param>
    /// <param name="storeId">Store identifier</param>
    /// <param name="branchId">Branch identifier</param>
    /// <param name="overrideAddressAttributesXml">Overridden address attributes in XML format</param>
    /// <param name="overrideCustomCustomerAttributesXml">Overridden customer attributes in XML format</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the packName register model
    /// </returns>
    Task<PackNameRegisterModel> PreparePackNameRegisterModelAsync(PackNameRegisterModel model, PackName manager, int storeId, int branchId, string overrideAddressAttributesXml = "", string overrideCustomCustomerAttributesXml = "");

    /// <summary>
    /// Prepare the employee edit model
    /// </summary>
    /// <param name="model">Employee register model</param>
    /// <param name="packName">PackNamer</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the employee register model
    /// </returns>
    Task<PackNameRegisterModel> PreparePackNameEditModelAsync(PackNameRegisterModel model, PackName packName);

    /// <summary>
    /// Prepare the manager's Excel import page model from the layout of the manager's file
    /// </summary>
    /// <param name="layout">The import layout</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the import page model
    /// </returns>
    Task<PackNameImportModel> PreparePackNameImportModelAsync(PackNameImportLayout layout);
}