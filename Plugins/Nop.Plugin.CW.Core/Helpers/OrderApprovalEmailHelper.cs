using Nop.Core;

namespace Nop.Plugin.CW.Core.Helpers;

/// <summary>
/// The approvers' e-mail addresses, as a form types them, and how an approver is named
/// </summary>
public static class OrderApprovalEmailHelper
{
    /// <summary>
    /// The longest address kept
    /// </summary>
    public const int MaxLength = 255;

    /// <summary>
    /// The longest approver's name kept: with the longest address it still fits who decided an order
    /// </summary>
    public const int MaxNameLength = 200;

    private static readonly char[] _separators = [',', ';', '|', '\r', '\n'];

    /// <summary>
    /// The addresses of a typed list, each once, in the order typed
    /// </summary>
    /// <param name="emails">Addresses separated by commas, semicolons, bars or new lines</param>
    public static IReadOnlyList<string> Parse(string emails)
        => (emails ?? string.Empty)
            .Split(_separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.InvariantCultureIgnoreCase)
            .ToList();

    /// <summary>
    /// Addresses as a form shows them
    /// </summary>
    /// <param name="emails">The addresses</param>
    public static string Format(IEnumerable<string> emails)
        => string.Join(", ", emails ?? []);

    /// <summary>
    /// Whether an address is one an e-mail can be sent to
    /// </summary>
    /// <param name="email">The address</param>
    public static bool IsValid(string email)
        => !string.IsNullOrWhiteSpace(email) && email.Length <= MaxLength && CommonHelper.IsValidEmail(email);

    /// <summary>
    /// An approver's name and address; the address alone for an approver named by it
    /// </summary>
    /// <param name="name">The approver's name</param>
    /// <param name="email">The approver's e-mail address</param>
    public static string NameAndEmail(string name, string email)
        => string.IsNullOrWhiteSpace(name) || string.Equals(name, email, StringComparison.InvariantCultureIgnoreCase)
            ? email
            : $"{name} ({email})";
}
