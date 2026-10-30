namespace Nop.Plugin.CW.Core.Domain.Exchanges.DTOs;

/// <summary>
/// What the admin order lists flag on an order: <c>E</c> when it holds the replacement of an exchange, <c>R</c>
/// when an item of it was returned and the return authorised.
/// </summary>
/// <param name="OrderId">Order identifier</param>
/// <param name="HasExchange">The order holds the replacement of an exchange</param>
/// <param name="HasReturn">An item of the order was returned and the return authorised</param>
public record OrderReturnFlags(int OrderId, bool HasExchange, bool HasReturn);