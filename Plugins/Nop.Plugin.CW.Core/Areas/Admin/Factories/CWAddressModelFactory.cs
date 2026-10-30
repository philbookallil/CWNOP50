using Nop.Core.Domain.Common;
using Nop.Plugin.CW.Core.Areas.Admin.Models.Common;
using Nop.Plugin.CW.Core.Domain.Common;
using Nop.Services.Attributes;
using Nop.Services.Directory;
using Nop.Web.Areas.Admin.Factories;
using Nop.Web.Areas.Admin.Infrastructure.Mapper.Extensions;
using Nop.Web.Areas.Admin.Models.Common;
using System.Net;
using System.Text;

namespace Nop.Plugin.CW.Core.Areas.Admin.Factories;

public class CWAddressModelFactory : ICWAddressModelFactory
{
    #region Fields

    protected readonly ICountryService _countryService;
    protected readonly IStateProvinceService _stateProvinceService;
    protected readonly AddressSettings _addressSettings;
    protected readonly IAttributeFormatter<AddressAttribute, AddressAttributeValue> _addressAttributeFormatter;
    protected readonly IAddressModelFactory _addressModelFactory;

    #endregion

    #region Ctor

    public CWAddressModelFactory(ICountryService countryService,
        IStateProvinceService stateProvinceService,
        AddressSettings addressSettings,
        IAttributeFormatter<AddressAttribute, AddressAttributeValue> addressAttributeFormatter,
        IAddressModelFactory addressModelFactory)
    {
        _countryService = countryService;
        _stateProvinceService = stateProvinceService;
        _addressSettings = addressSettings;
        _addressAttributeFormatter = addressAttributeFormatter;
        _addressModelFactory = addressModelFactory;
    }

    #endregion

    #region Utilities

    /// <summary>
    /// Prepare HTML string address
    /// </summary>
    /// <param name="model">Address model</param>
    /// <param name="address">Address</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    protected virtual async Task PrepareModelAddressHtmlAsync(AddressModel model, Address address)
    {
        ArgumentNullException.ThrowIfNull(model);

        var addressHtmlSb = new StringBuilder("<div>");

        if (_addressSettings.CompanyEnabled && !string.IsNullOrEmpty(model.Company))
            addressHtmlSb.AppendFormat("{0}<br />", WebUtility.HtmlEncode(model.Company));

        if (_addressSettings.StreetAddressEnabled && !string.IsNullOrEmpty(model.Address1))
            addressHtmlSb.AppendFormat("{0}<br />", WebUtility.HtmlEncode(model.Address1));

        if (_addressSettings.StreetAddress2Enabled && !string.IsNullOrEmpty(model.Address2))
            addressHtmlSb.AppendFormat("{0}<br />", WebUtility.HtmlEncode(model.Address2));

        if (_addressSettings.CityEnabled && !string.IsNullOrEmpty(model.City))
            addressHtmlSb.AppendFormat("{0},", WebUtility.HtmlEncode(model.City));

        if (_addressSettings.CountyEnabled && !string.IsNullOrEmpty(model.County))
            addressHtmlSb.AppendFormat("{0},", WebUtility.HtmlEncode(model.County));

        if (_addressSettings.StateProvinceEnabled && !string.IsNullOrEmpty(model.StateProvinceName))
            addressHtmlSb.AppendFormat("{0},", WebUtility.HtmlEncode(model.StateProvinceName));

        if (_addressSettings.ZipPostalCodeEnabled && !string.IsNullOrEmpty(model.ZipPostalCode))
            addressHtmlSb.AppendFormat("{0}<br />", WebUtility.HtmlEncode(model.ZipPostalCode));

        if (_addressSettings.CountryEnabled && !string.IsNullOrEmpty(model.CountryName))
            addressHtmlSb.AppendFormat("{0}", WebUtility.HtmlEncode(model.CountryName));

        var customAttributesFormatted = await _addressAttributeFormatter.FormatAttributesAsync(address?.CustomAttributes);
        if (!string.IsNullOrEmpty(customAttributesFormatted))
        {
            //already encoded
            addressHtmlSb.AppendFormat("<br />{0}", customAttributesFormatted);
        }

        addressHtmlSb.Append("</div>");

        model.AddressHtml = addressHtmlSb.ToString();
    }

    #endregion

    #region Methods

    /// <summary>
    /// Prepare CW address search model
    /// </summary>
    /// <param name="searchModel">CW address search model</param>
    /// <param name="entityId">Entity identifier</param>
    /// <returns>CW address search model</returns>
    public virtual Task<CWAddressSearchModel> PrepareCWAddressSearchModelAsync(CWAddressSearchModel searchModel, int entityId)
    {
        ArgumentNullException.ThrowIfNull(searchModel);

        searchModel.EntityId = entityId;

        //prepare page parameters
        searchModel.SetGridPageSize();

        return Task.FromResult(searchModel);
    }

    public virtual async Task<CWAddressModel> PrepareCWAddressModelAsync(Address address)
    {
        //fill in model values from the entity        
        var addressModel = address.ToModel<AddressModel>();

        addressModel.CountryName = (await _countryService.GetCountryByAddressAsync(address))?.Name;
        addressModel.StateProvinceName = (await _stateProvinceService.GetStateProvinceByAddressAsync(address))?.Name;

        //fill in additional values (not existing in the entity)
        await PrepareModelAddressHtmlAsync(addressModel, address);

        return new CWAddressModel
        {
            Address = addressModel,
            IsEditable = true,
        };
    }

    /// <summary>
    /// Prepare CW address model
    /// </summary>
    /// <param name="model">CW address model</param>
    /// <param name="entity">Entity</param>
    /// <param name="address">Address</param>
    /// <param name="excludeProperties">Whether to exclude populating of some properties of model</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the CW address model
    /// </returns>
    public virtual async Task<CWAddressModel> PrepareCWAddressModelAsync(CWAddressModel model,
        INamedEntity entity, Address address, bool excludeProperties = false)
    {
        ArgumentNullException.ThrowIfNull(entity);

        if (address != null)
        {
            //fill in model values from the entity
            model ??= new CWAddressModel();

            //whether to fill in some of properties
            if (!excludeProperties)
                model.Address = address.ToModel(model.Address);
        }

        model.EntityId = entity.Id;
        model.EntityName = entity.Name;

        //prepare address model
        await _addressModelFactory.PrepareAddressModelAsync(model.Address, address);
        model.Address.FirstNameRequired = true;
        model.Address.LastNameRequired = true;
        model.Address.EmailRequired = true;
        model.Address.CompanyRequired = _addressSettings.CompanyRequired;
        model.Address.CityRequired = _addressSettings.CityRequired;
        model.Address.CountyRequired = _addressSettings.CountyRequired;
        model.Address.StreetAddressRequired = _addressSettings.StreetAddressRequired;
        model.Address.StreetAddress2Required = _addressSettings.StreetAddress2Required;
        model.Address.ZipPostalCodeRequired = _addressSettings.ZipPostalCodeRequired;
        model.Address.PhoneRequired = _addressSettings.PhoneRequired;
        model.Address.FaxRequired = _addressSettings.FaxRequired;

        return model;
    }

    #endregion
}
