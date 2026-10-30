using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using Nop.Core;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Common;
using Nop.Core.Domain.Customers;
using Nop.Plugin.CW.Core.Domain.Branches;
using Nop.Plugin.CW.Core.Domain.Customers;
using Nop.Plugin.CW.Core.Domain.PackNames;
using Nop.Plugin.CW.Core.Domain.Settings;
using Nop.Plugin.CW.Core.Domain.Subsidies;
using Nop.Plugin.CW.Core.Extensions;
using Nop.Plugin.CW.Core.Factories;
using Nop.Plugin.CW.Core.Filters;
using Nop.Plugin.CW.Core.Helpers;
using Nop.Plugin.CW.Core.Models.PackNames;
using Nop.Plugin.CW.Core.Services.Branches;
using Nop.Plugin.CW.Core.Services.Customers;
using Nop.Plugin.CW.Core.Services.MultiStores;
using Nop.Plugin.CW.Core.Services.PackNames;
using Nop.Plugin.CW.Core.Services.PackNames.Import;
using Nop.Plugin.CW.Core.Services.Settings;
using Nop.Plugin.CW.Core.Services.Subsidies;
using Nop.Services.Attributes;
using Nop.Services.Common;
using Nop.Services.Configuration;
using Nop.Services.Customers;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Web.Controllers;
using Nop.Web.Framework.Mvc.Filters;
using System.Transactions;

namespace Nop.Plugin.CW.Core.Controllers;

[CheckCWFeature(CWFeature.PackName, StoreSource = CWFeatureStoreSource.CurrentStore)]
[AutoValidateAntiforgeryToken]
public class PackNameController : BasePublicController
{
    #region Fields

    protected readonly IPackNameModelFactory _packNameModelFactory;
    protected readonly IWorkContext _workContext;
    protected readonly IPackNameService _packNameService;
    protected readonly ISettingService _settingService;
    protected readonly IBranchService _branchService;
    protected readonly ILocalizationService _localizationService;
    protected readonly ICustomerOverrideService _customerService;
    protected readonly IMultiStoreService _multiStoreService;
    protected readonly CustomerSettings _customerSettings;
    protected readonly IAttributeService<CustomerAttribute, CustomerAttributeValue> _customerAttributeService;
    protected readonly IAttributeParser<CustomerAttribute, CustomerAttributeValue> _customerAttributeParser;
    protected readonly IAttributeParser<AddressAttribute, AddressAttributeValue> _addressAttributeParser;
    protected readonly IPackNameContext _packNameContext;
    protected readonly ICustomerActivityService _customerActivityService;
    protected readonly ISubsidyService _subsidyService;
    protected readonly ICWFeatureService _cwFeatureService;
    protected readonly IPackNameLandingService _packNameLandingService;
    protected readonly IPackNameRegistrationService _packNameRegistrationService;
    protected readonly IPackNameImportService _packNameImportService;

    private static readonly char[] _separator = [','];

    #endregion

    #region Ctor

    public PackNameController(IPackNameModelFactory packNameModelFactory,
        IWorkContext workContext,
        IPackNameService packNameService,
        ISettingService settingService,
        IBranchService branchService,
        ILocalizationService localizationService,
        ICustomerOverrideService customerService,
        IMultiStoreService multiStoreService,
        CustomerSettings customerSettings,
        IAttributeService<CustomerAttribute, CustomerAttributeValue> customerAttributeService,
        IAttributeParser<CustomerAttribute, CustomerAttributeValue> customerAttributeParser,
        IAttributeParser<AddressAttribute, AddressAttributeValue> addressAttributeParser,
        IPackNameContext packNameContext,
        ICustomerActivityService customerActivityService,
        ISubsidyService subsidyService,
        ICWFeatureService cwFeatureService,
        IPackNameLandingService packNameLandingService,
        IPackNameRegistrationService packNameRegistrationService,
        IPackNameImportService packNameImportService)
    {
        _packNameModelFactory = packNameModelFactory;
        _workContext = workContext;
        _packNameService = packNameService;
        _settingService = settingService;
        _branchService = branchService;
        _localizationService = localizationService;
        _customerService = customerService;
        _multiStoreService = multiStoreService;
        _customerSettings = customerSettings;
        _customerAttributeService = customerAttributeService;
        _customerAttributeParser = customerAttributeParser;
        _addressAttributeParser = addressAttributeParser;
        _packNameContext = packNameContext;
        _customerActivityService = customerActivityService;
        _subsidyService = subsidyService;
        _cwFeatureService = cwFeatureService;
        _packNameLandingService = packNameLandingService;
        _packNameRegistrationService = packNameRegistrationService;
        _packNameImportService = packNameImportService;
    }

    #endregion

    #region Utilities

    protected virtual async Task UpdatePackName(PackName packName, PackNameList packNameList, PackNameRegisterModel model)
    {
        packName.PackNameListId = packNameList.Id;
        packName.Name = model.Name;
        packName.PrintName = model.PrintName;
        packName.Group = model.Group;
        packName.Gender = model.Gender;
        //the colour is written into the selector's HTML: anything but a hex colour keeps the one the packname has
        packName.Colour = PackNameColourHelper.Normalize(model.Colour) ?? packName.Colour;
        packName.Comments = model.Comments;
        packName.EmployeeNumber = model.EmployeeNumber;
        packName.POEntry = model.POEntry;
        packName.DepartmentId = model.DepartmentId == 0 ? null : model.DepartmentId;
        packName.CostCenterId = model.CostCenterId == 0 ? null : model.CostCenterId;

        await _packNameService.UpdatePackNameAsync(packName);
    }

