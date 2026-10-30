using Microsoft.AspNetCore.Http;
using Nop.Core;
using Nop.Core.Caching;
using Nop.Core.Domain.Customers;
using Nop.Data;
using Nop.Plugin.CW.Core.Domain.Branches;
using Nop.Plugin.CW.Core.Domain.PackNames;
using Nop.Plugin.CW.Core.Domain.Settings;
using Nop.Plugin.CW.Core.Services.MultiStores;
using Nop.Plugin.CW.Core.Services.Settings;
using Nop.Services.Common;
using Nop.Services.Helpers;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.ScheduleTasks;

namespace Nop.Plugin.CW.Core.Services.PackNames
{
    public class PackNameContext : IPackNameContext
    {
        #region Fields

        protected readonly IHttpContextAccessor _httpContextAccessor;
        protected readonly IUserAgentHelper _userAgentHelper;
        protected readonly IGenericAttributeService _genericAttributeService;
        protected readonly IRepository<PackName> _packNameRepository;
        protected readonly IRepository<PackNameList> _packNameListRepository;
        protected readonly IRepository<Branch> _branchRepository;
        protected readonly IStoreContext _storeContext;
        protected readonly IStaticCacheManager _staticCacheManager;
        protected readonly ICustomerActivityService _customerActivityService;
        protected readonly ILocalizationService _localizationService;
        protected readonly ICWFeatureService _cwFeatureService;
        //Lazy: MultiStoreService -> IPackNameService -> IPackNameContext would be a constructor cycle.
        protected readonly Lazy<IMultiStoreService> _multiStoreService;

        protected Dictionary<int, PackName> _cachedPackName = new();
        protected Dictionary<int, int> _cachedPackNameList = new();
        protected Dictionary<int, Branch> _cachedPackNameBranch = new();

        #endregion

        #region Ctor

        public PackNameContext(IHttpContextAccessor httpContextAccessor,
            IUserAgentHelper userAgentHelper,
            IGenericAttributeService genericAttributeService,
            IRepository<PackName> packNameRepository,
            IRepository<PackNameList> packNameListRepository,
            IRepository<Branch> branchRepository,
            IStoreContext storeContext,
            IStaticCacheManager staticCacheManager,
            ICustomerActivityService customerActivityService,
            ILocalizationService localizationService,
            ICWFeatureService cwFeatureService,
            Lazy<IMultiStoreService> multiStoreService)
        {
            _httpContextAccessor = httpContextAccessor;
            _userAgentHelper = userAgentHelper;
            _genericAttributeService = genericAttributeService;
            _packNameRepository = packNameRepository;
            _packNameListRepository = packNameListRepository;
            _branchRepository = branchRepository;
            _storeContext = storeContext;
            _staticCacheManager = staticCacheManager;
            _customerActivityService = customerActivityService;
            _localizationService = localizationService;
            _cwFeatureService = cwFeatureService;
            _multiStoreService = multiStoreService;
        }

        #endregion

        #region Utilities

        /// <summary>
        /// Whether this customer may still hold the given packname as their ordering context.
        /// </summary>
        /// <remarks>
        /// One definition of ownership: the same <see cref="IMultiStoreService.IsPackNameOwnedByManagerId"/> every
        /// guard uses, resolved through a <c>Lazy</c> because <c>MultiStoreService</c> → <c>IPackNameService</c> →
        /// <c>IPackNameContext</c> is a constructor cycle (the pattern <c>ProductOverrideService</c> already uses for
        /// this context). Re-implementing the rule here would be a second definition of it, which is the failure this
        /// phase has been unpicking all week.
        /// </remarks>
        private async Task<bool> IsSelectableByCustomerAsync(Customer customer, PackName packName)
        {
            var ownPackName = await GetPackNameByCustomerIdAsync(customer.Id);

            //No packname of their own: nothing is selectable.
            if (ownPackName == null || ownPackName.Deleted)
                return false;

            //Their OWN packname is always theirs, manager or not.
            if (ownPackName.Id == packName.Id)
                return true;

            //Anyone else's is selectable only by a manager who still owns it.
            return ownPackName.Manager
                && await _multiStoreService.Value.IsPackNameOwnedByManagerId(ownPackName.Id, packName.Id);
        }

