using Nop.Data.Mapping;
using Nop.Plugin.CW.Freight.Domain;

namespace Nop.Plugin.CW.Freight.Data.Mapping;

/// <summary>
/// Maps entity types to their table names (CW_FR_*)
/// </summary>
public class CWFreightNameCompatibility : INameCompatibility
{
    public Dictionary<Type, string> TableNames => new()
    {
        { typeof(CWShippingMethod),                    "CW_FR_ShippingMethod" },
        { typeof(CWShippingMethodStoreMapping),        "CW_FR_ShippingMethodStore" },
        { typeof(CWShippingMethodCustomerRoleMapping), "CW_FR_ShippingMethodCustomerRole" },
        { typeof(CWShippingMethodCountryMapping),      "CW_FR_ShippingMethodCountry" },
    };

    public Dictionary<(Type, string), string> ColumnName => new();
}