    protected virtual async Task<string> ParseCustomCustomerAttributesAsync(IFormCollection form)
    {
        ArgumentNullException.ThrowIfNull(form);

        var attributesXml = string.Empty;
        var customerAttributes = await _customerAttributeService.GetAllAttributesAsync();
        foreach (var attribute in customerAttributes)
        {
            var controlId = $"{NopCustomerServicesDefaults.CustomerAttributePrefix}{attribute.Id}";
            StringValues ctrlAttributes;

            switch (attribute.AttributeControlType)
            {
                case AttributeControlType.DropdownList:
                case AttributeControlType.RadioList:
                    ctrlAttributes = form[controlId];
                    if (!StringValues.IsNullOrEmpty(ctrlAttributes))
                    {
                        var selectedAttributeId = int.Parse(ctrlAttributes);
                        if (selectedAttributeId > 0)
                            attributesXml = _customerAttributeParser.AddAttribute(attributesXml,
                                attribute, selectedAttributeId.ToString());
                    }

                    break;
                case AttributeControlType.Checkboxes:
                    var cblAttributes = form[controlId];
                    if (!StringValues.IsNullOrEmpty(cblAttributes))
                    {
                        foreach (var item in cblAttributes.ToString()
                                     .Split(_separator, StringSplitOptions.RemoveEmptyEntries))
                        {
                            var selectedAttributeId = int.Parse(item);
                            if (selectedAttributeId > 0)
                                attributesXml = _customerAttributeParser.AddAttribute(attributesXml,
                                    attribute, selectedAttributeId.ToString());
                        }
                    }

                    break;
                case AttributeControlType.ReadonlyCheckboxes:
                    //load read-only (already server-side selected) values
                    var attributeValues = await _customerAttributeService.GetAttributeValuesAsync(attribute.Id);
                    foreach (var selectedAttributeId in attributeValues
                                 .Where(v => v.IsPreSelected)
                                 .Select(v => v.Id)
                                 .ToList())
                    {
                        attributesXml = _customerAttributeParser.AddAttribute(attributesXml,
                            attribute, selectedAttributeId.ToString());
                    }

                    break;
                case AttributeControlType.TextBox:
                case AttributeControlType.MultilineTextbox:
                    ctrlAttributes = form[controlId];
                    if (!StringValues.IsNullOrEmpty(ctrlAttributes))
                    {
                        var enteredText = ctrlAttributes.ToString().Trim();
                        attributesXml = _customerAttributeParser.AddAttribute(attributesXml,
                            attribute, enteredText);
                    }

                    break;
                case AttributeControlType.Datepicker:
                case AttributeControlType.ColorSquares:
                case AttributeControlType.ImageSquares:
                case AttributeControlType.FileUpload:
                //not supported customer attributes
                default:
                    break;
            }
        }

        return attributesXml;
    }

    /// <summary>
    /// The current customer's own packname when they are a manager, otherwise null.
    /// </summary>
    /// <remarks>
    /// Deliberately does NOT check <c>AllowManagersToAddPackNames</c>. The two GET actions have to
    /// resolve the manager BEFORE they know which store's settings apply — a packname carries no
    /// StoreId, so the store comes from its list, and for "add" with no list posted the list comes
    /// from the manager. Folding the setting in here would force that ordering to be wrong somewhere.
    /// Each of the four actions checks the setting explicitly, one line, in the right order.
    /// </remarks>
    protected virtual async Task<PackName> GetCurrentManagerPackNameAsync()
    {
        var customer = await _workContext.GetCurrentCustomerAsync();
        var manager = await _packNameService.GetPackNameByCustomerIdAsync(customer.Id);

        return (manager?.Manager ?? false) ? manager : null;
    }

    /// <summary>
    /// The store a manager belongs to, resolved from the branch their own packname list hangs from.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>PackName</c> has no <c>StoreId</c> (the store comes from list → branch) and
    /// <c>MultiStoreService.GetManagerStoreIdAsync</c> is protected and not on the interface, so
    /// <c>GetBranchByPackNameIdAsync</c> is the route. It skips deleted branches.
    /// </para>
    /// <para>
    /// <b>Never take the store from the request.</b> <c>MultiStoreSettings</c> is store-scoped and
    /// carries this controller's authorisation gates — <c>AllowManagersToAddPackNames</c> and
    /// <c>LimitAssignedBranches</c>. Loading them for a posted store id lets a manager pick the
    /// store whose settings suit them. Both GETs already read the store from the
    /// list; this is how the POSTs do the same.
    /// </para>
    /// </remarks>
    protected virtual async Task<int?> ResolveManagerStoreIdAsync(PackName manager)
    {
        if (manager == null)
            return null;

        var managerBranch = await _packNameService.GetBranchByPackNameIdAsync(manager.Id);

        return managerBranch?.StoreId;
    }