        /// <summary>
        /// Sets the current packaging name
        /// </summary>
        /// <param name="packName">Current packaging name</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        private async Task SetCurrentPackNameAsync(Customer customer, PackName packName = null)
        {
            //PackNames disabled for this store: resolve to nothing so the whole store falls back to
            //standard nopCommerce behaviour. Every consumer treats a null packname as "not a
            //packname customer", so this one guard covers the selector, the info banner, cart
            //splitting, order totals and the checkout redirect.
            if (customer != null && !await _cwFeatureService.IsEnabledAsync(CWFeature.PackName))
            {
                _cachedPackName[customer.Id] = new PackName();
                _cachedPackNameList[customer.Id] = 0;
                _cachedPackNameBranch[customer.Id] = null;

                return;
            }

            if (packName == null)
            {
                //check whether request is made by a background (schedule) task
                if (_httpContextAccessor.HttpContext?.Request
                    ?.Path.Equals(new PathString($"/{NopTaskDefaults.ScheduleTaskPath}"), StringComparison.InvariantCultureIgnoreCase)
                    ?? true)
                {
                    //in this case return built-in packname record for background task
                    packName = new PackName();
                }

                if (packName == null)
                {
                    //check whether request is made by a search engine, in this case return built-in packname record for search engines
                    if (_userAgentHelper.IsSearchEngine())
                        packName = new PackName();
                }

                if (packName == null)
                {
                    //get selected packaging name
                    var selectedPackNameId = await _genericAttributeService.GetAttributeAsync<int?>(customer, PackNameDefaults.SelectedPackNameIdAttribute);

                    if (selectedPackNameId == null)
                    {
                        //we need to validate if the current customer have a packname assigned to it, if so we need to assign it as selected packname
                        var customerPackName = await GetPackNameByCustomerIdAsync(customer.Id);

                        if (customerPackName != null)
                        {
                            var branch = await GetBranchByPackNameIdAsync(customerPackName.Id);

                            //The branch should never be null, but we do the validation anyway
                            if (branch != null)
                            {
                                //activity log
                                await _customerActivityService.InsertActivityAsync(customer, "PackNameSelection.Started",
                                    string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.ActivityLog.PackNameSelection.Started.Customer"), customer.Email, customer.Id));
                                await _customerActivityService.InsertActivityAsync(customer, "PackNameSelection.Started",
                                    string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.ActivityLog.PackNameSelection.Started.Branch"), branch.BranchCode, customerPackName.Id), customer);

                                await _genericAttributeService.SaveAttributeAsync<int?>(customer, PackNameDefaults.SelectedPackNameIdAttribute, customerPackName.Id);
                                await _genericAttributeService.SaveAttributeAsync<int?>(customer, PackNameDefaults.SelectedPackNameListIdAttribute, customerPackName.PackNameListId);

                                selectedPackNameId = customerPackName.Id;
                            }
                        }
                    }

                    if (selectedPackNameId.HasValue && selectedPackNameId.Value > 0)
                    {
                        var selectedPackName = await GetPackNameByIdAsync(selectedPackNameId.Value);
                        var selectedPackNameList = selectedPackName == null
                            ? null
                            : await GetPackNameListByIdAsync(selectedPackName.PackNameListId);

                        var currentStoreId = (await _storeContext.GetCurrentStoreAsync()).Id;

                        //REVALIDATE, do not merely restore. This used to check the STORE and nothing else, so a
                        //packname the customer may no longer use stayed the ordering context for good: revoke a
                        //manager's department or list ownership, or delete the packname, and the catalogue, the
                        //subsidy bar, add-to-cart and the checkout all kept working against it.
                        //The guards refuse an action ON it, because they re-check the subject - but the context is
                        //the authorisation input for everything that has no guard of its own, so it has to answer
                        //the same ownership question the guards ask.
                        //Active is asked of someone else's packname only: a deactivated employee is no longer
                        //someone a manager orders for, while a customer's own packname stays their context.
                        if (selectedPackName != null
                            && !selectedPackName.Deleted
                            && (selectedPackName.Active || selectedPackName.CustomerId == customer.Id)
                            && selectedPackNameList?.StoreId == currentStoreId
                            && await IsSelectableByCustomerAsync(customer, selectedPackName))
                        {
                            //set packname
                            packName = selectedPackName;

                            //a packname moved to another list since it was selected: the remembered list follows it, so the
                            //selector opens the list the packname is in now. A department entry (a negative id) is not a list
                            //the packname moves out of, so it stays
                            var selectedListId = await _genericAttributeService.GetAttributeAsync<int?>(customer, PackNameDefaults.SelectedPackNameListIdAttribute);

                            if (selectedListId > 0 && selectedListId != selectedPackName.PackNameListId)
                                await _genericAttributeService.SaveAttributeAsync<int?>(customer, PackNameDefaults.SelectedPackNameListIdAttribute, selectedPackName.PackNameListId);
                        }
                        else
                        {
                            //Clear the stale selection so the state self-heals, and fall back to the customer's
                            //own packname - the same thing they would get on a first visit.
                            await _genericAttributeService.SaveAttributeAsync<int?>(customer, PackNameDefaults.SelectedPackNameIdAttribute, null);
                            await _genericAttributeService.SaveAttributeAsync<int?>(customer, PackNameDefaults.SelectedPackNameListIdAttribute, null);

                            var ownPackName = await GetPackNameByCustomerIdAsync(customer.Id);
                            var ownPackNameList = ownPackName == null
                                ? null
                                : await GetPackNameListByIdAsync(ownPackName.PackNameListId);

                            if (ownPackName != null && !ownPackName.Deleted && ownPackNameList?.StoreId == currentStoreId)
                            {
                                packName = ownPackName;

                                await _genericAttributeService.SaveAttributeAsync<int?>(customer, PackNameDefaults.SelectedPackNameIdAttribute, ownPackName.Id);
                                await _genericAttributeService.SaveAttributeAsync<int?>(customer, PackNameDefaults.SelectedPackNameListIdAttribute, ownPackName.PackNameListId);
                            }
                        }
                    }
                }
            }

            if (packName == null)
            {
                //No packname is an answer too: kept for the rest of the request, so a customer without one does not
                //run the selection again on every role or cart lookup
                _cachedPackName[customer.Id] = new PackName();
                _cachedPackNameBranch[customer.Id] = null;
                _cachedPackNameList[customer.Id] = 0;
            }
            else
            {
                //cache the found packaging name
                _cachedPackName[customer.Id] = packName;
                _cachedPackNameBranch[customer.Id] = await GetBranchByPackNameIdAsync(packName.Id);

                //get selected packaging name list identifier
                var selectedPackNameListId = await _genericAttributeService.GetAttributeAsync<int?>(customer, PackNameDefaults.SelectedPackNameListIdAttribute);
                _cachedPackNameList[customer.Id] = selectedPackNameListId ?? packName.PackNameListId;
            }
        }

        #endregion

        #region Duplicated from PackName Service

        /// <summary>
        /// Gets a packaging name
        /// </summary>
        /// <param name="packNameId">PackName identifier</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        private async Task<PackName> GetPackNameByIdAsync(int packNameId)
        {
            if (packNameId == 0)
                return null;

            return await _packNameRepository.GetByIdAsync(packNameId, cache => default);
        }

        /// <summary>
        /// Gets a packName by customer indentifier
        /// </summary>
        /// <param name="customerId">Customer indentifier</param>
        /// <returns>PackName</returns>
        private async Task<PackName> GetPackNameByCustomerIdAsync(int customerId)
        {
            var key = _staticCacheManager.PrepareKeyForDefaultCache(PackNameServicesDefaults.PackNameByCustomerCacheKey, customerId);

            var query = from pn in _packNameRepository.Table
                        where pn.CustomerId == customerId && !pn.Deleted
                        select pn;

            return await _staticCacheManager.GetAsync(key, () => query.FirstOrDefaultAsync());
        }

        /// <summary>
        /// Gets a packaging name
        /// </summary>
        /// <param name="packNameListId">PackNameList identifier</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        private async Task<PackNameList> GetPackNameListByIdAsync(int packNameListId)
        {
            if (packNameListId == 0)
                return null;

            return await _packNameListRepository.GetByIdAsync(packNameListId, cache => default);
        }

        /// <summary>
        /// Gets a branch by packaging name indentifier
        /// </summary>
        /// <param name="packNameId">Packaging name indentifier</param>
        /// <returns>Branch</returns>
        public async Task<Branch> GetBranchByPackNameIdAsync(int packNameId)
        {
            var query = from pn in _packNameRepository.Table
                        join pnl in _packNameListRepository.Table on pn.PackNameListId equals pnl.Id
                        join b in _branchRepository.Table on pnl.BranchId equals b.Id
                        where pn.Id == packNameId && !b.Deleted
                        select b;

            return await query.FirstOrDefaultAsync();
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the current packaging name
        /// </summary>
        /// <returns>A task that represents the asynchronous operation</returns>
        public virtual async Task<PackName> GetCurrentPackNameAsync(Customer customer)
        {
            if (customer == null)
                return null;

            //whether there is a cached value; a record with no id stands for "no packname"
            if (_cachedPackName.TryGetValue(customer.Id, out var packName))
                return packName.Id > 0 ? packName : null;

            await SetCurrentPackNameAsync(customer);

            if (_cachedPackName.TryGetValue(customer.Id, out var cachedPackName) && cachedPackName.Id > 0)
                return cachedPackName;

            return null;
        }

        /// <summary>
        /// Gets the current packaging name branch
        /// </summary>
        /// <returns>A task that represents the asynchronous operation</returns>
        public virtual async Task<Branch> GetCurrentPackNameBranchAsync(Customer customer)
        {
            //whether there is a cached value
            if (_cachedPackNameBranch.TryGetValue(customer.Id, out var branch))
                return branch;

            await SetCurrentPackNameAsync(customer);

            if (_cachedPackNameBranch.TryGetValue(customer.Id, out var cachedBranch))
                return cachedBranch;

            return null;
        }

        /// <summary>
        /// Gets the selected packNameList identifier. If negative, it is a department
        /// </summary>
        /// <returns>A task that represents the asynchronous operation</returns>
        public virtual async Task<int> GetSelectedPackNameListIdAsync(Customer customer)
        {
            //whether there is a cached value
            if (_cachedPackNameList.TryGetValue(customer.Id, out var packNameListId))
                return packNameListId;

            await SetCurrentPackNameAsync(customer);

            if (_cachedPackNameList.TryGetValue(customer.Id, out var cachedPackNameListId))
                return cachedPackNameListId;

            return 0;
        }

        public virtual void ClearPackNameContext()
        {
            _cachedPackName.Clear();
            _cachedPackNameBranch.Clear();
            _cachedPackNameList.Clear();
        }

        #endregion
    }
}
