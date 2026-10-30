using Nop.Core.Domain.Customers;
using Nop.Core.Infrastructure.Mapper;
using Nop.Plugin.CW.Core.Areas.Admin.Models.Branches;
using Nop.Plugin.CW.Core.Areas.Admin.Models.Customers;
using Nop.Plugin.CW.Core.Areas.Admin.Models.PackNames;
using Nop.Plugin.CW.Core.Areas.Admin.Models.Settings;
using Nop.Plugin.CW.Core.Areas.Admin.Models.Subsidies;
using Nop.Plugin.CW.Core.Domain.Branches;
using Nop.Plugin.CW.Core.Domain.PackNames;
using Nop.Plugin.CW.Core.Domain.Settings;
using Nop.Plugin.CW.Core.Domain.Subsidies;

namespace Nop.Plugin.CW.Core.Infrastructure.Mapper;

public class CWMapperConfigurationProfile : BaseMapperProfile
{
    #region Ctor

    public CWMapperConfigurationProfile()
    {
        CreateSettingMaps();
        CreateExtendedCustomerMaps();
        CreateBranchMaps();
        CreatePackNameMaps();
        CreateSubsidyMaps();
        CreateProductGroupMaps();
    }

    #endregion

    #region Utilities

    private void CreateSettingMaps()
    {
        CreateMap<MultiStoreSettings, MultiStoreSettingsModel>();
        CreateMap<MultiStoreSettingsModel, MultiStoreSettings>();

    }

