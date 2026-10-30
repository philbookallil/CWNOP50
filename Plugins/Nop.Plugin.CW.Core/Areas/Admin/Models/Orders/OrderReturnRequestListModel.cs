using Nop.Web.Areas.Admin.Models.Orders;
using Nop.Web.Framework.Models;

namespace Nop.Plugin.CW.Core.Areas.Admin.Models.Orders;

public partial record OrderReturnRequestListModel : BasePagedListModel<ReturnRequestModel>;