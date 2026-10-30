using Nop.Core;
using Nop.Core.Domain.Common;
using Nop.Plugin.CW.Core.Domain.Common;

namespace Nop.Plugin.CW.Core.Domain.Branches
{
    public class Branch : BaseEntity, ISoftDeletedEntity, INamedEntity
    {
        /// <summary>
        /// Gets or sets the identifier of the parent branch, if any.
        /// </summary>
        public int? ParentBranchId { get; set; }

        /// <summary>
        /// Gets or sets the branch code
        /// </summary>
        public string BranchCode { get; set; }

        /// <summary>
        /// Gets or sets the name
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the unique code associated with the branch.
        /// </summary>
        public string CardCode { get; set; }

        /// <summary>
        /// Gets or sets the branch number
        /// </summary>
        public string BranchNumber { get; set; }

        /// <summary>
        /// Gets or sets the store identifier
        /// </summary>
        public int StoreId { get; set; }

        /// <summary>
        /// Gets or sets the payment terms
        /// </summary>
        public string PaymentTerms { get; set; }

        /// <summary>
        /// Not used: no page shows or writes it since store self-registration was removed; the column is kept
        /// </summary>
        public string DomainRegistrationFilter { get; set; }

        /// <summary>
        /// Gets or sets the comma-separated names of the shipping methods packnames of this branch may use at the
        /// checkout, the first one selected by default; empty means the store's own selection
        /// </summary>
        public string ShippingMethod { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the entity is active
        /// </summary>
        public bool Active { get; set; }

        /// <summary>
        /// Gets or sets the identifier of the default shipping address for this branch.
        /// Null means no default has been set.
        /// </summary>
        public int? DefaultAddressId { get; set; }

        /// <summary>
        /// Gets or sets the date and time of instance creation
        /// </summary>
        public DateTime CreatedOnUtc { get; set; }

        /// <summary>
        /// Gets or sets the date and time of product update
        /// </summary>
        public DateTime UpdatedOnUtc { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the entity has been deleted
        /// </summary>
        public bool Deleted { get; set; }
    }
}
