using Nop.Web.Framework.Models;

namespace Nop.Plugin.CW.Core.Areas.Admin.Models.Subsidies
{
    public partial record AddPackNameToSubsidyModel : BaseNopModel
    {
        #region Ctor

        public AddPackNameToSubsidyModel()
        {
            SelectedPackNameIds = new List<int>();
        }

        #endregion

        #region Properties

        public int SubsidyId { get; set; }

        public IList<int> SelectedPackNameIds { get; set; }

        #endregion
    }
}
