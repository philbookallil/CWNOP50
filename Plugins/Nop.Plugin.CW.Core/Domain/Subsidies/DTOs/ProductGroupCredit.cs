namespace Nop.Plugin.CW.Core.Domain.Subsidies.DTOs;

public class ProductGroupCredit : ICloneable
{
    #region Ctor

    public ProductGroupCredit()
    {
        ProductIds = new List<int>();
    }

    #endregion

    #region Properties

    public int ProductGroupId { get; set; }

    public string Name { get; set; }

    public int DisplayOrder { get; set; }

    public decimal Credit { get; set; }

    public int Quantity { get; set; }

    public decimal CreditTopup { get; set; }

    public int QuantityTopup { get; set; }

    public decimal AvailableCredit { get; set; }

    public decimal AvailableCreditTopup { get; set; }

    public int AvailableQuantity { get; set; }

    public int AvailableQuantityTopup { get; set; }

    public IList<int> ProductIds { get; set; }

    #endregion

    #region Methods

    public object Clone()
    {
        return new ProductGroupCredit
        {
            ProductGroupId = this.ProductGroupId,
            Name = this.Name,
            DisplayOrder = this.DisplayOrder,
            Credit = this.Credit,
            Quantity = this.Quantity,
            CreditTopup = this.CreditTopup,
            QuantityTopup = this.QuantityTopup,
            AvailableCredit = this.AvailableCredit,
            AvailableCreditTopup = this.AvailableCreditTopup,
            AvailableQuantity = this.AvailableQuantity,
            AvailableQuantityTopup = this.AvailableQuantityTopup,
            ProductIds = new List<int>(this.ProductIds)
        };
    }

    #endregion
}
