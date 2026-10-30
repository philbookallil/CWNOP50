namespace Nop.Plugin.CW.Core.Domain.Subsidies.DTOs;

public class SubsidyCredit : ICloneable
{
    #region Ctor

    public SubsidyCredit()
    {
        ProductGroupCredits = new List<ProductGroupCredit>();
    }

    #endregion

    #region Properties

    public int SubsidyId { get; set; }

    public decimal Credit { get; set; }

    public decimal CreditTopup { get; set; }

    public decimal AvailableCredit { get; set; }

    public decimal AvailableCreditTopup { get; set; }

    public bool OnlyAllocatedProducts { get; set; }

    public bool RestrictToAllocatedQty { get; set; }

    public IList<ProductGroupCredit> ProductGroupCredits { get; set; }

    #endregion

    #region Methods

    public object Clone()
    {
        return new SubsidyCredit
        {
            SubsidyId = this.SubsidyId,
            Credit = this.Credit,
            CreditTopup = this.CreditTopup,
            AvailableCredit = this.AvailableCredit,
            AvailableCreditTopup = this.AvailableCreditTopup,
            OnlyAllocatedProducts = this.OnlyAllocatedProducts,
            RestrictToAllocatedQty = this.RestrictToAllocatedQty,
            ProductGroupCredits = new List<ProductGroupCredit>(this.ProductGroupCredits.Select(x => (ProductGroupCredit)x.Clone()))
        };
    }

    #endregion
}
