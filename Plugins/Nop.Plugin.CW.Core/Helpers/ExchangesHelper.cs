using Nop.Core.Domain.Orders;
using Nop.Plugin.CW.Core.Domain.Exchanges;
using Nop.Plugin.CW.Core.Models.Exchanges;
using Nop.Plugin.CW.Core.Models.Exchanges.FeeRule;
using Nop.Web.Models.Catalog;

namespace Nop.Plugin.CW.Core.Helpers;

/// <summary>
/// Pure helper routines for the Exchanges and Returns feature (no service dependencies)
/// </summary>
public static class ExchangesHelper
{
    private static readonly IList<ReturnRequestStatus> _finalStatuses = new List<ReturnRequestStatus>
    {
        ReturnRequestStatus.ReturnAuthorized
    };

    public static string DetermineFeeTypeName(FeeType feeType) => feeType switch
    {
        FeeType.RepackagingFee => "Repackaging Fee",
        FeeType.ReShippingFee => "Redelivery Fee",
        FeeType.AdditionalFee => "Additional Fees",
        _ => "Unknown"
    };

    public static string DetermineFeeRuleOperatorName(FeeRuleOperator feeOperator) => feeOperator switch
    {
        FeeRuleOperator.FixedPrice => "Fixed Price",
        FeeRuleOperator.PerUnitsQuantity => "Per Units",
        _ => "Unknown"
    };

    public static string DetermineExchangeTypeName(ExchangeType exchangeType) => exchangeType switch
    {
        ExchangeType.DifferentStyle => "Different Style",
        ExchangeType.SameStyle => "Same Style",
        ExchangeType.Exact => "Exact Exchange",
        _ => null
    };

    public static string DetermineExchangeModeName(ExchangeReturnMode exchangeMode) => exchangeMode switch
    {
        ExchangeReturnMode.CreditNote => "Credit Note",
        ExchangeReturnMode.Exchange => "Exchange",
        _ => null
    };

    public static decimal CalculateFeeAmount(FeeRule feeItem, int itemsQuantity)
    {
        switch (feeItem.Operator)
        {
            case FeeRuleOperator.PerUnitsQuantity:
                //A rule with no band is not a band. Guarded here because this is the only fee arithmetic
                //in the system, and an unguarded divide gives int.MinValue rather than an error — which,
                //since fees are frozen at resolution, would be written into a charge row and outlive the
                //mistake.
                if (feeItem.UnitsQuantity <= 0)
                    return decimal.Zero;

                var quantityMultiplier = (int)Math.Ceiling((double)itemsQuantity / feeItem.UnitsQuantity);
                return quantityMultiplier * feeItem.Amount;

            case FeeRuleOperator.FixedPrice:
                return itemsQuantity > 0 ? feeItem.Amount : 0;

            default:
                return 0;
        }
    }

    public static decimal CalculateFeeAmount(FeeConceptItem feeItem, int itemsQuantity)
    {
        switch (feeItem.FeeOperator)
        {
            case FeeRuleOperator.PerUnitsQuantity:
                //See the FeeRule overload above.
                if (feeItem.FeeUnitsQuantity <= 0)
                    return decimal.Zero;

                var quantityMultiplier = (int)Math.Ceiling((double)itemsQuantity / feeItem.FeeUnitsQuantity);
                return quantityMultiplier * feeItem.FeeAmount;

            case FeeRuleOperator.FixedPrice:
                return itemsQuantity > 0 ? feeItem.FeeAmount : 0;

            default:
                return 0;
        }
    }

    public static decimal CalculatePriceDifference(ReturnRequestGatherInfoModel.ReturnedOrderItemModel returnedItem)
    {
        var replacementItem = returnedItem.ReplacementProduct;

        if (replacementItem != null)
        {
            var quantity = returnedItem.ReturnedQuantity;
            var replacementTotalPrice = quantity * replacementItem.UnitPrice;
            var currentItemTotalPrice = quantity * returnedItem.UnitPrice;

            return replacementTotalPrice - currentItemTotalPrice;
        }

        return 0;
    }

    public static bool IsFinalReturnRequestStatus(ReturnRequestStatus status)
    {
        return _finalStatuses.Any(s => s == status);
    }

    public static int DetermineFeeRuleQuantity(FeeRule feeRule, ReturnRequest returnRequest, ReturnRequestItem returnRequestItem)
    {
        var qty = returnRequest.Quantity;
        switch (feeRule.FeeType)
        {
            case FeeType.RepackagingFee:
                if (returnRequestItem.StaffRepackQty.HasValue)
                    qty = returnRequestItem.StaffRepackQty.Value;
                break;
            case FeeType.ReShippingFee:
                if (returnRequestItem.StaffShipQty.HasValue)
                    qty = returnRequestItem.StaffShipQty.Value;
                break;
        }
        return qty;
    }

    public static decimal DetermineReturnedItemPrice(OrderItem returnedItem, bool inclTax = true)
    {
        return inclTax ? returnedItem.UnitPriceInclTax : returnedItem.UnitPriceExclTax;
    }

    /// <summary>
    /// Whether the replacement picker has any attribute to put on screen for this product.
    /// </summary>
    /// <remarks>
    /// Was "Count >= 0", which is true for every non-null list, so the guard never guarded anything
    /// and an attribute-free product rendered an empty attributes block. Rental periods and gift card
    /// details used to be ORed in here, but the picker has never had fields for either; they are now
    /// reported by ReplacementProductOptionsModel.UncollectableRequiredOptions instead.
    /// </remarks>
    public static bool HasProductAttributes(ProductDetailsModel productDetails)
    {
        return productDetails?.ProductAttributes != null && productDetails.ProductAttributes.Count > 0;
    }
}
