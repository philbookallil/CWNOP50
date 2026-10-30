using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Core.Domain.Catalog;
using Nop.Plugin.CW.Core.Areas.Admin.Models.Catalog;
using Nop.Plugin.CW.Core.Services.MultiStores;
using Nop.Services.Security;
using Nop.Services.Stores;
using Nop.Web.Areas.Admin.Models.Catalog;
using Nop.Web.Framework.Components;
using Nop.Web.Framework.Models;

namespace Nop.Plugin.CW.Core.Areas.Admin.Components;

/// <summary>
/// Represents view component to display field to select departments
/// </summary>
public partial class AclDepartmentsViewComponent : NopViewComponent
{
    #region Fields

    protected readonly IMultiStoreService _multiStoreService;
    protected readonly IAclService _aclService;
    protected readonly IStoreService _storeService;

    #endregion

    #region Ctor

    public AclDepartmentsViewComponent(IMultiStoreService multiStoreService,
        IAclService aclService,
        IStoreService storeService)
    {
        _multiStoreService = multiStoreService;
        _aclService = aclService;
        _storeService = storeService;
    }

    #endregion

    #region Methods

    /// <summary>
    /// Invoke view component
    /// </summary>
    /// <param name="additionalData">The admin model being edited</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the view component result
    /// </returns>
    /// <remarks>
    /// Options are grouped by store name and narrowed to the entity's own stores. Both matter, and the
    /// grouping matters more: department names repeat across stores ("Warehouse" in each), the roles
    /// behind them are per-store, and picking the wrong one restricts the entity to a role nobody in
    /// its own store holds. An entity mapped to no stores is visible everywhere, so nothing narrows it
    /// and the group label is the only thing telling the two apart.
    /// The POST is the real boundary and re-applies the same rule from the entity —
    /// <see cref="Services.Security.AclOverrideService.SaveAclDepartmentAsync"/>.
    /// </remarks>
    public async Task<IViewComponentResult> InvokeAsync(object additionalData)
    {
        var (entityId, selectedStoreIds) = additionalData switch
        {
            CategoryModel c => (c.Id, c.SelectedStoreIds),
            ManufacturerModel m => (m.Id, m.SelectedStoreIds),
            ProductModel p => (p.Id, p.SelectedStoreIds),
            _ => (0, null)
        };

        if (selectedStoreIds == null)
            return Content(string.Empty);

        var entityName = additionalData switch
        {
            CategoryModel => nameof(Category),
            ManufacturerModel => nameof(Manufacturer),
            _ => nameof(Product)
        };

        var departments = await _multiStoreService.GetAllDepartmentsAsync();

        //No store mapping means the entity is visible in every store, so every department is
        //permissible - the same rule the POST applies.
        if (selectedStoreIds.Any())
            departments = departments.Where(dep => selectedStoreIds.Contains(dep.StoreId)).ToList();

        var selectedCustomerRoleIds = (await _aclService.GetCustomerRoleIdsWithAccessAsync(entityId, entityName)).ToList();
        var selectedDepartmentIds = departments
            .Where(dep => selectedCustomerRoleIds.Contains(dep.CustomerRoleId ?? 0))
            .Select(dep => dep.Id)
            .ToList();

        //ACTIVE IS APPLIED AFTER THE SELECTION IS COMPUTED, NOT BEFORE. Restricting an entity to a
        //department is a new assignment, so inactive ones leave the list - but a department already
        //restricting this entity stays, or saving the form would silently drop that restriction and
        //widen who can see the product. Computing selectedDepartmentIds from the unfiltered list first
        //is what makes that possible.
        departments = departments
            .Where(dep => dep.Active || selectedDepartmentIds.Contains(dep.Id))
            .ToList();

        var stores = await _storeService.GetAllStoresAsync();
        var storeNames = stores.ToDictionary(store => store.Id, store => store.Name);

        //ONE INSTANCE PER STORE, SHARED BY EVERY DEPARTMENT IN IT. Html.ListBox groups options by
        //REFERENCE equality of SelectListItem.Group, not by its Name - so allocating a new group inside
        //the Select below gave each option a group of its own and repeated the store heading above every
        //department ("Your store name" three times, one department under each).
        //MVC's own grouping emits each group once with all its items, so the department ordering here
        //(by Name, from GetAllDepartmentsAsync) does not need to be re-sorted by store.
        var storeGroups = new Dictionary<int, SelectListGroup>();

        SelectListGroup GroupFor(int storeId)
        {
            if (!storeGroups.TryGetValue(storeId, out var group))
            {
                group = new SelectListGroup
                {
                    Name = storeNames.TryGetValue(storeId, out var storeName) ? storeName : storeId.ToString()
                };

                storeGroups[storeId] = group;
            }

            return group;
        }

        var model = new AclDepartmentModel
        {
            EntityId = entityId,
            //the same flag nop's customer role field reads, so both fields show in the same mode
            IsAdvancedSetting = additionalData is not IAclSupportedModel aclSupportedModel || aclSupportedModel.IsAdvancedSetting,
            SelectedDepartmentIds = selectedDepartmentIds,
            AvailableDepartments = departments.Select(dep => new SelectListItem
            {
                Value = dep.Id.ToString(),
                Text = dep.Name,
                Selected = selectedDepartmentIds.Contains(dep.Id),
                //The whole point: two stores each with a "Warehouse" are indistinguishable without this.
                Group = GroupFor(dep.StoreId)
            }).ToList()
        };

        return View(model);
    }

    #endregion
}
