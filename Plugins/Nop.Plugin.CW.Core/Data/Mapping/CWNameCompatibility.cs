using Nop.Data.Mapping;
using Nop.Plugin.CW.Core.Domain.Branches;
using Nop.Plugin.CW.Core.Domain.CreditNotes;
using Nop.Plugin.CW.Core.Domain.CreditNotes.Views;
using Nop.Plugin.CW.Core.Domain.Customers;
using Nop.Plugin.CW.Core.Domain.Exchanges;
using Nop.Plugin.CW.Core.Domain.MultiStores;
using Nop.Plugin.CW.Core.Domain.Orders;
using Nop.Plugin.CW.Core.Domain.Orders.Views;
using Nop.Plugin.CW.Core.Domain.PackNames;
using Nop.Plugin.CW.Core.Domain.Returns;
using Nop.Plugin.CW.Core.Domain.Subsidies;
using Nop.Plugin.CW.Core.Domain.Subsidies.Views;

namespace Nop.Plugin.CW.Core.Data.Mapping;

/// <summary>
/// CW instance of backward compatibility of table naming
/// </summary>
public class CWNameCompatibility : INameCompatibility
{
    public Dictionary<Type, string> TableNames => new()
    {
        // Credit Notes
        { typeof(CreditNote), "CORE_EX_CreditNote" },
        { typeof(CreditNoteHistory), "CORE_EX_CreditNoteHistory" },
        { typeof(CreditNoteBalance), "vw_CORE_EX_CreditNoteRemain" },
        { typeof(CreditNoteHistoryBalance), "vw_CORE_EX_CreditNoteHistoryRemain" },
        
        // Exchanges - Fee Rules
        { typeof(FeeRule), "CORE_EX_FeeRule" },
        
        // Exchanges - Return Reason Rules
        { typeof(ReturnReasonRule), "CORE_EX_ReturnReasonRule" },
        
        // Exchanges - Return Action Rules
        { typeof(ReturnActionRule), "CORE_EX_ReturnActionRule" },
        
        // Exchanges - Return Status Rules
        { typeof(ReturnStatusRule), "CORE_EX_ReturnStatusRule" },
        
        // Exchanges - Reason Action Relationship
        { typeof(ReasonActionRelationship), "CORE_EX_ReasonActionRelationship" },
        
        // Exchanges - Impersonation Token
        { typeof(ImpersonationToken), "CORE_EX_ImpersonationToken" },
        
        // Exchanges - Return Request Item
        { typeof(ReturnRequestItem), "CORE_EX_ReturnRequestItem" },
        
        // Exchanges - Return Replacement Item
        { typeof(ReturnReplacementItem), "CORE_EX_ReturnReplacementItem" },

        // Returns - Case aggregate
        { typeof(ReturnCase), "CORE_RET_ReturnCase" },
        { typeof(ReturnCaseLine), "CORE_RET_ReturnCaseLine" },
        { typeof(ReturnResolution), "CORE_RET_ReturnResolution" },
        { typeof(ReturnCharge), "CORE_RET_ReturnCharge" },

        //PackNames
        { typeof(Branch), "CORE_Branch" },
        { typeof(BranchAddressMapping), "CORE_BranchAddresses" },
        { typeof(CustomerBranch), "CORE_CustomerBranch" },
        { typeof(Department), "CORE_Department" },
        { typeof(PackNameList), "CORE_PN_PackNameList" },
        { typeof(PackName), "CORE_PN_PackName" },
        { typeof(PackNameAddressMapping), "CORE_PN_PackNameAddresses" },
        { typeof(PackNameListManagerMapping), "CORE_PN_PackNameListManagers" },
        { typeof(CostCenter), "CORE_PN_CostCenter" },
        { typeof(CostCenterAddressMapping), "CORE_PN_CostCenterAddresses" },
        { typeof(DepartmentManagerMapping), "CORE_PN_DepartmentManagers" },
        { typeof(PackNameCartItem), "CORE_PN_PackNameCartItem" },
        { typeof(PackNameOrderItem), "CORE_PN_PackNameOrderItem" },
        { typeof(OrderShipping), "CORE_PN_OrderShipping" },
        { typeof(OrderSettlement), "CORE_PN_OrderSettlement" },
        { typeof(PackNameFullOrderItem), "vw_CORE_PN_PackNameOrderItem" },
        { typeof(PackNameShoppingCartItem), "vw_CORE_PN_PackNameShoppingCartItem" },
        { typeof(ShipmentAddress), "vw_CORE_PN_ShipmentAddress" },

        //Subsidies
        { typeof(Subsidy), "CORE_SUB_Subsidy" },
        { typeof(SubsidyPackName), "CORE_SUB_SubsidyPackName" },
        { typeof(SubsidyTopup), "CORE_SUB_SubsidyTopup" },
        { typeof(ProductGroup), "CORE_SUB_ProductGroup" },
        { typeof(SubsidyProductGroup), "CORE_SUB_SubsidyProductGroup" },
        { typeof(ProductGroupProduct), "CORE_SUB_ProductGroupProduct" },
        { typeof(ProductGroupTopup), "CORE_SUB_ProductGroupTopup" },
        { typeof(SubsidyOrderItem), "CORE_SUB_SubsidyOrderItem" },
        { typeof(SubsidyTopupHistory), "CORE_SUB_SubsidyTopupHistory" },
        { typeof(ProductGroupTopupHistory), "CORE_SUB_ProductGroupTopupHistory" },
        { typeof(SubsidyVariation), "CORE_SUB_SubsidyVariation" },
        { typeof(SubsidyVariationHistory), "CORE_SUB_SubsidyVariationHistory" },
        { typeof(SubsidyOrderShipping), "CORE_SUB_SubsidyOrderShipping" },
        { typeof(SubsidyRestoreItem), "CORE_SUB_SubsidyRestoreItem" },
        { typeof(SubsidyPackNameCredit), "vw_CORE_SUB_SubsidyPackNameCredit" },
        { typeof(ProductGroupPackNameCredit), "vw_CORE_SUB_ProductGroupPackNameCredit" },
        { typeof(SubsidyPackNameCanOrder), "vw_CORE_SUB_SubsidyPackNameCanOrder" },
        { typeof(SubsidyInOrderingPeriod), "vw_CORE_SUB_SubsidyInOrderingPeriod" },
        { typeof(SubsidyVariationInOrderingPeriod), "vw_CORE_SUB_SubsidyVariationInOrderingPeriod" },
        { typeof(StoreSubsidyVariationSetting), "vw_CORE_SUB_StoreSubsidyVariationSetting" },
    };

    public Dictionary<(Type, string), string> ColumnName => new()
    {
    };
}