    /// <summary>
    /// Records a model error when a posted department or cost centre does not belong to the given store.
    /// </summary>
    /// <remarks>
    /// Both ids come from dropdowns the model factory fills per store, and both were persisted
    /// unvalidated. <c>DepartmentId</c> is not cosmetic: ownership is granted through it
    /// (<c>MultiStoreService.GetOwnedPackNameIdsByManagerIdAsync</c> matches
    /// <c>pn.DepartmentId</c> against the manager's departments), so a posted foreign department
    /// moved one store's packname into another store's manager scope.
    /// The store must be the one resolved from the manager, never the posted <c>StoreId</c>.
    /// </remarks>
    /// <param name="packName">
    /// The packname being edited, or null on an insert. Only used to tell a NEW assignment onto an
    /// inactive department or cost centre from a re-save of one that was already there.
    /// </param>
    protected virtual async Task ValidateDepartmentAndCostCenterAsync(PackNameRegisterModel model, int storeId, PackName packName = null)
    {
        if (model.DepartmentId.GetValueOrDefault() > 0)
        {
            var department = await _multiStoreService.GetDepartmentByIdAsync(model.DepartmentId.Value);

            if (department == null || department.Deleted || department.StoreId != storeId)
                ModelState.AddModelError(nameof(model.DepartmentId), await _localizationService.GetResourceAsync("Plugins.CW.Core.PackNames.PackName.Field.Department.WrongStore"));

            //Active means "no new assignments" - a packname already on a deactivated department still
            //saves. See MultiStoreController.ValidatePackNameTargetAsync, which applies the same rule
            //on the admin side.
            else if (!department.Active && department.Id != packName?.DepartmentId)
                ModelState.AddModelError(nameof(model.DepartmentId), await _localizationService.GetResourceAsync("Plugins.CW.Core.PackNames.PackName.Field.Department.Inactive"));
        }

        if (model.CostCenterId.GetValueOrDefault() > 0)
        {
            var costCenter = await _multiStoreService.GetCostCenterByIdAsync(model.CostCenterId.Value);

            if (costCenter == null || costCenter.Deleted || costCenter.StoreId != storeId)
                ModelState.AddModelError(nameof(model.CostCenterId), await _localizationService.GetResourceAsync("Plugins.CW.Core.PackNames.PackName.Field.CostCenter.WrongStore"));

            else if (!costCenter.Active && costCenter.Id != packName?.CostCenterId)
                ModelState.AddModelError(nameof(model.CostCenterId), await _localizationService.GetResourceAsync("Plugins.CW.Core.PackNames.PackName.Field.CostCenter.Inactive"));
        }
    }

    /// <summary>
    /// The packname's gender is required when the store asks for one. The store is the manager's, never the posted
    /// <c>StoreId</c>
    /// </summary>
    /// <param name="model">The posted form</param>
    /// <param name="storeSettings">The settings of the manager's store</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    protected virtual async Task ValidateGenderAsync(PackNameRegisterModel model, MultiStoreSettings storeSettings)
    {
        if (storeSettings.GenderEnabled && string.IsNullOrEmpty(model.Gender))
            ModelState.AddModelError(nameof(model.Gender), await _localizationService.GetResourceAsync("Plugins.CW.Core.PackNames.PackName.Field.Gender.Required"));
    }

    /// <summary>
    /// Why a branch has no list for the manager's new packname: under LimitAssignedBranches none of its lists is his;
    /// otherwise it has no active list
    /// </summary>
    /// <param name="storeSettings">The settings of the manager's store</param>
    /// <returns>A task that represents the asynchronous operation; the task result contains the message</returns>
    protected virtual Task<string> NoTargetListMessageAsync(MultiStoreSettings storeSettings)
    {
        return _localizationService.GetResourceAsync(storeSettings.LimitAssignedBranches
            ? "Plugins.CW.Core.PackNames.PackName.Field.Branch.NoAccess"
            : "Plugins.CW.Core.PackNames.PackName.Field.Branch.NoActiveList");
    }

