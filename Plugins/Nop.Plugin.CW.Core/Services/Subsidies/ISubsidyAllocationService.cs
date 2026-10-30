using Nop.Plugin.CW.Core.Domain.Subsidies.DTOs;

namespace Nop.Plugin.CW.Core.Services.Subsidies;

/// <summary>
/// The single subsidy allocation pass over a cart. Replaces the three divergent copies that previously
/// lived in OrderTotalCalculationOverrideService (twice) and OrderProcessingOverrideService (once).
///
/// This service decides consumption and reports it; it never persists. Callers that need the
/// consumption recorded copy the returned rows onto real order items — see SubsidySettlementSource.
/// </summary>
public interface ISubsidyAllocationService
{
    /// <summary>
    /// Allocates subsidy across the cart in the order the subsidy model requires: per item, product
    /// group quantity then quantity topup, then product group credit, credit topup, global credit and
    /// global topup; then shipping. Honours <see cref="SubsidyAllocationRequest.BudgetInclTax"/>.
    /// </summary>
    Task<SubsidyAllocation> AllocateAsync(SubsidyAllocationRequest request);
}