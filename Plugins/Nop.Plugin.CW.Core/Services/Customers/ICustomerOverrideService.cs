using Nop.Core;
using Nop.Core.Domain.Customers;
using Nop.Plugin.CW.Core.Domain.Customers;
using Nop.Plugin.CW.Core.Domain.MultiStores;
using Nop.Plugin.CW.Core.Domain.Orders.Views;
using Nop.Plugin.CW.Core.Domain.PackNames;
using Nop.Services.Customers;

namespace Nop.Plugin.CW.Core.Services.Customers;

public interface ICustomerOverrideService : ICustomerService
{
    #region Customers 

    /// <summary>
    /// Gets all customers
    /// </summary>
    /// <param name="createdFromUtc">Created date from (UTC); null to load all records</param>
    /// <param name="createdToUtc">Created date to (UTC); null to load all records</param>
    /// <param name="lastActivityFromUtc">Last activity date from (UTC); null to load all records</param>
    /// <param name="lastActivityToUtc">Last activity date to (UTC); null to load all records</param>
    /// <param name="affiliateId">Affiliate identifier</param>
    /// <param name="vendorId">Vendor identifier</param>
    /// <param name="customerRoleIds">A list of customer role identifiers to filter by (at least one match); pass null or empty list in order to load all customers; </param>
    /// <param name="email">Email; null to load all customers</param>
    /// <param name="username">Username; null to load all customers</param>
    /// <param name="firstName">First name; null to load all customers</param>
    /// <param name="lastName">Last name; null to load all customers</param>
    /// <param name="dayOfBirth">Day of birth; 0 to load all customers</param>
    /// <param name="monthOfBirth">Month of birth; 0 to load all customers</param>
    /// <param name="company">Company; null to load all customers</param>
    /// <param name="phone">Phone; null to load all customers</param>
    /// <param name="zipPostalCode">Phone; null to load all customers</param>
    /// <param name="ipAddress">IP address; null to load all customers</param>
    /// <param name="isActive">Customer is active; null to load all customers</param>
    /// <param name="storeId">Store identifier</param>
    /// <param name="pageIndex">Page index</param>
    /// <param name="pageSize">Page size</param>
    /// <param name="getOnlyTotalCount">A value in indicating whether you want to load only total number of records. Set to "true" if you don't want to load data from database</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the customers
    /// </returns>
    Task<IPagedList<(Customer Customer, CustomerBranch CustomerBranch, string BranchName)>> GetAllCustomersDataAsync(DateTime? createdFromUtc = null, DateTime? createdToUtc = null,
        DateTime? lastActivityFromUtc = null, DateTime? lastActivityToUtc = null,
        int affiliateId = 0, int vendorId = 0, int[] customerRoleIds = null,
        string email = null, string username = null, string firstName = null, string lastName = null,
        int dayOfBirth = 0, int monthOfBirth = 0,
        string company = null, string phone = null, string zipPostalCode = null, string ipAddress = null,
        bool? isActive = null, int storeId = 0, int pageIndex = 0, int pageSize = int.MaxValue, bool getOnlyTotalCount = false);

    /// <summary>
    /// Returns the customer role name for a Department
    /// </summary>
    /// <param name="department">The department</param>
    /// <returns>The formatted customer role name for the department</returns>
    string DepartmentCustomerRoleName(Department department);

    /// <summary>
    /// Gets the role identifiers of the customer themselves, whatever packname they are ordering for
    /// </summary>
    /// <remarks>
    /// For what the customer pays and may use - tier prices, plugins limited to roles. GetCustomerRoleIdsAsync
    /// answers with the packname's roles in the packname context.
    /// </remarks>
    /// <param name="customer">Customer</param>
    /// <param name="showHidden">A value indicating whether to load hidden records</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the customer role identifiers
    /// </returns>
    Task<int[]> GetSignedInCustomerRoleIdsAsync(Customer customer, bool showHidden = false);

    /// <summary>
    /// Gets the packname whose catalogue the customer is seeing
    /// </summary>
    /// <remarks>
    /// The packname of the open PackNameCatalogScope when there is one - a cart line, an exchange replacement -
    /// else the packname the customer has selected in the public store.
    /// </remarks>
    /// <param name="customer">Customer</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the packname; null outside the packname context
    /// </returns>
    Task<PackName> GetCatalogPackNameAsync(Customer customer);

    /// <summary>
    /// Asynchronously retrieves a set of unique customer role IDs associated with departments.
    /// </summary>
    /// <remarks>The result is cached to improve performance on subsequent calls. Only departments that are
    /// not marked as deleted and have a customer role assigned are included.</remarks>
    /// <returns>A task that represents the asynchronous operation. The task result contains a set of unique customer role IDs
    /// for all departments. The set will be empty if no departments have associated customer roles.</returns>
    Task<HashSet<int>> GetDepartmentCustomerRoleIdsAsync();

    /// <summary>
    /// Creates or updates the customer role associated with the specified department, ensuring the department has a
    /// corresponding customer role with the correct name.
    /// </summary>
    /// <remarks>If the department does not have an associated customer role, a new customer role is created
    /// and linked to the department. If a customer role already exists but its name does not match the expected value,
    /// the role is updated. This method is typically used to maintain consistency between departments and their
    /// customer roles.</remarks>
    /// <param name="department">The department for which to create or update the associated customer role. Cannot be null. The department's
    /// CustomerRoleId property will be set or updated as needed.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task SaveDepartmentCustomerRole(Department department);

    /// <summary>
    /// Gets customer for shopping cart
    /// </summary>
    /// <param name="shoppingCart">Shopping cart</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the result
    /// </returns>
    Task<Customer> GetShoppingCartCustomerAsync(IList<PackNameShoppingCartItem> shoppingCart);

    #endregion

    #region Customer Branch

    /// <summary>
    /// Gets a customer
    /// </summary>
    /// <param name="customerId">Customer identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains a customer
    /// </returns>
    Task<CustomerBranch> GetCustomerBranchByIdAsync(int customerId);

    /// <summary>
    /// Insert a customer branch mapping
    /// </summary>
    /// <param name="customer">CustomerBranch</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    Task InsertCustomerBranchAsync(CustomerBranch customer);

    /// <summary>
    /// Updates the customer branch mapping
    /// </summary>
    /// <param name="customer">CustomerBranch</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    Task UpdateCustomerBranchAsync(CustomerBranch customer);

    #endregion
}
