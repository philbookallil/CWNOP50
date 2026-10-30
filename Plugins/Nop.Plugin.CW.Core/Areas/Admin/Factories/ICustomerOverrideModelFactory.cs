using Nop.Core.Domain.Customers;
using Nop.Plugin.CW.Core.Areas.Admin.Models.Customers;
using Nop.Web.Areas.Admin.Factories;

namespace Nop.Plugin.CW.Core.Areas.Admin.Factories;

public interface ICustomerOverrideModelFactory : ICustomerModelFactory
{
    /// <summary>
    /// Prepare customer search model
    /// </summary>
    /// <param name="searchModel">Customer search model</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the customer search model
    /// </returns>
    Task<ExtendedCustomerSearchModel> PrepareCustomerSearchModelAsync(ExtendedCustomerSearchModel searchModel);

    /// <summary>
    /// Prepare paged customer list model
    /// </summary>
    /// <param name="searchModel">Customer search model</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the customer list model
    /// </returns>
    Task<ExtendedCustomerListModel> PrepareCustomerListModelAsync(ExtendedCustomerSearchModel searchModel);

    /// <summary>
    /// Prepare customer model
    /// </summary>
    /// <param name="model">Customer model</param>
    /// <param name="customer">Customer</param>
    /// <param name="excludeProperties">Whether to exclude populating of some properties of model</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the customer model
    /// </returns>
    Task<ExtendedCustomerModel> PrepareExtendedCustomerModelAsync(ExtendedCustomerModel model, Customer customer, bool excludeProperties = false);
}