    /// <summary>
    /// Create customer maps 
    /// </summary>
    private void CreateExtendedCustomerMaps()
    {
        CreateMap<Customer, ExtendedCustomerModel>()
            .ForMember(model => model.Email, options => options.Ignore())
            .ForMember(model => model.FullName, options => options.Ignore())
            .ForMember(model => model.Company, options => options.Ignore())
            .ForMember(model => model.Phone, options => options.Ignore())
            .ForMember(model => model.ZipPostalCode, options => options.Ignore())
            .ForMember(model => model.CreatedOn, options => options.Ignore())
            .ForMember(model => model.LastActivityDate, options => options.Ignore())
            .ForMember(model => model.CustomerRoleNames, options => options.Ignore())
            .ForMember(model => model.AvatarUrl, options => options.Ignore())
            .ForMember(model => model.UsernamesEnabled, options => options.Ignore())
            .ForMember(model => model.Password, options => options.Ignore())
            .ForMember(model => model.AvailableVendors, options => options.Ignore())
            .ForMember(model => model.GenderEnabled, options => options.Ignore())
            .ForMember(model => model.NeutralGenderEnabled, options => options.Ignore())
            .ForMember(model => model.Gender, options => options.Ignore())
            .ForMember(model => model.FirstNameEnabled, options => options.Ignore())
            .ForMember(model => model.FirstName, options => options.Ignore())
            .ForMember(model => model.LastNameEnabled, options => options.Ignore())
            .ForMember(model => model.LastName, options => options.Ignore())
            .ForMember(model => model.DateOfBirthEnabled, options => options.Ignore())
            .ForMember(model => model.DateOfBirth, options => options.Ignore())
            .ForMember(model => model.CompanyEnabled, options => options.Ignore())
            .ForMember(model => model.StreetAddressEnabled, options => options.Ignore())
            .ForMember(model => model.StreetAddress, options => options.Ignore())
            .ForMember(model => model.StreetAddress2Enabled, options => options.Ignore())
            .ForMember(model => model.StreetAddress2, options => options.Ignore())
            .ForMember(model => model.ZipPostalCodeEnabled, options => options.Ignore())
            .ForMember(model => model.CityEnabled, options => options.Ignore())
            .ForMember(model => model.City, options => options.Ignore())
            .ForMember(model => model.CountyEnabled, options => options.Ignore())
            .ForMember(model => model.County, options => options.Ignore())
            .ForMember(model => model.CountryEnabled, options => options.Ignore())
            .ForMember(model => model.CountryId, options => options.Ignore())
            .ForMember(model => model.AvailableCountries, options => options.Ignore())
            .ForMember(model => model.StateProvinceEnabled, options => options.Ignore())
            .ForMember(model => model.StateProvinceId, options => options.Ignore())
            .ForMember(model => model.AvailableStates, options => options.Ignore())
            .ForMember(model => model.PhoneEnabled, options => options.Ignore())
            .ForMember(model => model.FaxEnabled, options => options.Ignore())
            .ForMember(model => model.Fax, options => options.Ignore())
            .ForMember(model => model.CustomerAttributes, options => options.Ignore())
            .ForMember(model => model.RegisteredInStore, options => options.Ignore())
            .ForMember(model => model.DisplayRegisteredInStore, options => options.Ignore())
            .ForMember(model => model.AffiliateName, options => options.Ignore())
            .ForMember(model => model.TimeZoneId, options => options.Ignore())
            .ForMember(model => model.AllowCustomersToSetTimeZone, options => options.Ignore())
            .ForMember(model => model.AvailableTimeZones, options => options.Ignore())
            .ForMember(model => model.VatNumber, options => options.Ignore())
            .ForMember(model => model.VatNumberStatusNote, options => options.Ignore())
            .ForMember(model => model.DisplayVatNumber, options => options.Ignore())
            .ForMember(model => model.LastVisitedPage, options => options.Ignore())
            .ForMember(model => model.DisplayRewardPointsHistory, options => options.Ignore())
            .ForMember(model => model.AddRewardPoints, options => options.Ignore())
            .ForMember(model => model.CustomerRewardPointsSearchModel, options => options.Ignore())
            .ForMember(model => model.SendEmail, options => options.Ignore())
            .ForMember(model => model.SendPm, options => options.Ignore())
            .ForMember(model => model.AllowSendingOfPrivateMessage, options => options.Ignore())
            .ForMember(model => model.AllowSendingOfWelcomeMessage, options => options.Ignore())
            .ForMember(model => model.AllowReSendingOfActivationMessage, options => options.Ignore())
            .ForMember(model => model.GdprEnabled, options => options.Ignore())
            .ForMember(model => model.MultiFactorAuthenticationProvider, options => options.Ignore())
            .ForMember(model => model.CustomerAssociatedExternalAuthRecordsSearchModel, options => options.Ignore())
            .ForMember(model => model.CustomerAddressSearchModel, options => options.Ignore())
            .ForMember(model => model.CustomerOrderSearchModel, options => options.Ignore())
            .ForMember(model => model.CustomerShoppingCartSearchModel, options => options.Ignore())
            .ForMember(model => model.CustomerActivityLogSearchModel, options => options.Ignore())
            .ForMember(model => model.CustomerBackInStockSubscriptionSearchModel, options => options.Ignore())
            .ForMember(model => model.BranchId, options => options.Ignore())
            .ForMember(model => model.RegisteredInBranch, options => options.Ignore());
    }

    /// <summary>
    /// Create branch maps 
    /// </summary>
    private void CreateBranchMaps()
    {
        CreateMap<BranchModel, Branch>()
            .ForMember(entity => entity.CreatedOnUtc, options => options.Ignore())
            .ForMember(entity => entity.UpdatedOnUtc, options => options.Ignore())
            .ForMember(entity => entity.DomainRegistrationFilter, options => options.Ignore())
            .ForMember(entity => entity.Deleted, options => options.Ignore());

        CreateMap<Branch, BranchModel>()
             .ForMember(model => model.StaffCount, options => options.Ignore())
             .ForMember(model => model.AvailableStores, options => options.Ignore())
             .ForMember(model => model.CWAddressSearchModel, options => options.Ignore())
             .ForMember(model => model.AvailableBranches, options => options.Ignore())
             .ForMember(model => model.StoreName, options => options.Ignore());
    }

