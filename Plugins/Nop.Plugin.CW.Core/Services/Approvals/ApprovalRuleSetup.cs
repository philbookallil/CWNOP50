namespace Nop.Plugin.CW.Core.Services.Approvals;

/// <summary>
/// The approval rule of a branch or a department, as its form shows and saves it
/// </summary>
/// <param name="Required">Whether the orders of its customers need approval</param>
/// <param name="Threshold">The order total an order must be over; null or 0 means every order</param>
/// <param name="Emails">The approvers' e-mail addresses</param>
public record ApprovalRuleSetup(bool Required, decimal? Threshold, IReadOnlyList<string> Emails);
