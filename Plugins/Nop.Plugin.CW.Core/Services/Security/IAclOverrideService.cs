using Nop.Core;
using Nop.Core.Domain.Security;
using Nop.Core.Domain.Stores;
using Nop.Services.Security;

namespace Nop.Plugin.CW.Core.Services.Security;

public interface IAclOverrideService : IAclService
{
    /// <summary>
    /// Save ACL Department mapping
    /// </summary>
    /// <typeparam name="TEntity">Type of entity</typeparam>
    /// <param name="entity">Entity</param>
    /// <param name="selectedDepartmentIds">Departments selected on the form</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    /// <remarks>
    /// <c>IStoreMappingSupported</c> is required here: the department ids arrive from the raw request
    /// form (<c>EventConsumer.HandleEntityEventAsync</c>), so the only trustworthy source of the store
    /// is the ENTITY, and the implementation needs its mappings to filter them.
    /// </remarks>
    Task SaveAclDepartmentAsync<TEntity>(TEntity entity, IList<int> selectedDepartmentIds) where TEntity : BaseEntity, IAclSupported, IStoreMappingSupported;
}