    /// <summary>
    /// Makes the packname's subsidy assignments match the form: a ticked subsidy is assigned, an unticked one is
    /// removed. Returns the names of the subsidies assigned afterwards.
    /// </summary>
    /// <remarks>
    /// Only the subsidies the form could offer are touched: the store's active ones. An assignment to a subsidy
    /// that is no longer active is not on the form and is left alone. Without the store setting, or with subsidies
    /// switched off for the store, the form carries no subsidy fields and nothing is changed — the setting is
    /// checked here and not only in the view, so a hand-built post cannot assign money the store never offered.
    /// </remarks>
    /// <param name="packName">The packname just created or edited</param>
    /// <param name="model">The posted form</param>
    /// <param name="storeSettings">The settings of the manager's store</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the names of the active subsidies assigned to the packname
    /// </returns>
    protected virtual async Task<IList<string>> SaveSubsidyAssignmentsAsync(PackName packName, PackNameRegisterModel model, MultiStoreSettings storeSettings)
    {
        if (!storeSettings.AllowSubsidySelection || !await _cwFeatureService.IsEnabledAsync(CWFeature.Subsidy, model.StoreId))
            return new List<string>();

        var customer = await _workContext.GetCurrentCustomerAsync();
        var wanted = (model.SubsidyIds ?? new List<int>()).ToHashSet();
        var current = await _subsidyService.GetAllSubsidyPackNamesByPackNameIdAsync(packName.Id);
        var assigned = new List<string>();

        foreach (var subsidy in await _subsidyService.GetActiveSubsidiesByStoreIdAsync(model.StoreId))
        {
            var mapping = current.FirstOrDefault(existing => existing.SubsidyId == subsidy.Id);

            if (wanted.Contains(subsidy.Id))
            {
                assigned.Add(subsidy.Name);

                if (mapping != null)
                    continue;

                await _subsidyService.InsertSubsidyPackNameAsync(new SubsidyPackName
                {
                    SubsidyId = subsidy.Id,
                    PackNameId = packName.Id,
                    CreatorId = customer.Id,
                    CreatedOnUtc = DateTime.UtcNow,
                    Active = true
                });

                await _customerActivityService.InsertActivityAsync("AddSubsidyPackName",
                    string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.ActivityLog.AddSubsidyPackName"), subsidy.Id, 1));
            }
            else if (mapping != null)
            {
                await _subsidyService.DeleteSubsidyPackNameAsync(mapping);

                await _customerActivityService.InsertActivityAsync("DeleteSubsidyPackName",
                    string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.ActivityLog.DeleteSubsidyPackName"), subsidy.Id, packName.Id));
            }
        }

        return assigned;
    }

    /// <summary>
    /// The manager, store and settings behind the Excel import, or null when the current customer may not import:
    /// not a manager, no branch to resolve the store from, or a store that does not let managers add packnames, or
    /// add them by Excel
    /// </summary>
    protected virtual async Task<(PackName Manager, int StoreId, MultiStoreSettings StoreSettings)?> ResolveImportAccessAsync()
    {
        var manager = await GetCurrentManagerPackNameAsync();

        if (manager == null)
            return null;

        //The store comes from the MANAGER, never from the request. See ResolveManagerStoreIdAsync's remarks.
        var managerStoreId = await ResolveManagerStoreIdAsync(manager);

        if (managerStoreId == null)
            return null;

        var storeSettings = await _settingService.LoadSettingAsync<MultiStoreSettings>(managerStoreId.Value);

        if (!storeSettings.AllowManagersToAddPackNames || !storeSettings.AllowToAddByExcel)
            return null;

        return (manager, managerStoreId.Value, storeSettings);
    }

    /// <summary>
    /// What the add-packname form asks the registration service to create: the packname, the customer account when
    /// the store allows one and the form asked for it (never for a group), the delivery address likewise, and the
    /// subsidies ticked on the form
    /// </summary>
    protected virtual PackNameRegistration BuildRegistration(PackNameRegisterModel model, Branch branch, PackNameList packNameList, MultiStoreSettings storeSettings,
        string customerAttributesXml, string addressAttributesXml, int createdByCustomerId, int languageId)
    {
        PackNameCustomerRegistration customer = null;

        if (storeSettings.AllowToAddCustomerToPackName && model.AddCustomer && !model.Group)
        {
            customer = new PackNameCustomerRegistration
            {
                Email = model.Customer.Email,
                Username = model.Customer.Username,
                Gender = model.Customer.Gender,
                FirstName = model.Customer.FirstName,
                LastName = model.Customer.LastName,
                DateOfBirth = model.Customer.ParseDateOfBirth(),
                StreetAddress = model.Customer.StreetAddress,
                StreetAddress2 = model.Customer.StreetAddress2,
                ZipPostalCode = model.Customer.ZipPostalCode,
                City = model.Customer.City,
                County = model.Customer.County,
                CountryId = model.Customer.CountryId,
                StateProvinceId = model.Customer.StateProvinceId,
                Phone = model.Customer.Phone,
                Fax = model.Customer.Fax,
                CustomAttributesXml = customerAttributesXml
            };
        }

        Address address = null;

        if (storeSettings.AllowHomeDelivery && model.AddAddress)
        {
            address = model.Address.ToEntity();
            address.CustomAttributes = addressAttributesXml;
        }

        return new PackNameRegistration
        {
            StoreId = model.StoreId,
            Branch = branch,
            PackNameList = packNameList,
            CreatedByCustomerId = createdByCustomerId,
            LanguageId = languageId,
            Name = model.Name,
            PrintName = model.PrintName,
            Group = model.Group,
            Gender = model.Gender,
            Colour = model.Colour,
            Comments = model.Comments,
            EmployeeNumber = model.EmployeeNumber,
            POEntry = model.POEntry,
            DepartmentId = model.DepartmentId,
            CostCenterId = model.CostCenterId,
            Customer = customer,
            Address = address,
            Subsidies = (model.SubsidyIds ?? new List<int>()).Select(id => new PackNameSubsidyAssignment { SubsidyId = id }).ToList()
        };
    }

    #endregion

    #region Methods

    /// <summary>
    /// One page of the packnames the current manager may order for in a selector entry
    /// </summary>
    /// <param name="packNameListId">The selector entry: a packname list, or the negated identifier of a department</param>
    /// <param name="term">What the manager typed; empty for the first page</param>
    /// <param name="selectedPackNameId">The packname in context, kept on the first page; 0 for none</param>
    /// <returns>The page as JSON: PackNames and HasMore</returns>
    [HttpPost]
    public virtual async Task<IActionResult> PackNames(int packNameListId, string term, int selectedPackNameId)
    {
        //MANAGERS ONLY. The factory validates that the LIST is in the caller's scope, but
        //packname_get_listids_by_manager has no Manager = 1 predicate - its path 1 returns every
        //active list in the caller's own branch - so for a plain employee "their own list" resolved
        //as owned and this endpoint returned the whole branch roster. The selector UI is hidden from
        //employees (GetAvailablePackNameListsAsync returns nothing for a non-manager); the endpoint
        //was not.
        var manager = await GetCurrentManagerPackNameAsync();

        if (manager == null)
            return AccessDeniedView();

        var model = await _packNameModelFactory.PreparePackNamesModelForManagerAsync(packNameListId, term, selectedPackNameId);
        return Json(model);
    }

    /// <summary>
    /// The current manager's cart quantities by packname, for the cart badges of the selector
    /// </summary>
    /// <returns>A JSON object keyed by packname identifier; a packname without cart lines is not in it</returns>
    [HttpPost]
    public virtual async Task<IActionResult> PackNameCartCounts()
    {
        if (await GetCurrentManagerPackNameAsync() == null)
            return AccessDeniedView();

        return Json(await _packNameService.GetCartCountsByCustomerAsync());
    }

    /// <summary>
    /// Sets (or clears) the ordering context for the current customer.
    /// </summary>
    /// <remarks>
    /// <b>POST, not GET.</b> This writes the customer's <c>SelectedPackNameId</c> /
    /// <c>SelectedPackNameListId</c> attributes, so as a GET it was a state-changing GET exempt from
    /// the class-level <c>[AutoValidateAntiforgeryToken]</c> (which only validates unsafe methods).
    /// An <c>&lt;img src="/PackName/SelectPackName/123"&gt;</c> on any page the victim visited
    /// switched a manager's ordering context to another employee, so their next order was charged
    /// against that employee's allowance - no interaction, no token. The four
    /// callers now submit a token-bearing form; the action still answers with a redirect, so the
    /// browser navigation they relied on is unchanged.
    /// </remarks>
    [HttpPost]
    public virtual async Task<IActionResult> SelectPackName(int id, int listId, string returnUrl)
    {
        var customer = await _workContext.GetCurrentCustomerAsync();

        if (id == 0)
        {
            await _packNameService.UnsetPackNameAsync(customer);
        }
        else
        {
            //We need to validate if the packName can be managed by the current customer, to avoid that a user can set a packName that doesn't belong to any of the branches that the manager has access to
            var manager = await _packNameService.GetPackNameByCustomerIdAsync(customer.Id);

            //A customer with no packname at all has nothing to select. Without this test the code
            //dereferences a null manager and returns 500.
            if (manager == null)
                return AccessDeniedView();

            //A NON-MANAGER owns only himself. IsPackNameOwnedByManagerId answers a question about a
            //manager's scope and says nothing about whether this customer IS one, so without this
            //test a plain employee could make a colleague in the same branch their ordering context
            //by typing the URL - the selector UI is hidden, the endpoint was not.
            var packNameId = manager.Manager && await _multiStoreService.IsPackNameOwnedByManagerId(manager.Id, id)
                ? id
                : manager.Id;

            var packName = await _packNameService.GetPackNameByIdAsync(packNameId);
            if (packName != null)
            {
                var branch = await _packNameService.GetBranchByPackNameIdAsync(packName.Id);

                //A posted listId is a hint, not an instruction: it is stored on the customer and read
                //back as the selector's selected option. Accept it only if the manager owns that list
                //or it is one of their departments (negative = department), otherwise derive it.
                var packNameListIds = await _multiStoreService.GetPackNameListIdsByManagerIdAsync(manager.Id);

                if (listId > 0 && !packNameListIds.Contains(listId))
                    listId = 0;

                if (listId < 0 && !(await _multiStoreService.GetDepartmentsByManagerIdAsync(manager.Id)).Any(d => d.Id == Math.Abs(listId)))
                    listId = 0;

                if (listId == 0)
                {
                    //If 0 there could be two scenarios,
                    //either the packName is in a list owned by the manager and we can get the listId from the packName, or
                    //the packName is in a list that is not owned by the manager but the manager has access to it, in that the packName is in a department and the manager has access to that department
                    listId = packNameListIds.Contains(packName.PackNameListId) ? packName.PackNameListId : -packName.DepartmentId ?? 0;
                }

                if (branch != null)
                    await _packNameService.SetPackNameAsync(packNameId, listId, customer, branch);
            }
        }

        //a store that sends customers to the subsidy catalogue has its say before the page they came from
        var landingRoute = await _packNameLandingService.ResolveSelectionLandingRouteAsync(customer);

        if (landingRoute != null)
            return new RedirectToRouteResult(landingRoute, null);

        //redirect to the return URL if it's specified
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return new RedirectResult(returnUrl);

        return new RedirectToRouteResult("Homepage", null);
    }

    public virtual async Task<IActionResult> AddPackName(int id)
    {
        var manager = await GetCurrentManagerPackNameAsync();

        if (manager == null)
            return AccessDeniedView();

        //Id is a packNameList id (the selector posts the list it is standing on); 0 or less means
        //"the manager's own list".
        //THE LIST ID IS A REQUEST VALUE, and the model factory fans the list's store out into every
        //dropdown it fills - departments, cost centres and, when that store has LimitAssignedBranches
        //off, its whole branch tree. So the form opens on a posted list only when the manager owns it;
        //any other list (one the selector offers through the manager's departments, or a typed id)
        //opens it on the manager's own list. The list only preselects the branch: the save puts the
        //packname in a list of the branch chosen on the form.
        if (id > 0 && !(await _multiStoreService.GetPackNameListIdsByManagerIdAsync(manager.Id)).Contains(id))
            id = 0;

        //NotFound, not InvalidOperationException: three throws here returned 500 to the browser for
        //what are ordinary bad-input cases, and 4.6 returned NotFound().
        var packNameList = id > 0
            ? await _packNameService.GetPackNameListByIdAsync(id)
            : await _packNameService.GetPackNameListByIdAsync(manager.PackNameListId);

        if (packNameList == null)
            return NotFound();

        //The store comes from the MANAGER, never from a posted id. Deriving it from the list was
        //right only because the list is now known to be the manager's; deriving it from the manager
        //is right whatever the request says, and is the same rule the two POSTs follow.
        var managerStoreId = await ResolveManagerStoreIdAsync(manager);

        if (managerStoreId == null)
            return AccessDeniedView();

        var storeSettings = await _settingService.LoadSettingAsync<MultiStoreSettings>(managerStoreId.Value);

        if (!storeSettings.AllowManagersToAddPackNames)
            return AccessDeniedView();

        var model = new PackNameRegisterModel();
        model = await _packNameModelFactory.PreparePackNameRegisterModelAsync(model, manager, packNameList.StoreId, packNameList.BranchId ?? 0);

        return PartialView("_AddPackName", model);
    }

    [HttpPost]
    [CheckAccessPublicStore(ignore: true)]
    public virtual async Task<IActionResult> AddPackName(PackNameRegisterModel model, IFormCollection form)
    {
        //the current customer must be a manager, and the store must allow managers to maintain packnames
        var manager = await GetCurrentManagerPackNameAsync();

        if (manager == null)
            return AccessDeniedView();

        //The store comes from the MANAGER, never from the posted model: MultiStoreSettings carries
        //the gates below, so a posted store id is a settings-shopping bypass. model.StoreId is
        //overwritten so the redisplay and the registration cannot disagree with the gate.
        var managerStoreId = await ResolveManagerStoreIdAsync(manager);

        if (managerStoreId == null)
            return AccessDeniedView();

        model.StoreId = managerStoreId.Value;

        var storeSettings = await _settingService.LoadSettingAsync<MultiStoreSettings>(model.StoreId);

        if (!storeSettings.AllowManagersToAddPackNames)
            return AccessDeniedView();

        var branch = await _branchService.GetBranchByIdAsync(model.BranchId);

        if (branch == null)
            ModelState.AddModelError("BranchId", await _localizationService.GetResourceAsync("Plugins.CW.Core.PackNames.PackName.Field.Branch.Required"));

        //Every insert is a NEW assignment, so there is no already-on-it case to spare here - unlike
        //the edit path above, which only refuses a branch the packname is moving TO.
        else if (!branch.Active || branch.Deleted)
            ModelState.AddModelError("BranchId", await _localizationService.GetResourceAsync("Plugins.CW.Core.PackNames.PackName.Field.Branch.Inactive"));

        await ValidateDepartmentAndCostCenterAsync(model, model.StoreId);
        await ValidateGenderAsync(model, storeSettings);

        var customerAttributesXml = string.Empty;

        if (storeSettings.AllowToAddCustomerToPackName && model.AddCustomer && !model.Group)
        {
            if (storeSettings.GenderEnabled && _customerSettings.GenderEnabled)
                model.Customer.Gender = model.Gender == "I" ? "M" : model.Gender;

            //custom customer attributes
            customerAttributesXml = await ParseCustomCustomerAttributesAsync(form);
            var customerAttributeWarnings = await _customerAttributeParser.GetAttributeWarningsAsync(customerAttributesXml);
            foreach (var error in customerAttributeWarnings)
            {
                ModelState.AddModelError("", error);
            }
        }

        var customAttributes = string.Empty;

        if (model.AddAddress)
        {
            //custom address attributes
            customAttributes = await _addressAttributeParser.ParseCustomAttributesAsync(form, NopCommonDefaults.AddressAttributeControlName);
            var customAttributeWarnings = await _addressAttributeParser.GetAttributeWarningsAsync(customAttributes);
            foreach (var error in customAttributeWarnings)
            {
                ModelState.AddModelError("", error);
            }
        }

        //the registration fills these fields itself (the branch's name as Company, no VAT number), so what the form
        //posts for them is not checked
        foreach (var field in PackNameServicesDefaults.ServerSuppliedCustomerFields)
            ModelState.Remove($"{nameof(model.Customer)}.{field}");

        foreach (var field in PackNameServicesDefaults.ServerSuppliedAddressFields)
            ModelState.Remove($"{nameof(model.Address)}.{field}");

        //the target list, restricted to the manager's own store
        var packNameList = await _packNameRegistrationService.ResolveTargetPackNameListAsync(manager, branch, storeSettings);

        if (packNameList == null)
            ModelState.AddModelError("BranchId", await NoTargetListMessageAsync(storeSettings));

        //one packname per name in a list
        else if (await _packNameService.PackNameNameExistsAsync(packNameList.Id, model.Name))
            ModelState.AddModelError("Name", string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.PackNames.PackName.Field.Name.Exists"), model.Name, branch.Name));

        if (ModelState.IsValid)
        {
            var customer = await _workContext.GetCurrentCustomerAsync();
            var language = await _workContext.GetWorkingLanguageAsync();

            var registration = BuildRegistration(model, branch, packNameList, storeSettings, customerAttributesXml, customAttributes, customer.Id, language.Id);
            var registered = await _packNameRegistrationService.RegisterAsync(registration);

            if (registered.Success)
            {
                return Json(new
                {
                    success = true,
                    packNameId = registered.PackName.Id
                });
            }

            //nothing was created: the name was taken since the check above, or the customer account could not be
            //registered; each message goes where the form shows it
            foreach (var error in registered.Errors)
            {
                ModelState.AddModelError(registered.NameTaken ? nameof(model.Name) : "AddCustomer", error);
            }
        }

        //If we got this far, something failed, redisplay form
        model = await _packNameModelFactory.PreparePackNameRegisterModelAsync(model, manager, model.StoreId, model.BranchId, customAttributes, customerAttributesXml);

        return Json(new
        {
            success = false,
            packnameinfosectionhtml = await RenderPartialViewToStringAsync("_AddPackName", model)
        });
    }

    public virtual async Task<IActionResult> EditPackName(int id)
    {
        var packName = await _packNameService.GetPackNameByIdAsync(id);

        if (packName == null || packName.Deleted)
            return NotFound();

        //THIS GET USED TO HAVE NO CHECK of any kind: any signed-in customer could read any packname's
        //edit model, in any store, by typing the id. The POST was guarded on "is a manager"; this was
        //not guarded at all.
        var manager = await GetCurrentManagerPackNameAsync();

        if (manager == null)
            return AccessDeniedView();

        if (!await _multiStoreService.IsPackNameOwnedByManagerId(manager.Id, packName.Id))
            return AccessDeniedView();

        //A PackName has no StoreId of its own - the store comes from its list.
        var packNameList = await _packNameService.GetPackNameListByIdAsync(packName.PackNameListId);

        if (packNameList == null)
            return NotFound();

        var storeSettings = await _settingService.LoadSettingAsync<MultiStoreSettings>(packNameList.StoreId);

        if (!storeSettings.AllowManagersToAddPackNames)
            return AccessDeniedView();

        var model = await _packNameModelFactory.PreparePackNameEditModelAsync(null, packName);

        return PartialView("_EditPackName", model);
    }

    [HttpPost]
    [CheckAccessPublicStore(ignore: true)]
    public virtual async Task<IActionResult> EditPackName(PackNameRegisterModel model)
    {
        var packName = await _packNameService.GetPackNameByIdAsync(model.Id);

        //RETURN, do not merely record. Execution used to continue to packName.PackNameListId below and
        //throw NullReferenceException before any validation ran.
        if (packName == null || packName.Deleted)
            return NotFound();

        //Validate that the current customer is a manager, that the store allows it, and that this
        //packname is theirs. The ownership test used to run AFTER UpdatePackName, purely to decide
        //which packname to select afterwards - so any manager could rename, re-gender or move any
        //packname, into another store, by posting its id.
        var manager = await GetCurrentManagerPackNameAsync();

        if (manager == null)
            return AccessDeniedView();

        //The store comes from the MANAGER, never from the posted model. See
        //ResolveManagerStoreIdAsync's remarks.
        var managerStoreId = await ResolveManagerStoreIdAsync(manager);

        if (managerStoreId == null)
            return AccessDeniedView();

        model.StoreId = managerStoreId.Value;

        var storeSettings = await _settingService.LoadSettingAsync<MultiStoreSettings>(model.StoreId);

        if (!storeSettings.AllowManagersToAddPackNames)
            return AccessDeniedView();

        if (!await _multiStoreService.IsPackNameOwnedByManagerId(manager.Id, packName.Id))
            return AccessDeniedView();

        //packName passed: this is the EDIT path, so a department or cost centre the packname is
        //already on stays valid even after it is deactivated.
        await ValidateDepartmentAndCostCenterAsync(model, model.StoreId, packName);
        await ValidateGenderAsync(model, storeSettings);

        var branch = await _branchService.GetBranchByIdAsync(model.BranchId);
        var packNameList = await _packNameService.GetPackNameListByIdAsync(packName.PackNameListId);

        if (packNameList == null)
            ModelState.AddModelError("BranchId", await _localizationService.GetResourceAsync("Plugins.CW.Core.PackNames.PackName.Field.PackNameList.NotFound"));

        var branchChanged = false;

        if (branch == null)
            ModelState.AddModelError("BranchId", await _localizationService.GetResourceAsync("Plugins.CW.Core.PackNames.PackName.Field.Branch.Required"));
        else
        {
            if (packNameList?.BranchId != branch.Id) //The branch has been changed
            {
                //ACTIVE, ON THE WRITE. The picker no longer offers inactive branches; without
                //this the endpoint still took one, so the Active rule was decoration - a stale page in
                //another tab is enough, no forged post needed. This branch of the if IS the definition
                //of a new assignment, so an employee already on a since-deactivated branch never
                //reaches it and keeps saving - scoped on read means scoped on write.
                if (!branch.Active || branch.Deleted)
                    ModelState.AddModelError("BranchId", await _localizationService.GetResourceAsync("Plugins.CW.Core.PackNames.PackName.Field.Branch.Inactive"));

                packNameList = await _packNameRegistrationService.ResolveTargetPackNameListAsync(manager, branch, storeSettings);

                if (packNameList == null)
                    ModelState.AddModelError("BranchId", await NoTargetListMessageAsync(storeSettings));

                branchChanged = true;
            }
        }

        //one packname per name in a list, for a new name or a move: a packname saved again with its name keeps it, even
        //when another of the list has the same one (provisioning names packnames after customers without the rule)
        var nameChanged = !string.Equals(model.Name?.Trim(), packName.Name?.Trim(), StringComparison.OrdinalIgnoreCase);

        if (packNameList != null && (nameChanged || branchChanged)
            && await _packNameService.PackNameNameExistsAsync(packNameList.Id, model.Name, packName.Id))
            ModelState.AddModelError("Name", string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.PackNames.PackName.Field.Name.Exists"), model.Name, branch?.Name ?? string.Empty));

        var packNameCustomer = await _customerService.GetCustomerByIdAsync(packName.CustomerId ?? 0);

        if (packName.IsCustomer && packNameCustomer == null)
            ModelState.AddModelError("", await _localizationService.GetResourceAsync("Plugins.CW.Core.PackNames.PackName.Field.Customer.NotFound"));

        //the add form's rule: a packname with its own customer account is a person, not a group. The message goes to
        //the dialog's summary: the Group checkbox has no message of its own
        if (model.Group && packName.IsCustomer)
            ModelState.AddModelError("", await _localizationService.GetResourceAsync("Plugins.CW.Core.PackNames.PackName.Field.Group.NoCustomer"));

        if (ModelState.IsValid)
        {
            //the packname, its subsidies and its customer's branch are saved together or not at all
            using (var scope = PackNameTransaction.Begin(TransactionManager.DefaultTimeout))
            {
                await UpdatePackName(packName, packNameList, model);

                await SaveSubsidyAssignmentsAsync(packName, model, storeSettings);

                //If the branch has changed and the packName is a customer, we need to move the branch to the new one
                if (branchChanged && packName.IsCustomer)
                {
                    //INSERT when there is no mapping row. CustomerBranch is keyed by the customer id and
                    //is written only when an account is registered for a packname or an admin links a customer
                    //to a branch, so other packname customers have none - and this ran AFTER UpdatePackName had already
                    //committed the move, so the 500 left the packname moved and the customer's branch
                    //not.
                    var customerBranch = await _customerService.GetCustomerBranchByIdAsync(packName.CustomerId ?? 0);

                    if (customerBranch == null)
                    {
                        await _customerService.InsertCustomerBranchAsync(new CustomerBranch
                        {
                            Id = packName.CustomerId.Value,
                            BranchId = branch.Id,
                            LastModifiedDateUtc = DateTime.UtcNow
                        });
                    }
                    else
                    {
                        customerBranch.BranchId = branch.Id;
                        await _customerService.UpdateCustomerBranchAsync(customerBranch);
                    }
                }

                scope.Complete();
            }

            //Verify that the manager can still select the packname: the branch may have moved to one
            //whose lists the manager does not own, even though the move itself was permitted.
            var packNameId = await _multiStoreService.IsPackNameOwnedByManagerId(manager.Id, packName.Id) ? packName.Id : manager.Id;

            return Json(new
            {
                success = true,
                packNameId
            });
        }

        //If we got this far, something failed, redisplay form
        model = await _packNameModelFactory.PreparePackNameEditModelAsync(model, packName);

        return Json(new
        {
            success = false,
            packnameinfosectionhtml = await RenderPartialViewToStringAsync("_EditPackName", model)
        });
    }

    [HttpPost]
    public virtual async Task<IActionResult> DeletePackName(int id)
    {
        var customer = await _workContext.GetCurrentCustomerAsync();
        var packName = await _packNameService.GetPackNameByIdAsync(id);
        var currentPackName = await _packNameContext.GetCurrentPackNameAsync(customer);

        if (packName == null || packName.Deleted)
            return AccessDeniedView();

        //Validate that the current customer is a manager
        var manager = await GetCurrentManagerPackNameAsync();

        if (manager == null)
            return AccessDeniedView();

        //Verify that the manager can remove the packname
        if (!await _multiStoreService.IsPackNameOwnedByManagerId(manager.Id, packName.Id))
            return AccessDeniedView();

        if (id == manager.Id)
            return AccessDeniedView();

        //The same gate the other three maintenance actions apply. Delete was the only one that never
        //read it, so with "managers may maintain packnames" switched OFF a manager could still
        //permanently delete an employee's packname and its allowance history.
        var managerStoreId = await ResolveManagerStoreIdAsync(manager);

        if (managerStoreId == null)
            return AccessDeniedView();

        var storeSettings = await _settingService.LoadSettingAsync<MultiStoreSettings>(managerStoreId.Value);

        if (!storeSettings.AllowManagersToAddPackNames)
            return AccessDeniedView();

        await _packNameService.DeletePackNameAsync(packName);

        //activity log
        await _customerActivityService.InsertActivityAsync("DeletePackName",
            string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.ActivityLog.DeletePackName"), packName.Name), packName);

        if (currentPackName?.Id == id)
            return Json(new
            {
                success = true,
                packNameId = manager.Id
            });

        return Json(new
        {
            success = true,
        });
    }

    /// <summary>
    /// The manager's Excel import page
    /// </summary>
    public virtual async Task<IActionResult> ImportPackNames()
    {
        var access = await ResolveImportAccessAsync();

        if (access == null)
            return AccessDeniedView();

        var (manager, storeId, _) = access.Value;

        var layout = await _packNameImportService.BuildLayoutAsync(manager, storeId);
        var model = await _packNameModelFactory.PreparePackNameImportModelAsync(layout);

        return View("~/Plugins/CW.Core/Views/PackName/ImportPackNames.cshtml", model);
    }

    /// <summary>
    /// The empty template of the manager's import file; its columns follow the store's settings
    /// </summary>
    public virtual async Task<IActionResult> ImportPackNamesTemplate()
    {
        var access = await ResolveImportAccessAsync();

        if (access == null)
            return AccessDeniedView();

        var (manager, storeId, _) = access.Value;

        var layout = await _packNameImportService.BuildLayoutAsync(manager, storeId);
        var template = await _packNameImportService.GenerateTemplateAsync(layout);

        return File(template, MimeTypes.TextXlsx, PackNameImportDefaults.TemplateFileName);
    }

    /// <summary>
    /// Imports an uploaded file: every row is checked, and the packnames are created only when all of them pass.
    /// Answers the page's script with the summary or the errors
    /// </summary>
    [HttpPost]
    [CheckAccessPublicStore(ignore: true)]
    public virtual async Task<IActionResult> ImportPackNames(IFormFile importFile)
    {
        var access = await ResolveImportAccessAsync();

        if (access == null)
            return AccessDeniedView();

        var fileError = await _packNameImportService.CheckFileAsync(importFile?.FileName, importFile?.Length ?? 0);

        if (fileError != null)
            return Json(new { message = fileError });

        var (manager, storeId, _) = access.Value;

        var layout = await _packNameImportService.BuildLayoutAsync(manager, storeId);
        var language = await _workContext.GetWorkingLanguageAsync();

        await using var stream = importFile.OpenReadStream();
        var result = await _packNameImportService.ImportAsync(layout, stream, language.Id);

        return Json(result.ToPageAnswer());
    }

    /// <summary>
    /// A copy of the uploaded file with an Errors column that names each row's problems; the same file the page
    /// just refused, posted again by its download button
    /// </summary>
    [HttpPost]
    [CheckAccessPublicStore(ignore: true)]
    public virtual async Task<IActionResult> ImportPackNamesErrorReport(IFormFile importFile)
    {
        var access = await ResolveImportAccessAsync();

        if (access == null)
            return AccessDeniedView();

        if (await _packNameImportService.CheckFileAsync(importFile?.FileName, importFile?.Length ?? 0) != null)
            return RedirectToAction(nameof(ImportPackNames));

        var (manager, storeId, _) = access.Value;

        var layout = await _packNameImportService.BuildLayoutAsync(manager, storeId);

        await using var stream = importFile.OpenReadStream();
        var report = await _packNameImportService.BuildErrorReportAsync(layout, stream);

        if (report == null)
            return RedirectToAction(nameof(ImportPackNames));

        return File(report, MimeTypes.TextXlsx, $"{Path.GetFileNameWithoutExtension(importFile.FileName)}-errors.xlsx");
    }

    #endregion
}