using Nop.Core.Domain.Common;

namespace Nop.Plugin.CW.Core.Services.Common;

public interface ICWAddressService
{
    Task<int> InsertCheckoutAddressAsync(Address checkoutAddress);
}
