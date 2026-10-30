using Nop.Web.Framework.Models;

namespace Nop.Plugin.CW.Core.Areas.Admin.Models.Orders;

/// <summary>
/// The grid payload. Its own paged model rather than reusing nop's OrderShipmentListModel so that Data
/// is typed as the row shape the grid actually renders — the Destination column is declared, not
/// smuggled through as a derived instance and left to the serialiser's polymorphism rules.
/// </summary>
public partial record CWOrderShipmentListModel : BasePagedListModel<CWShipmentModel>;