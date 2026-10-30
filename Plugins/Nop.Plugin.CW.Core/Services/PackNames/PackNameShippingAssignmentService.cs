using Nop.Core.Domain.Customers;
using Nop.Plugin.CW.Core.Domain.PackNames;
using Nop.Services.Common;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Nop.Plugin.CW.Core.Services.PackNames;

public class PackNameShippingAssignmentService : IPackNameShippingAssignmentService
{
    #region Fields

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
    };

    protected readonly IGenericAttributeService _genericAttributeService;

    #endregion

    #region Ctor

    public PackNameShippingAssignmentService(IGenericAttributeService genericAttributeService)
    {
        _genericAttributeService = genericAttributeService;
    }

    #endregion

    #region Methods

    /// <inheritdoc/>
    public virtual async Task<PackNameShippingAssignments> LoadAsync(Customer customer, int storeId)
    {
        ArgumentNullException.ThrowIfNull(customer);

        var json = await _genericAttributeService.GetAttributeAsync<string>(
            customer, PackNameDefaults.PackNameShippingAssignmentsAttribute, storeId);

        if (string.IsNullOrWhiteSpace(json))
            return new PackNameShippingAssignments();

        try
        {
            var data = JsonSerializer.Deserialize<Dictionary<int, PackNameShippingAssignment>>(json, JsonOptions);
            return new PackNameShippingAssignments(data ?? new Dictionary<int, PackNameShippingAssignment>());
        }
        catch
        {
            // Treat corrupt/stale data as empty — it will be re-populated naturally
            return new PackNameShippingAssignments();
        }
    }

    /// <inheritdoc/>
    public virtual async Task SaveAsync(Customer customer, int storeId, PackNameShippingAssignments assignments)
    {
        ArgumentNullException.ThrowIfNull(customer);
        ArgumentNullException.ThrowIfNull(assignments);

        var json = JsonSerializer.Serialize(assignments.All, JsonOptions);
        await _genericAttributeService.SaveAttributeAsync(
            customer, PackNameDefaults.PackNameShippingAssignmentsAttribute, json, storeId);
    }

    /// <inheritdoc/>
    public virtual async Task ClearAsync(Customer customer, int storeId)
    {
        ArgumentNullException.ThrowIfNull(customer);

        await _genericAttributeService.SaveAttributeAsync<string>(
            customer, PackNameDefaults.PackNameShippingAssignmentsAttribute, null, storeId);
    }

    #endregion
}
