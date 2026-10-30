namespace Nop.Plugin.CW.Core.Domain.Subsidies.DTOs;

public class PackNameCredit : ICloneable
{
    #region Ctor

    public PackNameCredit()
    {
        SubsidyCredits = new List<SubsidyCredit>();
    }

    #endregion

    #region Properties

    public int PackNameId { get; set; }

    public IList<SubsidyCredit> SubsidyCredits { get; set; }

    #endregion

    #region Methods

    public object Clone()
    {
        return new PackNameCredit
        {
            PackNameId = this.PackNameId,
            SubsidyCredits = new List<SubsidyCredit>(this.SubsidyCredits.Select(x => (SubsidyCredit)x.Clone()))
        };
    }

    #endregion
}