    /// <summary>
    /// Create company maps 
    /// </summary>
    private void CreatePackNameMaps()
    {
        //StoreId and BranchId ignored: both are parents, and mapping them from a post moved a list -
        //with its packnames and its manager mappings - into another store.
        //SavePackNameList sets both explicitly, from the branch on insert and from the entity on edit.
        CreateMap<PackNameListModel, PackNameList>()
            .ForMember(entity => entity.CreatedOnUtc, options => options.Ignore())
            .ForMember(entity => entity.StoreId, options => options.Ignore())
            .ForMember(entity => entity.BranchId, options => options.Ignore());

        CreateMap<PackNameList, PackNameListModel>()
            .ForMember(model => model.Store, options => options.Ignore())
            .ForMember(model => model.Branch, options => options.Ignore());

        //PackNameListId ignored. Moving a packname to another list in the SAME store is a legitimate
        //edit - so unlike StoreId this is a real form field - but there is no validator for this model
        //and no [Required] on the property, so a post that omits it mapped 0 onto the entity and
        //orphaned the packname: no list, therefore no branch, therefore no store, therefore no admin
        //screen will touch it again. The save paths write it explicitly, and only when it is > 0.
        CreateMap<PackNameModel, PackName>()
            .ForMember(entity => entity.PackNameListId, options => options.Ignore());

        CreateMap<PackName, PackNameModel>()
            .ForMember(model => model.Branch, options => options.Ignore());

        CreateMap<PackName, PackNameSubsidiesModel>();
        CreateMap<PackNameSubsidiesModel, PackName>();
    }

    /// <summary>
    /// Create subsidy maps 
    /// </summary>
    protected virtual void CreateSubsidyMaps()
    {
        CreateMap<Core.Domain.Subsidies.Subsidy, SubsidyModel>()
            .ForMember(model => model.StartDate, options => options.Ignore())
            .ForMember(model => model.EndDate, options => options.Ignore())
            .ForMember(model => model.AllocatedStaff, options => options.Ignore())
            .ForMember(model => model.TotalCredit, options => options.Ignore())
            .ForMember(model => model.UsedPercent, options => options.Ignore())
            .ForMember(model => model.PrimaryStoreCurrencyCode, options => options.Ignore());

        //StoreId ignored - see the product-group map above.
        CreateMap<SubsidyModel, Core.Domain.Subsidies.Subsidy>()
            .ForMember(entity => entity.CreatedOnUtc, options => options.Ignore())
            .ForMember(entity => entity.StoreId, options => options.Ignore());

        CreateMap<SubsidyTopup, SubsidyTopupModel>();

        CreateMap<SubsidyTopupModel, SubsidyTopup>()
            .ForMember(entity => entity.CreatedOnUtc, options => options.Ignore());

        CreateMap<SubsidyVariation, SubsidyVariationModel>();

        CreateMap<SubsidyVariationModel, SubsidyVariation>()
            .ForMember(entity => entity.CreatedOnUtc, options => options.Ignore());
    }

    /// <summary>
    /// Create product groups maps 
    /// </summary>
    protected virtual void CreateProductGroupMaps()
    {
        CreateMap<ProductGroup, ProductGroupModel>()
            .ForMember(model => model.ProductCount, options => options.Ignore())
            .ForMember(model => model.IsNew, options => options.Ignore());

        //StoreId is NOT a form field: it is the entity's parent. Mapped from the post, an existing
        //product group could be moved into another store by editing a hidden input, carrying its
        //subsidy allocations with it. Insert paths set it explicitly.
        CreateMap<ProductGroupModel, ProductGroup>()
            .ForMember(entity => entity.CreatedOnUtc, options => options.Ignore())
            .ForMember(entity => entity.StoreId, options => options.Ignore());
    }

    #endregion
}