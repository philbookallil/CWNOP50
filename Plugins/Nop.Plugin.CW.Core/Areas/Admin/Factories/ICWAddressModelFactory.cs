using Nop.Core.Domain.Common;
using Nop.Plugin.CW.Core.Areas.Admin.Models.Common;
using Nop.Plugin.CW.Core.Domain.Common;

namespace Nop.Plugin.CW.Core.Areas.Admin.Factories;

public interface ICWAddressModelFactory
{
    /// <summary>
    /// Prepare CW address search model
    /// </summary>
    /// <param name="searchModel">CW address search model</param>
    /// <param name="entityId">Entity identifier</param>
    /// <returns>CW address search model</returns>
    Task<CWAddressSearchModel> PrepareCWAddressSearchModelAsync(CWAddressSearchModel searchModel, int entityId);

    Task<CWAddressModel> PrepareCWAddressModelAsync(Address address);

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
    Task<CWAddressModel> PrepareCWAddressModelAsync(CWAddressModel model,
       INamedEntity entity, Address address, bool excludeProperties = false);
}
