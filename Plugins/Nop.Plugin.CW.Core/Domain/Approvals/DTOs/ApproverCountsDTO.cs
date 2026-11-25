namespace Nop.Plugin.CW.Core.Domain.Approvals.DTOs;

/// <summary>
/// How many orders an approver has been asked about
/// </summary>
/// <param name="Waiting">Orders waiting, with a link of this approver that still works</param>
/// <param name="Approved">Orders this approver accepted</param>
/// <param name="Declined">Orders this approver declined</param>
public readonly record struct ApproverCountsDTO(int Waiting, int Approved, int Declined);
