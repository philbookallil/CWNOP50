using Nop.Core.Domain.Customers;
using Nop.Plugin.CW.Core.Domain.PackNames;

namespace Nop.Plugin.CW.Core.Services.PackNames;

/// <summary>
/// Manages per-packname shipping assignment data (address, selected method, offered methods)
/// as a single consolidated generic attribute per customer per store.
/// This replaces the previous pattern of one generic attribute per packname per data type,
/// which caused N×3 database reads on every checkout page render.
/// </summary>
public interface IPackNameShippingAssignmentService
{
    /// <summary>
    /// Loads all packname shipping assignments for the given customer and store in a single database read.
    /// Returns an empty collection (not null) when no assignments are stored yet.
    /// </summary>
    Task<PackNameShippingAssignments> LoadAsync(Customer customer, int storeId);

    /// <summary>
    /// Saves all packname shipping assignments for the given customer and store in a single database write.
    /// </summary>
    Task SaveAsync(Customer customer, int storeId, PackNameShippingAssignments assignments);

    /// <summary>
    /// Removes all packname shipping assignments for the given customer and store.
    /// Called when an order is placed or shipping data needs a full reset.
    /// </summary>
    Task ClearAsync(Customer customer, int storeId);
}
