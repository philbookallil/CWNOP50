namespace Nop.Plugin.Integration.Odoo.Services;

/// <summary>
/// Synchronises Odoo departments (<c>x_department</c>) into CW.Core Departments (each backed by a
/// <c>DEP_{storeId}_{name}</c> customer role), reusing CW.Core's existing provisioning logic.
/// </summary>
public interface IDepartmentSyncService
{
    /// <summary>
    /// Creates/updates a CW.Core Department (+ its role) for each active Odoo department, under the
    /// nopCommerce store resolved from <c>x_studio_store</c> via the store mapping.
    /// </summary>
    Task<DepartmentSyncResult> SyncDepartmentsAsync(int logEventId);
}

/// <summary>
/// Aggregated result of a department sync run. Every x_department the run retrieved is counted exactly
/// once (Skipped = no store, store not mapped, no name, archived with nothing to deactivate, or failed),
/// so Processed is their sum by construction.
/// </summary>
public record DepartmentSyncResult(int Created, int Updated, int Skipped)
{
    public int Processed => Created + Updated + Skipped;
}
