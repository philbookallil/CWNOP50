namespace Nop.Plugin.CW.Core.Domain.Returns;

/// <summary>
/// The outcome of reviewing one returned item. Items are judged individually: one garment may come
/// back in good condition and be accepted while another is rejected as worn or soiled.
/// </summary>
public enum ReturnLineDisposition
{
    /// <summary>
    /// Not yet reviewed.
    /// </summary>
    Pending = 1,

    /// <summary>
    /// Accepted. Attracts fees and takes part in credit and exchange outcomes.
    /// </summary>
    Accepted = 2,

    /// <summary>
    /// Rejected. Attracts no fees — it is skipped entirely when a case is priced.
    /// </summary>
    Rejected = 3
}