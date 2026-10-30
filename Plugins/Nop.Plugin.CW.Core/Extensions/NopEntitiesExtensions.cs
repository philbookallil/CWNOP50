using Nop.Core.Domain.Common;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Stores;
using Nop.Services.Security;
using Nop.Web.Models.ShoppingCart;
using System.Text;

namespace Nop.Plugin.CW.Core.Extensions;

public static class NopEntitiesExtensions
{
    public static void CopyFrom(this Address address, Address source)
    {
        if (address != null && source != null)
        {
            address.Address1 = source.Address1;
            address.Address2 = source.Address2;
            address.City = source.City;
            address.Company = source.Company;
            address.CountryId = source.CountryId;
            address.CustomAttributes = source.CustomAttributes;
            address.Email = source.Email;
            address.FaxNumber = source.FaxNumber;
            address.FirstName = source.FirstName;
            address.LastName = source.LastName;
            address.PhoneNumber = source.PhoneNumber;
            address.StateProvinceId = source.StateProvinceId;
            address.ZipPostalCode = source.ZipPostalCode;
        }

    }

    public static bool IsEmpty(this Address address)
    {
        return string.IsNullOrEmpty(address.Address1) && string.IsNullOrEmpty(address.Address2) && string.IsNullOrEmpty(address.City) &&
            !address.CountryId.HasValue && !address.StateProvinceId.HasValue && string.IsNullOrEmpty(address.ZipPostalCode);
    }

    /// <summary>
    /// Gets a value indicating whether customer is administrator
    /// </summary>
    /// <param name="customer">Customer</param>
    /// <param name="onlyActiveCustomerRoles">A value indicating whether we should look only in active customer roles</param>
    /// <returns>Result</returns>
    public static async Task<bool> IsAllowedImpersonationAsync(this Customer customer, IPermissionService _permissionService)
    {
        return await _permissionService.AuthorizeAsync(StandardPermission.Customers.CUSTOMERS_IMPERSONATION);
    }

    public static string FormattedAttributesInfo(this ShoppingCartModel.ShoppingCartItemModel shoppingCartItem)
    {
        // This will result in an array which items will be label/value pairs in the form of LABEL: VALUE
        var attributesInfoParts = shoppingCartItem.AttributeInfo.Split(new string[] { "<br />" }, StringSplitOptions.RemoveEmptyEntries);
        StringBuilder formattedAttributesSB = new StringBuilder();
        foreach (var attributesInfoPart in attributesInfoParts)
        {
            if (formattedAttributesSB.Length > 0)
            {
                formattedAttributesSB.Append(" ");
            }
            // This will result in an array with length = 2, position 0 will have the "label" and position 1 the text value
            var attributeValueParts = attributesInfoPart.Split(new string[] { ": " }, StringSplitOptions.RemoveEmptyEntries);
            formattedAttributesSB.Append("<span class=\"value-label\"><strong>" + attributeValueParts[0] + ": </strong></span>");
            formattedAttributesSB.Append("<span class=\"value-text\">" + attributeValueParts[1] + "</span>");
        }
        return formattedAttributesSB.ToString();
    }

    public static string GetCompanyName(this Store store)
    {
        return !string.IsNullOrEmpty(store.CompanyName) ? store.CompanyName : store.Name;
    }

    public static string GetUrl(this Store store)
    {
        return store.Url.EndsWith("/") ? store.Url : string.Concat(store.Url, "/");
    }
}
